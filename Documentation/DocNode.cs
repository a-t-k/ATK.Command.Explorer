using ATK.Command.Explorer.Models;

namespace ATK.Command.Explorer.Documentation;

/// <summary>
/// Knoten im Namespace-Baum der Dokumentation.
/// </summary>
/// <remarks>
/// Bewusst ein eigener Typ und nicht <see cref="NamespaceNode"/>: der Dokumentations-Baum
/// wird aus den Commands neu aufgebaut und braucht keine abgeschnittenen Prefixe.
/// </remarks>
internal sealed class DocNode
{
    /// <summary>Name des Namespace-Segments.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Vollqualifizierter Namespace bis zu diesem Knoten.</summary>
    public string FullName { get; init; } = string.Empty;

    /// <summary>Commands, die direkt in diesem Namespace liegen.</summary>
    public List<CommandDescriptor> Commands { get; } = [];

    /// <summary>Untergeordnete Namespaces.</summary>
    public List<DocNode> Children { get; } = [];

    /// <summary>Anzahl der Commands inklusive aller Nachfahren.</summary>
    public int TotalCount => this.Commands.Count + this.Children.Sum(child => child.TotalCount);
}
