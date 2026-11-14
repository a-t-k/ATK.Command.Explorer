using System.Text;
using ATK.Command.Explorer.Models;

namespace ATK.Command.Explorer.Documentation;

/// <summary>
/// Erzeugt aus den Command-Metadaten einen Obsidian-Vault aus Markdown-Dateien.
/// </summary>
/// <remarks>
/// Der Vault ist so aufgebaut, dass er direkt in Obsidian geöffnet werden kann:
/// eine Startseite mit Wikilinks, eine Notiz pro Command und eine Notiz pro Namespace.
/// </remarks>
public sealed class MarkdownVaultGenerator
{
    /// <summary>
    /// Erzeugt alle Dateien des Vaults.
    /// </summary>
    /// <param name="commands">Die zu dokumentierenden Commands.</param>
    /// <param name="title">Titel der Dokumentation, z. B. der Anwendungsname.</param>
    /// <returns>Dateiname → Inhalt. Die Namen sind relativ und enthalten Unterordner.</returns>
    public IReadOnlyDictionary<string, string> Generate(
        IReadOnlyList<CommandDescriptor> commands,
        string title)
    {
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        Dictionary<string, string> files = new(StringComparer.OrdinalIgnoreCase);
        var tree = BuildTree(commands);

        files["00 Start.md"] = this.BuildIndex(commands, tree, title);

        foreach (var group in commands
                     .GroupBy(command => command.Namespace)
                     .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            files[$"Namespaces/{SanitizeFileName(group.Key)}.md"] =
                this.BuildNamespaceNote(group.Key, [.. group], tree);
        }

        foreach (var command in commands.OrderBy(c => c.Name, StringComparer.Ordinal))
        {
            files[$"Commands/{SanitizeFileName(command.Name)}.md"] = this.BuildCommandNote(command);
        }

        files["Glossar.md"] = this.BuildGlossary();

        return files;
    }

    /// <summary>
    /// Baut aus den Namespaces einen Baum.
    /// </summary>
    /// <param name="commands">Die Commands.</param>
    /// <remarks>
    /// Verwendet bewusst <see cref="CommandDescriptor.Namespace"/> und nicht
    /// <see cref="CommandDescriptor.NamespaceSegments"/>: die Segmente sind um die
    /// konfigurierten Prefixe gekürzt, was in der UI gewollt ist, in einem Dokument aber
    /// den Zusammenhang verschleiert. Der Ketten-Kollaps macht den vollen Pfad trotzdem
    /// kompakt.
    /// </remarks>
    internal static IReadOnlyList<DocNode> BuildTree(IReadOnlyList<CommandDescriptor> commands)
    {
        DocNode root = new();

        foreach (var command in commands)
        {
            var segments = SplitNamespace(command.Namespace);

            var current = root;

            foreach (var segment in segments)
            {
                var child = current.Children.FirstOrDefault(c => c.Name == segment);

                if (child is null)
                {
                    child = new DocNode
                    {
                        Name = segment,
                        FullName = string.IsNullOrEmpty(current.FullName)
                            ? segment
                            : $"{current.FullName}.{segment}",
                    };

                    current.Children.Add(child);
                }

                current = child;
            }

            current.Commands.Add(command);
        }

        Sort(root);
        return root.Children;
    }

