using System.Collections;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using ATK.Command.AspNet.ModelBinder.CommandAuthentications.Attributes;
using ATK.Command.Explorer.Configuration;
using ATK.Command.Explorer.Models;
using Cortex.Mediator.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace ATK.Command.Explorer.Services;

/// <summary>
/// Erzeugt Beschreibungen aller Commands einer Assembly für den Command Explorer.
/// </summary>
/// <remarks>
/// Welche Typen als Commands gelten, steuert das Marker-Interface, das dem Konstruktor
/// übergeben wird. In der Regel ist das ein leeres Interface wie <c>IRequestCommand</c>,
/// das die Anwendung selbst definiert.
/// </remarks>
public sealed class CommandMetadataProvider
{
    private const int MaxDepth = 4;

    private readonly Assembly assembly;
    private readonly Type markerInterface;
    private readonly XmlDocumentationReader documentation;
    private readonly JsonSchemaBuilder schemaBuilder;
    private readonly JsonSerializerOptions jsonOptions;
    private readonly IReadOnlyList<string> namespacePrefixes;

    /// <summary>
    /// Erzeugt einen Provider für die angegebene Assembly.
    /// </summary>
    /// <param name="assembly">Assembly, die nach Commands durchsucht wird.</param>
    /// <param name="markerInterface">
    /// Interface, das alle Commands implementieren. Bestimmt, welche Typen berücksichtigt werden.
    /// </param>
    /// <param name="options">Optionen des Explorers, insbesondere die Namespace-Prefixe.</param>
    public CommandMetadataProvider(
        Assembly assembly,
        Type markerInterface,
        IOptions<CommandExplorerOptions>? options = null)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(markerInterface);

