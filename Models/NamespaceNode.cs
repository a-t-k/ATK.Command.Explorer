namespace ATK.Command.Explorer.Models;

/// <summary>
/// Ein Knoten im Namespace-Baum des Command Explorers.
/// </summary>
/// <remarks>
/// Die Namespaces der Commands werden als Hierarchie abgebildet, damit die UI sie wie eine
/// IDE darstellen kann. Ein Knoten kann sowohl eigene Commands als auch Kind-Namespaces
/// enthalten.
/// </remarks>
public sealed record NamespaceNode
{
    /// <summary>Name des Namespace-Segments, z. B. <c>Modules</c>.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Vollqualifizierter Namespace bis zu diesem Knoten.</summary>
    public string FullName { get; init; } = string.Empty;

    /// <summary>
    /// Vollqualifizierter Namespace ohne Abschneiden der konfigurierten Prefixe.
    /// </summary>
    /// <remarks>
    /// <see cref="FullName"/> ist um <c>NamespacePrefixes</c> gekürzt und taugt deshalb
    /// nicht als Schlüssel für die Dokumentations-Erzeugung. Dieser Wert enthält immer
    /// den echten .NET-Namespace.
    /// </remarks>
    public string Namespace { get; init; } = string.Empty;

    /// <summary>Commands, die direkt in diesem Namespace liegen.</summary>
    public IReadOnlyList<CommandDescriptor> Commands { get; init; } = [];

    /// <summary>Untergeordnete Namespaces, alphabetisch sortiert.</summary>
    public IReadOnlyList<NamespaceNode> Children { get; init; } = [];

    /// <summary>Anzahl der Commands in diesem Knoten inklusive aller Kind-Namespaces.</summary>
    public int TotalCommandCount { get; init; }

    /// <summary>Gibt an, ob dieser Knoten oder einer seiner Nachfahren Commands enthält.</summary>
    public bool HasCommands => this.TotalCommandCount > 0;

    /// <summary>
    /// Der Namespace-Prefix, der bei diesem Knoten abgeschnitten wurde.
    /// </summary>
    /// <remarks>
    /// Nur bei Wurzelknoten gesetzt. Die UI zeigt ihn in kleiner Schrift neben dem
    /// Namespace-Namen an, damit erkennbar bleibt, woher der Teilbaum stammt.
    /// </remarks>
    public string? TrimmedPrefix { get; init; }
}
