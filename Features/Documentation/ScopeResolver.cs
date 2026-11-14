using ATK.Command.Explorer.Models;

namespace ATK.Command.Explorer.Features.Documentation;

/// <summary>
/// Löst den <c>Scope</c>-Parameter der Dokumentations-Commands auf.
/// </summary>
/// <remarks>
/// Ein Namespace umfasst immer den ganzen Teilbaum – der Knoten selbst und alle
/// Kind-Namespaces. Ein Command-Name liefert genau diesen einen Command. Ohne Scope kommen
/// alle Commands.
/// </remarks>
internal static class ScopeResolver
{
    /// <summary>
    /// Ermittelt die Commands für einen Bereich.
    /// </summary>
    /// <param name="provider">Der Metadaten-Provider.</param>
    /// <param name="scope">Namespace oder Command-Name; leer bedeutet „alle“.</param>
    /// <returns>Die Commands im Bereich.</returns>
    /// <exception cref="InvalidOperationException">
    /// Wird geworfen, wenn der Bereich unbekannt ist.
    /// </exception>
    public static IReadOnlyList<CommandDescriptor> Resolve(
        Services.CommandMetadataProvider provider,
        string? scope)
    {
        var all = provider.GetCommands();

        if (string.IsNullOrWhiteSpace(scope))
        {
            return all;
        }

        var value = scope.Trim();

        var single = all.FirstOrDefault(
            command => string.Equals(command.FullName, value, StringComparison.Ordinal)
                       || string.Equals(command.Name, value, StringComparison.Ordinal));

        if (single is not null)
        {
            return [single];
        }

        var prefix = value + ".";

        var matches = all
            .Where(command => string.Equals(command.Namespace, value, StringComparison.Ordinal)
                              || command.Namespace.StartsWith(prefix, StringComparison.Ordinal))
            .ToList();

        return matches.Count > 0
            ? matches
            : throw new InvalidOperationException($"Der Bereich '{value}' enthält keine Commands.");
    }

    /// <summary>
    /// Ermittelt die Typen für einen Bereich.
    /// </summary>
    /// <param name="provider">Der Metadaten-Provider.</param>
    /// <param name="scope">Namespace oder Command-Name; leer bedeutet „alle“.</param>
    /// <returns>Die Typen im Bereich.</returns>
    /// <exception cref="InvalidOperationException">
    /// Wird geworfen, wenn der Bereich unbekannt ist.
    /// </exception>
    public static IReadOnlyList<Type> ResolveTypes(
        Services.CommandMetadataProvider provider,
        string? scope)
    {
        var types = provider.GetCommandTypes(scope);

        return types ?? throw new InvalidOperationException(
            $"Der Bereich '{scope}' enthält keine Commands.");
    }

    /// <summary>
    /// Ergänzt den Dokumenttitel um den Bereich.
    /// </summary>
    /// <param name="baseTitle">Der Basis-Titel aus den Optionen.</param>
    /// <param name="scope">Der gewählte Bereich oder <c>null</c>.</param>
    /// <returns>Der Basis-Titel oder „Basis-Titel - Bereich“.</returns>
    public static string ResolveTitle(string baseTitle, string? scope) =>
        string.IsNullOrWhiteSpace(scope)
            ? baseTitle
            : $"{baseTitle} - {scope.Trim()}";

    /// <summary>
    /// Entfernt einen YAML-Frontmatter-Block am Anfang einer Notiz.
    /// </summary>
    /// <param name="note">Der Inhalt der Notiz.</param>
    /// <returns>Die Notiz ohne Frontmatter.</returns>
    public static string StripFrontMatter(string note)
    {
        if (!note.StartsWith("---", StringComparison.Ordinal))
        {
            return note;
        }

        var end = note.IndexOf("\n---", 3, StringComparison.Ordinal);

        return end < 0
            ? note
            : note[(end + 4)..].TrimStart('\r', '\n');
    }

    /// <summary>
    /// Wandelt einen Titel in einen dateisystemtauglichen Namen.
    /// </summary>
    /// <param name="title">Der Titel.</param>
    /// <returns>Der Name ohne Sonderzeichen.</returns>
    public static string Slug(string title)
    {
        var invalid = Path.GetInvalidFileNameChars();
        string cleaned = new(title.Select(c => invalid.Contains(c) || c == ' ' ? '-' : c).ToArray());

        return cleaned.Trim('-');
    }
}
