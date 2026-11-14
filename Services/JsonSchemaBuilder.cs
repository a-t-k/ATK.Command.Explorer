using System.Text.Json.Nodes;
using ATK.Command.Explorer.Models;

namespace ATK.Command.Explorer.Services;

/// <summary>
/// Erzeugt aus den Command-Metadaten ein JSON-Schema (Draft 2020-12).
/// </summary>
/// <remarks>
/// Das Schema dient dem Explorer dazu, den eingegebenen JSON-Body vor dem Absenden zu
/// validieren. Es beschreibt dieselben Typen, die auch das ModelBinding erwartet.
/// </remarks>
public sealed class JsonSchemaBuilder
{
    private const string Draft = "https://json-schema.org/draft/2020-12/schema";

    /// <summary>
    /// Erzeugt das Schema für einen Command.
    /// </summary>
    /// <param name="command">Der beschriebene Command.</param>
    public JsonObject Build(CommandDescriptor command)
    {
        ArgumentNullException.ThrowIfNull(command);

        JsonObject properties = [];
        JsonArray required = [];

        foreach (var property in command.Properties)
        {
            properties[property.JsonName] = this.BuildPropertySchema(property);

            if (!property.IsNullable && property.IsValueType)
            {
                required.Add(property.JsonName);
            }
        }

        // $type ist immer Pflicht, weil der ModelBinder darüber den Zieltyp bestimmt.
        properties["$type"] = new JsonObject
        {
            ["type"] = "string",
            ["const"] = command.TypeName,
            ["description"] = "Vollqualifizierter .NET-Typname. Wird vom ModelBinder ausgewertet."
        };
        required.Add("$type");

        return new JsonObject
        {
            ["$schema"] = Draft,
            ["title"] = command.Name,
            ["description"] = command.Summary ?? command.FullName,
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required,
            ["additionalProperties"] = false
        };
    }

    /// <summary>
    /// Erzeugt das Schema für das Ergebnis eines Commands.
    /// </summary>
    /// <param name="result">Beschreibung des Ergebnistyps.</param>
    /// <param name="title">Titel des Schemas, üblicherweise der Typname.</param>
    /// <param name="description">Optionale Beschreibung aus der XML-Dokumentation.</param>
    /// <remarks>
    /// Anders als beim Request-Schema wird <c>additionalProperties</c> nicht eingeschränkt:
    /// Die WebApi darf zusätzliche Felder liefern, ohne dass die Prüfung fehlschlägt.
    /// </remarks>
    public JsonObject BuildResult(CommandPropertyDescriptor result, string title, string? description)
    {
        ArgumentNullException.ThrowIfNull(result);

        var schema = this.BuildPropertySchema(result);
        schema["$schema"] = Draft;
        schema["title"] = title;

        if (!string.IsNullOrWhiteSpace(description))
        {
            schema["description"] = description;
        }

        return schema;
    }

    private JsonObject BuildPropertySchema(CommandPropertyDescriptor property)
    {
        JsonObject schema = new()
        {
            ["title"] = property.Name
        };

        if (!string.IsNullOrWhiteSpace(property.Summary))
        {
            schema["description"] = property.Summary;
        }

        var typeSchema = this.BuildTypeSchema(property);
        if (typeSchema is JsonObject typeObject)
        {
            foreach (var entry in typeObject.ToList())
            {
                typeObject.Remove(entry.Key);
                schema[entry.Key] = entry.Value;
            }
        }

        if (property.IsNullable)
        {
            schema["type"] = ToArrayWithNull(schema["type"]);
        }

        return schema;
    }

    private JsonNode? BuildTypeSchema(CommandPropertyDescriptor property) =>
        property.Kind switch
        {
            CommandPropertyKind.String => new JsonObject { ["type"] = "string" },
            CommandPropertyKind.Integer => new JsonObject { ["type"] = "integer" },
            CommandPropertyKind.Number => new JsonObject { ["type"] = "number" },
            CommandPropertyKind.Boolean => new JsonObject { ["type"] = "boolean" },
            CommandPropertyKind.Date => new JsonObject
            {
                ["type"] = "string",
                ["format"] = "date"
            },
            CommandPropertyKind.DateTime => new JsonObject
            {
                ["type"] = "string",
                ["format"] = "date-time"
            },
            CommandPropertyKind.TimeSpan => new JsonObject
            {
                ["type"] = "string",
                ["format"] = "time"
            },
            CommandPropertyKind.Guid => new JsonObject
            {
                ["type"] = "string",
                ["format"] = "uuid"
            },
            CommandPropertyKind.Binary => new JsonObject
            {
                ["type"] = "string",
                ["contentEncoding"] = "base64"
            },
            CommandPropertyKind.Enum => this.BuildEnumSchema(property),
            CommandPropertyKind.Array => this.BuildArraySchema(property),
            CommandPropertyKind.Object => this.BuildObjectSchema(property),
            CommandPropertyKind.Dictionary => new JsonObject { ["type"] = "object" },
            _ => new JsonObject()
        };

    private JsonObject BuildEnumSchema(CommandPropertyDescriptor property)
    {
        JsonArray values = [];

        foreach (var entry in property.EnumValues)
        {
            values.Add(int.TryParse(entry.Value, out var numeric) ? numeric : entry.Value);
        }

        JsonObject schema = new()
        {
            ["type"] = "integer",
            ["enum"] = values
        };

        if (property.EnumValues.Count > 0)
        {
            JsonObject names = [];
            foreach (var entry in property.EnumValues)
            {
                names[entry.Value] = entry.Name;
            }

            schema["x-enumNames"] = names;
        }

        return schema;
    }

    private JsonObject BuildArraySchema(CommandPropertyDescriptor property)
    {
        JsonObject schema = new() { ["type"] = "array" };

        if (property.ElementType is not null)
        {
            schema["items"] = this.BuildPropertySchema(property.ElementType);
        }

        return schema;
    }

    private JsonObject BuildObjectSchema(CommandPropertyDescriptor property)
    {
        JsonObject properties = [];
        JsonArray required = [];

        foreach (var field in property.Properties)
        {
            properties[field.JsonName] = this.BuildPropertySchema(field);

            if (!field.IsNullable && field.IsValueType)
            {
                required.Add(field.JsonName);
            }
        }

        JsonObject schema = new()
        {
            ["type"] = "object",
            ["properties"] = properties
        };

        if (required.Count > 0)
        {
            schema["required"] = required;
        }

        return schema;
    }

    private static JsonNode? ToArrayWithNull(JsonNode? typeNode)
    {
        if (typeNode is null)
        {
            return new JsonArray("null");
        }

        if (typeNode is JsonArray array)
        {
            if (!array.Any(node => node?.GetValue<string>() == "null"))
            {
                array.Add("null");
            }

            return array;
        }

        return new JsonArray(typeNode.GetValue<string>(), "null");
    }
}
