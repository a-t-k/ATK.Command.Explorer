namespace ATK.Command.Explorer.Models;

/// <summary>
/// Beschreibt einen einzelnen Command, der in der WebApi implementiert ist.
/// </summary>
public sealed record CommandDescriptor
{
    /// <summary>Name der Command-Klasse.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Vollqualifizierter Typname inklusive Namespace.</summary>
    public string FullName { get; init; } = string.Empty;

    /// <summary>Wert für das <c>$type</c>-Feld im Request-Body.</summary>
    public string TypeName { get; init; } = string.Empty;

    /// <summary>Namespace der Command-Klasse.</summary>
    public string Namespace { get; init; } = string.Empty;

    /// <summary>
    /// Namespace in einzelne Segmente zerlegt, z. B. <c>["ATK", "Command", "Ui", "WebApi", "Modules"]</c>.
    /// </summary>
    public IReadOnlyList<string> NamespaceSegments { get; init; } = [];

    /// <summary>XML-Summary des Commands, falls dokumentiert.</summary>
    public string? Summary { get; init; }

    /// <summary>XML-Remarks des Commands, falls dokumentiert.</summary>
    public string? Remarks { get; init; }

    /// <summary>Typ des Ergebnisses, das der Handler zurückgibt.</summary>
    public string ResultType { get; init; } = string.Empty;

    /// <summary>Autorisierungsinformationen des Commands.</summary>
    public CommandAuthorizationInfo Authorization { get; init; } = new();

    /// <summary>Properties des Commands in Deklarationsreihenfolge.</summary>
    public IReadOnlyList<CommandPropertyDescriptor> Properties { get; init; } = [];

    /// <summary>Beispiel-JSON, das direkt an <c>POST /commands</c> gesendet werden kann.</summary>
    public string SampleJson { get; init; } = "{}";

    /// <summary>
    /// JSON-Schema (Draft 2020-12) für den Request-Body dieses Commands.
    /// </summary>
    public string JsonSchema { get; init; } = "{}";

    /// <summary>
    /// JSON-Schema (Draft 2020-12) für das Ergebnis dieses Commands.
    /// </summary>
    /// <remarks>
    /// Wird im Explorer genutzt, um die Antwort der WebApi zu prüfen.
    /// </remarks>
    public string ResultJsonSchema { get; init; } = "{}";
}