        this.assembly = assembly;
        this.markerInterface = markerInterface;
        this.documentation = new XmlDocumentationReader(assembly);
        this.schemaBuilder = new JsonSchemaBuilder();
        this.namespacePrefixes = NormalizePrefixes(options?.Value.NamespacePrefixes);
        this.jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
        };
    }

    /// <summary>
    /// Bereinigt die konfigurierten Prefixe und sortiert sie absteigend nach Länge.
    /// </summary>
    /// <remarks>
    /// Die Sortierung sorgt dafür, dass beim Abschneiden der spezifischste Prefix gewinnt:
    /// Ist sowohl <c>ATK</c> als auch <c>ATK.Command.Ui</c> konfiguriert, wird der längere
    /// Prefix verwendet.
    /// </remarks>
    private static IReadOnlyList<string> NormalizePrefixes(IEnumerable<string>? prefixes) =>
        (prefixes ?? [])
            .Where(prefix => !string.IsNullOrWhiteSpace(prefix))
            .Select(prefix => prefix.Trim().TrimEnd('.'))
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(prefix => prefix.Length)
            .ToArray();

    /// <summary>
    /// Liefert alle Commands der Assembly, alphabetisch sortiert.
    /// </summary>
    public IReadOnlyList<CommandDescriptor> GetCommands() =>
        this.assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false }
                           && this.markerInterface.IsAssignableFrom(type))
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .Select(this.Describe)
            .ToArray();

    /// <summary>
    /// Liefert die Beschreibung eines einzelnen Commands oder <c>null</c>.
    /// </summary>
    /// <param name="name">Klassenname oder vollqualifizierter Name des Commands.</param>
    public CommandDescriptor? GetCommand(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return this.GetCommands().FirstOrDefault(command =>
            string.Equals(command.Name, name, StringComparison.OrdinalIgnoreCase)
            || string.Equals(command.FullName, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Liefert die .NET-Typen der Commands in einem Bereich.
    /// </summary>
    /// <param name="scope">
    /// Namespace oder Command-Name. <c>null</c> oder leer bedeutet „alle Commands“.
    /// </param>
    /// <returns>
    /// Die Typen im Bereich oder <c>null</c>, wenn der Bereich unbekannt ist.
    /// </returns>
    /// <remarks>
    /// Wird für den TypeScript-Export gebraucht: Der Generator arbeitet auf <see cref="Type"/>,
    /// nicht auf <see cref="CommandDescriptor"/>. Die Auflösung entspricht der der
    /// Dokumentations-Endpunkte – ein Namespace umfasst immer den ganzen Teilbaum.
    /// </remarks>
    public IReadOnlyList<Type>? GetCommandTypes(string? scope)
    {
        Type[] all = this.assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false }
                           && this.markerInterface.IsAssignableFrom(type))
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .ToArray();

        if (string.IsNullOrWhiteSpace(scope))
        {
            return all;
        }

        string value = scope.Trim();

        Type? single = all.FirstOrDefault(type =>
            string.Equals(type.FullName, value, StringComparison.Ordinal)
            || string.Equals(type.Name, value, StringComparison.Ordinal));

        if (single is not null)
        {
            return [single];
        }

        string prefix = value + ".";

        Type[] matches = all
            .Where(type => string.Equals(type.Namespace, value, StringComparison.Ordinal)
                           || (type.Namespace?.StartsWith(prefix, StringComparison.Ordinal) ?? false))
            .ToArray();

        return matches.Length == 0 ? null : matches;
    }

    /// <summary>
    /// Liefert die Commands als Namespace-Hierarchie.
    /// </summary>
    /// <remarks>
    /// Die Hierarchie entspricht der Namespace-Struktur der Commands, damit die UI sie wie
    /// einen Projektbaum in einer IDE darstellen kann.
    /// </remarks>
    public IReadOnlyList<NamespaceNode> GetNamespaceTree()
    {
        NamespaceNode root = new() { Name = string.Empty, FullName = string.Empty };

        foreach (CommandDescriptor command in this.GetCommands())
        {
            root = Insert(root, command.NamespaceSegments, command);
        }

        return root.Children
            .Select(node => this.ApplyPrefix(node))
            .ToArray();
    }

    /// <summary>
    /// Ergänzt an einem Wurzelknoten und allen Nachfahren den vollen Namespace.
    /// </summary>
    /// <param name="node">Der Wurzelknoten des Teilbaums.</param>
    /// <returns>Der Teilbaum mit gesetztem <see cref="NamespaceNode.Namespace"/>.</returns>
    private NamespaceNode ApplyPrefix(NamespaceNode node)
    {
        string? prefix = this.ResolveTrimmedPrefix(node);

        return WithNamespace(node, prefix, isRoot: true);
    }

    private static NamespaceNode WithNamespace(NamespaceNode node, string? prefix, bool isRoot)
    {
        string full = string.IsNullOrEmpty(prefix)
            ? node.FullName
            : string.IsNullOrEmpty(node.FullName)
                ? prefix
                : $"{prefix}.{node.FullName}";

        return node with
        {
            Namespace = full,
            TrimmedPrefix = isRoot ? prefix : node.TrimmedPrefix,
            Children = node.Children
                .Select(child => WithNamespace(child, prefix, isRoot: false))
                .ToArray()
        };
    }

    /// <summary>
    /// Ermittelt den Prefix, der bei einem Wurzelknoten abgeschnitten wurde.
    /// </summary>
    /// <remarks>
    /// Der Prefix wird aus dem Namespace des ersten Commands im Teilbaum abgeleitet.
    /// Gibt es keinen passenden Prefix, bleibt das Feld leer.
    /// </remarks>
    private string? ResolveTrimmedPrefix(NamespaceNode node)
    {
        CommandDescriptor? command = FindFirstCommand(node);
        if (command is null || string.IsNullOrWhiteSpace(command.Namespace))
        {
            return null;
        }

        foreach (string prefix in this.namespacePrefixes)
        {
            if (string.Equals(command.Namespace, prefix, StringComparison.Ordinal)
                || command.Namespace.StartsWith(prefix + ".", StringComparison.Ordinal))
            {
                return prefix;
            }
        }

        return null;
    }

    /// <summary>Liefert den ersten Command eines Teilbaums (Tiefensuche).</summary>
    private static CommandDescriptor? FindFirstCommand(NamespaceNode node)
    {
        if (node.Commands.Count > 0)
        {
            return node.Commands[0];
        }

        foreach (NamespaceNode child in node.Children)
        {
            CommandDescriptor? found = FindFirstCommand(child);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static NamespaceNode Insert(
        NamespaceNode node,
        IReadOnlyList<string> segments,
        CommandDescriptor command)
    {
        if (segments.Count == 0)
        {
            return Rebuild(node, node.Commands.Append(command).ToArray(), node.Children);
        }

        string segment = segments[0];
        string fullName = string.IsNullOrEmpty(node.FullName) ? segment : $"{node.FullName}.{segment}";
        IReadOnlyList<string> rest = segments.Skip(1).ToArray();

        NamespaceNode? existing = node.Children.FirstOrDefault(child => child.Name == segment);
        NamespaceNode child = existing ?? new NamespaceNode { Name = segment, FullName = fullName };
        NamespaceNode updatedChild = Insert(child, rest, command);

        NamespaceNode[] children = node.Children
            .Where(candidate => candidate.Name != segment)
            .Append(updatedChild)
            .OrderBy(candidate => candidate.Name, StringComparer.Ordinal)
            .ToArray();

        return Rebuild(node, node.Commands, children);
    }

    private static NamespaceNode Rebuild(
        NamespaceNode node,
        IReadOnlyList<CommandDescriptor> commands,
        IReadOnlyList<NamespaceNode> children) =>
        new()
        {
            Name = node.Name,
            FullName = node.FullName,
            Namespace = node.Namespace,
            Commands = commands.OrderBy(command => command.Name, StringComparer.Ordinal).ToArray(),
            Children = children,
            TotalCommandCount = commands.Count + children.Sum(child => child.TotalCommandCount)
        };

    private CommandDescriptor Describe(Type commandType)
    {
        Type resultType = ResolveResultType(commandType);

        CommandDescriptor descriptor = new()
        {
            Name = commandType.Name,
            FullName = commandType.FullName ?? commandType.Name,
            TypeName = $"{commandType.FullName}, {commandType.Assembly.GetName().Name}",
            Namespace = commandType.Namespace ?? string.Empty,
            NamespaceSegments = this.SplitNamespace(commandType.Namespace),
            Summary = this.documentation.GetTypeSummary(commandType),
            Remarks = this.documentation.GetTypeRemarks(commandType),
            ResultType = FormatTypeName(resultType),
            Authorization = this.DescribeAuthorization(commandType),
            Properties = this.DescribeProperties(commandType, 0),
            SampleJson = this.BuildSampleJson(commandType)
        };

        return descriptor with
        {
            JsonSchema = this.schemaBuilder.Build(descriptor).ToJsonString(this.jsonOptions),
            ResultJsonSchema = this.BuildResultSchema(resultType)
        };
    }

    /// <summary>
    /// Erzeugt das JSON-Schema für den Ergebnistyp eines Commands.
    /// </summary>
    /// <remarks>
    /// Für primitive Ergebnistypen wie <c>bool</c> entsteht ein Schema ohne Objektstruktur.
    /// Der Explorer kann damit trotzdem prüfen, ob der Antworttyp stimmt.
    /// </remarks>
    private string BuildResultSchema(Type resultType)
    {
        CommandPropertyDescriptor result = this.DescribeProperty(resultType, "result", 0);

        return this.schemaBuilder
            .BuildResult(result, FormatTypeName(resultType), this.documentation.GetTypeSummary(resultType))
            .ToJsonString(this.jsonOptions);
    }

    /// <summary>
    /// Beschreibt einen Typ ohne zugehörige <see cref="PropertyInfo"/>.
    /// </summary>
    private CommandPropertyDescriptor DescribeProperty(Type type, string name, int depth)
    {
        Type underlyingType = Nullable.GetUnderlyingType(type) ?? type;
        CommandPropertyKind kind = ClassifyType(underlyingType);

        return new CommandPropertyDescriptor
        {
            Name = name,
            JsonName = name,
            TypeName = FormatTypeName(type),
            FullTypeName = type.FullName ?? type.Name,
            Kind = kind,
            Summary = this.documentation.GetTypeSummary(type),
            CanWrite = true,
            IsValueType = type.IsValueType,
            IsNullable = Nullable.GetUnderlyingType(type) is not null || !type.IsValueType,
            EnumValues = kind == CommandPropertyKind.Enum ? this.DescribeEnum(underlyingType) : [],
            ElementType = kind == CommandPropertyKind.Array
                ? this.DescribeElementType(underlyingType, depth)
                : null,
            Properties = kind == CommandPropertyKind.Object
                ? this.DescribeProperties(underlyingType, depth + 1)
                : []
        };
    }

    private static Type ResolveResultType(Type commandType)
    {
        Type? commandInterface = commandType
            .GetInterfaces()
            .FirstOrDefault(type => type.IsGenericType
                                    && type.GetGenericTypeDefinition() == typeof(ICommand<>));

        return commandInterface?.GetGenericArguments()[0] ?? typeof(object);
    }

    /// <summary>
    /// Zerlegt einen Namespace in seine Segmente.
    /// </summary>
    /// <remarks>
    /// Konfigurierte Prefixe werden vorher abgeschnitten. Bleibt dadurch nichts übrig,
    /// landet der Command in der Wurzel des Baums.
    /// </remarks>
    private string[] SplitNamespace(string? @namespace)
    {
        if (string.IsNullOrWhiteSpace(@namespace))
        {
            return [];
        }

        string trimmed = this.TrimNamespacePrefix(@namespace);
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return [];
        }

        return trimmed.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>
    /// Entfernt den längsten passenden konfigurierten Prefix von einem Namespace.
    /// </summary>
    /// <param name="namespace">Der vollqualifizierte Namespace.</param>
    /// <returns>Der verbleibende Rest oder eine leere Zeichenkette.</returns>
    private string TrimNamespacePrefix(string @namespace)
    {
        foreach (string prefix in this.namespacePrefixes)
        {
            if (string.Equals(@namespace, prefix, StringComparison.Ordinal))
            {
                return string.Empty;
            }

            if (@namespace.StartsWith(prefix + ".", StringComparison.Ordinal))
            {
                return @namespace[(prefix.Length + 1)..];
            }
        }

        return @namespace;
    }

    private CommandAuthorizationInfo DescribeAuthorization(Type commandType)
    {
        bool allowAnonymous = commandType.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true) is not null;
        AuthorizeAttribute? authorize = commandType.GetCustomAttribute<AuthorizeAttribute>(inherit: true);

        CommandClaimRequirement[] claims = commandType
            .GetCustomAttributes<ClaimRequirementAttribute>(inherit: true)
            .Select(ParseClaim)
            .ToArray();

        string[] roles = SplitValues(authorize?.Roles);
        string[] policies = SplitValues(authorize?.Policy);

        bool requiresAuthentication = !allowAnonymous
                                      && (authorize is not null || claims.Length > 0);

        return new CommandAuthorizationInfo
        {
            AllowAnonymous = allowAnonymous,
            RequiresAuthentication = requiresAuthentication,
            Roles = roles,
            Policies = policies,
            Claims = claims,
            Description = BuildAuthorizationDescription(allowAnonymous, roles, policies, claims)
        };
    }

    private static string[] SplitValues(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// Liest eine Claim-Anforderung aus einem <see cref="ClaimRequirementAttribute"/>.
    /// </summary>
    /// <remarks>
    /// Das Attribut leitet von <c>TypeFilterAttribute</c> ab und speichert seine
    /// Konstruktorargumente in <c>Arguments</c>. Beide Werte landen dort in einem
    /// einzigen String im Format <c>"claimType: claimValue"</c>.
    /// </remarks>
    private static CommandClaimRequirement ParseClaim(ClaimRequirementAttribute attribute)
    {
        object[]? arguments = attribute.Arguments;
        string raw = arguments is { Length: > 0 }
            ? Convert.ToString(arguments[0], System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty
            : string.Empty;

        int separator = raw.IndexOf(':', StringComparison.Ordinal);
        if (separator < 0)
        {
            return new CommandClaimRequirement { ClaimType = raw.Trim(), ClaimValue = string.Empty };
        }

        return new CommandClaimRequirement
        {
            ClaimType = raw[..separator].Trim(),
            ClaimValue = raw[(separator + 1)..].Trim()
        };
    }

    private static string BuildAuthorizationDescription(
        bool allowAnonymous,
        IReadOnlyList<string> roles,
        IReadOnlyList<string> policies,
        IReadOnlyList<CommandClaimRequirement> claims)
    {
        if (allowAnonymous)
        {
            return "Anonym – keine Anmeldung erforderlich.";
        }

        List<string> parts = [];
        if (roles.Count > 0)
        {
            parts.Add($"Rollen: {string.Join(", ", roles)}");
        }

        if (policies.Count > 0)
        {
            parts.Add($"Policies: {string.Join(", ", policies)}");
        }

        if (claims.Count > 0)
        {
            parts.Add($"Claims: {string.Join(", ", claims.Select(claim => $"{claim.ClaimType}={claim.ClaimValue}"))}");
        }

        return parts.Count == 0
            ? "Keine Autorisierung definiert."
            : string.Join(" | ", parts);
    }

    private IReadOnlyList<CommandPropertyDescriptor> DescribeProperties(Type type, int depth)
    {
        if (depth > MaxDepth)
        {
            return [];
        }

        List<CommandPropertyDescriptor> properties = [];

        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            properties.Add(this.DescribeProperty(property, depth));
        }

        return properties;
    }

    private CommandPropertyDescriptor DescribeProperty(PropertyInfo property, int depth)
    {
        Type propertyType = property.PropertyType;
        Type underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        CommandPropertyKind kind = ClassifyType(underlyingType);

        return new CommandPropertyDescriptor
        {
            Name = property.Name,
            JsonName = JsonNamingPolicy.CamelCase.ConvertName(property.Name),
            TypeName = FormatTypeName(propertyType),
            FullTypeName = propertyType.FullName ?? propertyType.Name,
            Kind = kind,
            Summary = this.documentation.GetPropertySummary(property),
            CanWrite = property.CanWrite,
            IsValueType = propertyType.IsValueType,
            IsNullable = Nullable.GetUnderlyingType(propertyType) is not null
                         || !propertyType.IsValueType,
            DefaultValue = FormatDefaultValue(property),
            EnumValues = kind == CommandPropertyKind.Enum ? this.DescribeEnum(underlyingType) : [],
            ElementType = kind == CommandPropertyKind.Array
                ? this.DescribeElementType(underlyingType, depth)
                : null,
            Properties = kind == CommandPropertyKind.Object
                ? this.DescribeProperties(underlyingType, depth + 1)
                : []
        };
    }

    private CommandPropertyDescriptor? DescribeElementType(Type collectionType, int depth)
    {
        Type? elementType = ResolveElementType(collectionType);
        if (elementType is null || depth > MaxDepth)
        {
            return null;
        }

        Type underlying = Nullable.GetUnderlyingType(elementType) ?? elementType;
        CommandPropertyKind kind = ClassifyType(underlying);

        return new CommandPropertyDescriptor
        {
            Name = "item",
            JsonName = "item",
            TypeName = FormatTypeName(elementType),
            FullTypeName = elementType.FullName ?? elementType.Name,
            Kind = kind,
            CanWrite = true,
            IsValueType = elementType.IsValueType,
            IsNullable = Nullable.GetUnderlyingType(elementType) is not null || !elementType.IsValueType,
            EnumValues = kind == CommandPropertyKind.Enum ? this.DescribeEnum(underlying) : [],
            Properties = kind == CommandPropertyKind.Object
                ? this.DescribeProperties(underlying, depth + 1)
                : []
        };
    }

    private IReadOnlyList<CommandEnumValue> DescribeEnum(Type enumType) =>
        Enum.GetNames(enumType)
            .Select(name =>
            {
                FieldInfo field = enumType.GetField(name)!;
                object? rawValue = field.GetRawConstantValue();

                return new CommandEnumValue
                {
                    Name = name,
                    Value = Convert.ToString(rawValue, System.Globalization.CultureInfo.InvariantCulture) ?? name,
                    Summary = this.documentation.GetFieldSummary(field)
                };
            })
            .ToArray();

    private static CommandPropertyKind ClassifyType(Type type)
    {
        if (type == typeof(string) || type == typeof(char))
        {
            return CommandPropertyKind.String;
        }

        if (type == typeof(bool))
        {
            return CommandPropertyKind.Boolean;
        }

        if (type == typeof(byte) || type == typeof(sbyte)
            || type == typeof(short) || type == typeof(ushort)
            || type == typeof(int) || type == typeof(uint)
            || type == typeof(long) || type == typeof(ulong))
        {
            return CommandPropertyKind.Integer;
        }

        if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
        {
            return CommandPropertyKind.Number;
        }

        if (type == typeof(DateTime) || type == typeof(DateTimeOffset))
        {
            return CommandPropertyKind.DateTime;
        }

        if (type == typeof(DateOnly))
        {
            return CommandPropertyKind.Date;
        }

        if (type == typeof(TimeOnly) || type == typeof(TimeSpan))
        {
            return CommandPropertyKind.TimeSpan;
        }

        if (type == typeof(Guid))
        {
            return CommandPropertyKind.Guid;
        }

        if (type == typeof(byte[]))
        {
            return CommandPropertyKind.Binary;
        }

        if (type.IsEnum)
        {
            return CommandPropertyKind.Enum;
        }

        if (typeof(IDictionary).IsAssignableFrom(type)
            || type.GetInterfaces().Any(IsGenericDictionary))
        {
            return CommandPropertyKind.Dictionary;
        }

        if (type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type))
        {
            return CommandPropertyKind.Array;
        }

        if (type.IsClass || (type.IsValueType && !type.IsPrimitive && !type.IsEnum))
        {
            return CommandPropertyKind.Object;
        }

        return CommandPropertyKind.Unknown;
    }

    private static bool IsGenericDictionary(Type type) =>
        type.IsGenericType
        && (type.GetGenericTypeDefinition() == typeof(IDictionary<,>)
            || type.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>));

    private static Type? ResolveElementType(Type collectionType)
    {
        if (collectionType.IsArray)
        {
            return collectionType.GetElementType();
        }

        Type? enumerable = collectionType
            .GetInterfaces()
            .Append(collectionType)
            .FirstOrDefault(type => type.IsGenericType
                                    && type.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        return enumerable?.GetGenericArguments()[0];
    }

    private static string FormatTypeName(Type type)
    {
        Type underlying = Nullable.GetUnderlyingType(type) ?? type;

        if (underlying.IsArray)
        {
            return $"{FormatTypeName(underlying.GetElementType()!)}[]";
        }

        if (!underlying.IsGenericType)
        {
            return GetAlias(underlying);
        }

        string name = underlying.Name;
        int tickIndex = name.IndexOf('`', StringComparison.Ordinal);
        if (tickIndex > 0)
        {
            name = name[..tickIndex];
        }

        string arguments = string.Join(", ", underlying.GetGenericArguments().Select(FormatTypeName));
        return $"{name}<{arguments}>";
    }

    private static string GetAlias(Type type) =>
        type.FullName switch
        {
            "System.String" => "string",
            "System.Char" => "char",
            "System.Boolean" => "bool",
            "System.Byte" => "byte",
            "System.SByte" => "sbyte",
            "System.Int16" => "short",
            "System.UInt16" => "ushort",
            "System.Int32" => "int",
            "System.UInt32" => "uint",
            "System.Int64" => "long",
            "System.UInt64" => "ulong",
            "System.Single" => "float",
            "System.Double" => "double",
            "System.Decimal" => "decimal",
            "System.Object" => "object",
            "System.DateTime" => "DateTime",
            "System.DateTimeOffset" => "DateTimeOffset",
            "System.DateOnly" => "DateOnly",
            "System.TimeOnly" => "TimeOnly",
            "System.TimeSpan" => "TimeSpan",
            "System.Guid" => "Guid",
            _ => type.Name
        };

    private static string? FormatDefaultValue(PropertyInfo property)
    {
        DefaultValueAttribute? defaultValue = property.GetCustomAttribute<DefaultValueAttribute>();
        if (defaultValue?.Value is not null)
        {
            return Convert.ToString(defaultValue.Value, System.Globalization.CultureInfo.InvariantCulture);
        }

        if (!property.PropertyType.IsValueType || Nullable.GetUnderlyingType(property.PropertyType) is not null)
        {
            return null;
        }

        try
        {
            object? instance = RuntimeHelpers.GetUninitializedObject(property.DeclaringType!);
            object? value = property.GetValue(instance);

            // "Leere" Defaults sind für ein Formular wertlos und würden nur Rauschen erzeugen.
            return value switch
            {
                null => null,
                DateTime dateTime when dateTime == default => null,
                DateOnly dateOnly when dateOnly == default => null,
                TimeOnly timeOnly when timeOnly == default => null,
                TimeSpan timeSpan when timeSpan == default => null,
                Guid guid when guid == Guid.Empty => null,
                _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)
            };
        }
        catch (Exception)
        {
            return null;
        }
    }

    private string BuildSampleJson(Type commandType)
    {
        try
        {
            object instance = RuntimeHelpers.GetUninitializedObject(commandType);
            JsonNode? node = JsonSerializer.SerializeToNode(instance, commandType, this.jsonOptions);

            if (node is JsonObject jsonObject)
            {
                jsonObject.Insert(0, "$type", JsonValue.Create($"{commandType.FullName}, {commandType.Assembly.GetName().Name}"));
            }

            return node?.ToJsonString(this.jsonOptions) ?? "{}";
        }
        catch (Exception)
        {
            return $$"""
                     {
                       "$type": "{{commandType.FullName}}, {{commandType.Assembly.GetName().Name}}"
                     }
                     """;
        }
    }
}