    private static void Sort(DocNode node)
    {
        node.Children.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));
        node.Commands.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));

        foreach (var child in node.Children)
        {
            Sort(child);
        }
    }

    private static IReadOnlyList<string> SplitNamespace(string @namespace) =>
        string.IsNullOrWhiteSpace(@namespace)
            ? []
            : @namespace.Split('.', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// Faltet Ketten aus Namespaces ohne eigene Commands zu einem Namen zusammen.
    /// </summary>
    /// <remarks>
    /// Aus <c>ATK</c> → <c>Command</c> → <c>Ui</c> → <c>WebApi</c> → <c>Modules</c> wird
    /// <c>ATK.Command.Ui.WebApi.Modules</c>. Das hält das Inhaltsverzeichnis lesbar, ohne die
    /// Hierarchie zu verlieren.
    /// </remarks>
    private static (string Name, DocNode Target) Collapse(DocNode node)
    {
        StringBuilder name = new(node.Name);
        var current = node;

        while (current.Commands.Count == 0 && current.Children.Count == 1)
        {
            current = current.Children[0];
            name.Append('.').Append(current.Name);
        }

        return (name.ToString(), current);
    }

    private void AppendTree(StringBuilder builder, DocNode node, int depth)
    {
        string indent = new(' ', depth * 2);
        (var name, var target) = Collapse(node);

        builder.AppendLine($"{indent}- 📁 **{name}** ({target.TotalCount})");

        foreach (var command in target.Commands)
        {
            builder.AppendLine(
                $"{indent}  - [[{SanitizeFileName(command.Name)}]]"
                + $" – {EscapeCell(command.Authorization.Description)}");
        }

        foreach (var child in target.Children)
        {
            this.AppendTree(builder, child, depth + 1);
        }
    }

    private string BuildIndex(
        IReadOnlyList<CommandDescriptor> commands,
        IReadOnlyList<DocNode> tree,
        string title)
    {
        StringBuilder builder = new();

        builder.AppendLine("---");
        builder.AppendLine("tags:");
        builder.AppendLine("  - moc");
        builder.AppendLine("---");
        builder.AppendLine();
        builder.AppendLine($"# {title}");
        builder.AppendLine();
        builder.AppendLine(
            $"Automatisch erzeugte Command-Referenz, Stand {DateTimeOffset.Now:yyyy-MM-dd HH:mm}, "
            + $"{commands.Count} Commands.");
        builder.AppendLine();
        builder.AppendLine("---");
        builder.AppendLine();
        builder.AppendLine("## Inhaltsverzeichnis");
        builder.AppendLine();
        builder.AppendLine("Namespace-Baum wie im Explorer. Ein Klick auf einen Command öffnet");
        builder.AppendLine("seine Notiz.");
        builder.AppendLine();

        foreach (var node in tree)
        {
            this.AppendTree(builder, node, 0);
        }

        builder.AppendLine();
        builder.AppendLine("## Alle Commands");
        builder.AppendLine();
        builder.AppendLine("| Command | Namespace | Response Type | Autorisierung |");
        builder.AppendLine("| --- | --- | --- | --- |");

        foreach (var command in commands.OrderBy(c => c.Name, StringComparer.Ordinal))
        {
            builder.AppendLine(
                $"| [[{SanitizeFileName(command.Name)}]] "
                + $"| `{command.Namespace}` "
                + $"| `{command.ResultType}` "
                + $"| {EscapeCell(command.Authorization.Description)} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Aufbau dieses Vaults");
        builder.AppendLine();
        builder.AppendLine("```");
        builder.AppendLine("00 Start.md          diese Seite");
        builder.AppendLine("Glossar.md           Begriffe");
        builder.AppendLine("Namespaces/          eine Notiz pro Namespace");
        builder.AppendLine("Commands/            eine Notiz pro Command");
        builder.AppendLine("```");
        builder.AppendLine();
        builder.AppendLine("> [!tip] Obsidian");
        builder.AppendLine(
            "> Diesen Ordner in Obsidian als Vault öffnen. Die Wikilinks funktionieren dann,");
        builder.AppendLine("> ebenso Graph-Ansicht und Suche.");

        return builder.ToString();
    }

    private string BuildNamespaceNote(
        string @namespace,
        IReadOnlyList<CommandDescriptor> commands,
        IReadOnlyList<DocNode> tree)
    {
        StringBuilder builder = new();

        builder.AppendLine("---");
        builder.AppendLine("tags:");
        builder.AppendLine("  - namespace");
        builder.AppendLine("---");
        builder.AppendLine();
        builder.AppendLine($"# {@namespace}");
        builder.AppendLine();
        builder.AppendLine($"Enthält {commands.Count} Commands.");
        builder.AppendLine();

        var node = FindNode(tree, @namespace);

        if (node is not null && node.Children.Count > 0)
        {
            builder.AppendLine("## Untergeordnete Namespaces");
            builder.AppendLine();

            foreach (var child in node.Children)
            {
                builder.AppendLine(
                    $"- [[{SanitizeFileName(child.FullName)}]] – {child.TotalCount} Commands");
            }

            builder.AppendLine();
        }

        builder.AppendLine("## Commands");
        builder.AppendLine();
        builder.AppendLine("| Command | Response Type | Autorisierung |");
        builder.AppendLine("| --- | --- | --- |");

        foreach (var command in commands.OrderBy(c => c.Name, StringComparer.Ordinal))
        {
            builder.AppendLine(
                $"| [[{SanitizeFileName(command.Name)}]] "
                + $"| `{command.ResultType}` "
                + $"| {EscapeCell(command.Authorization.Description)} |");
        }

        builder.AppendLine();
        builder.AppendLine("Zurück zur [[00 Start]].");

        return builder.ToString();
    }

    private static DocNode? FindNode(IReadOnlyList<DocNode> nodes, string fullName)
    {
        foreach (var node in nodes)
        {
            if (string.Equals(node.FullName, fullName, StringComparison.Ordinal))
            {
                return node;
            }

            var found = FindNode(node.Children, fullName);

            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private string BuildCommandNote(CommandDescriptor command)
    {
        StringBuilder builder = new();

        builder.AppendLine("---");
        builder.AppendLine("tags:");
        builder.AppendLine("  - command");
        builder.AppendLine($"namespace: {@command.Namespace}");
        builder.AppendLine($"result: {command.ResultType}");
        builder.AppendLine("---");
        builder.AppendLine();
        builder.AppendLine($"# {command.Name}");
        builder.AppendLine();

        if (!string.IsNullOrWhiteSpace(command.Summary))
        {
            builder.AppendLine(command.Summary);
            builder.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(command.Remarks))
        {
            builder.AppendLine("> [!note] Hinweise");
            foreach (var line in command.Remarks.Split('\n'))
            {
                builder.AppendLine($"> {line.TrimEnd()}");
            }

            builder.AppendLine();
        }

        builder.AppendLine("## Basisinformationen");
        builder.AppendLine();
        builder.AppendLine("| Merkmal | Wert |");
        builder.AppendLine("| --- | --- |");
        builder.AppendLine($"| Namespace | `{command.Namespace}` |");
        builder.AppendLine($"| Response Type | `{command.ResultType}` |");
        builder.AppendLine($"| Autorisierung | {EscapeCell(command.Authorization.Description)} |");
        builder.AppendLine($"| `$type` | `{command.TypeName}` |");
        builder.AppendLine();

        this.AppendProperties(builder, command.Properties);

        builder.AppendLine("## Beispiel-Request");
        builder.AppendLine();
        builder.AppendLine("```json");
        builder.AppendLine(command.SampleJson);
        builder.AppendLine("```");
        builder.AppendLine();
        builder.AppendLine("Gesendet an `POST /commands` mit `Content-Type: application/json`.");
        builder.AppendLine();
        builder.AppendLine("## JSON-Schema (Request)");
        builder.AppendLine();
        builder.AppendLine("```json");
        builder.AppendLine(command.JsonSchema);
        builder.AppendLine("```");
        builder.AppendLine();
        builder.AppendLine("## JSON-Schema (Response)");
        builder.AppendLine();
        builder.AppendLine("```json");
        builder.AppendLine(command.ResultJsonSchema);
        builder.AppendLine("```");
        builder.AppendLine();
        builder.AppendLine($"Zurück zu [[{SanitizeFileName(command.Namespace)}]] · [[00 Start]]");

        return builder.ToString();
    }

    private void AppendProperties(
        StringBuilder builder,
        IReadOnlyList<CommandPropertyDescriptor> properties,
        int depth = 0)
    {
        if (properties.Count == 0)
        {
            return;
        }

        if (depth == 0)
        {
            builder.AppendLine("## Properties");
            builder.AppendLine();
            builder.AppendLine("| Name | Typ | Pflicht | Beschreibung |");
            builder.AppendLine("| --- | --- | --- | --- |");
        }

        foreach (var property in properties)
        {
            var required = property.IsValueType && !property.IsNullable ? "ja" : "nein";
            var description = EscapeCell(property.Summary ?? string.Empty);

            if (property.EnumValues.Count > 0)
            {
                var values = string.Join(
                    ", ",
                    property.EnumValues.Select(value => $"{value.Name} = {value.Value}"));
                description = string.IsNullOrEmpty(description)
                    ? $"Werte: {values}"
                    : $"{description}<br>Werte: {values}";
            }

            // Markdown-Editoren trimmen führende Leerzeichen in Tabellenzellen,
            // deshalb wird die Einrückung mit geschützten Leerzeichen erzeugt.
            var indent = depth == 0
                ? string.Empty
                : string.Concat(Enumerable.Repeat("&nbsp;&nbsp;&nbsp;", depth)) + "- ";

            builder.AppendLine(
                $"| {indent}`{property.JsonName}` | `{property.TypeName}` | {required} | {description} |");

            if (property.Properties.Count > 0)
            {
                this.AppendProperties(builder, property.Properties, depth + 1);
            }
        }

        if (depth == 0)
        {
            builder.AppendLine();
        }
    }

    private string BuildGlossary()
    {
        StringBuilder builder = new();

        builder.AppendLine("---");
        builder.AppendLine("tags:");
        builder.AppendLine("  - glossar");
        builder.AppendLine("---");
        builder.AppendLine();
        builder.AppendLine("# Glossar");
        builder.AppendLine();
        builder.AppendLine("**Command** – Klasse, die `ICommand<TResult>` und das Marker-Interface");
        builder.AppendLine("implementiert. Wird über `POST /commands` ausgeführt.");
        builder.AppendLine();
        builder.AppendLine("**`$type`** – Feld im Request-Body. Enthält");
        builder.AppendLine("`\"<Vollqualifizierter Typname>, <Assembly-Name>\"` und bestimmt, welcher");
        builder.AppendLine("Command-Typ instanziiert wird.");
        builder.AppendLine();
        builder.AppendLine("**Handler** – Klasse, die `ICommandHandler<TCommand, TResult>`");
        builder.AppendLine("implementiert und die Fachlogik enthält.");
        builder.AppendLine();
        builder.AppendLine("**JSON-Schema** – Strukturbeschreibung des Request- oder Response-Bodys");
        builder.AppendLine("nach Draft 2020-12.");
        builder.AppendLine();
        builder.AppendLine("Zurück zur [[00 Start]].");

        return builder.ToString();
    }

    /// <summary>
    /// Entfernt Zeichen, die in Dateinamen unzulässig sind.
    /// </summary>
    /// <param name="name">Der gewünschte Dateiname ohne Endung.</param>
    internal static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        StringBuilder builder = new(name.Length);

        foreach (var character in name)
        {
            builder.Append(invalid.Contains(character) ? '_' : character);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Maskiert Pipe-Zeichen, damit Tabellenzellen nicht zerbrechen.
    /// </summary>
    private static string EscapeCell(string value) =>
        value.Replace("|", "\\|", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
}
