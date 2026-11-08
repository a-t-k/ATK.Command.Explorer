namespace ATK.Command.Explorer.Models;

/// <summary>
/// Beschreibt eine einzelne Property eines Commands.
/// </summary>
public sealed class CommandPropertyDescriptor
{
    /// <summary>Name der Property in C#-Schreibweise.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Name der Property im JSON (camelCase).</summary>
    public string JsonName { get; init; } = string.Empty;

    /// <summary>Anzeigename des .NET-Typs, z. B. <c>string</c> oder <c>List&lt;int&gt;</c>.</summary>
    public string TypeName { get; init; } = string.Empty;

    /// <summary>Vollqualifizierter .NET-Typname.</summary>
    public string FullTypeName { get; init; } = string.Empty;

    /// <summary>Grobe Kategorie für die Formularerzeugung.</summary>
    public CommandPropertyKind Kind { get; init; }

    /// <summary>XML-Summary der Property, falls dokumentiert.</summary>
    public string? Summary { get; init; }

    /// <summary>Gibt an, ob die Property geschrieben werden kann.</summary>
    public bool CanWrite { get; init; }

    /// <summary>Gibt an, ob die Property einen Wertetyp (nicht nullable) darstellt.</summary>
    public bool IsValueType { get; init; }

    /// <summary>Gibt an, ob die Property nullable ist.</summary>
    public bool IsNullable { get; init; }

    /// <summary>Default-Wert als Text, falls vorhanden.</summary>
    public string? DefaultValue { get; init; }

    /// <summary>Erlaubte Werte bei Enums.</summary>
    public IReadOnlyList<CommandEnumValue> EnumValues { get; init; } = [];

    /// <summary>Elementtyp bei Collections, sonst <c>null</c>.</summary>
    public CommandPropertyDescriptor? ElementType { get; init; }

    /// <summary>Properties bei komplexen Objekten, sonst leer.</summary>
    public IReadOnlyList<CommandPropertyDescriptor> Properties { get; init; } = [];
}
