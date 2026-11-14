using System.Text;
using ATK.Command.Communication.Models;
using ATK.Command.Explorer.Commands;
using ATK.Command.Explorer.Configuration;
using ATK.Command.Explorer.Documentation;
using ATK.Command.Explorer.Models;
using ATK.Command.Explorer.Services;
using Microsoft.Extensions.Options;

namespace ATK.Command.Explorer.Features.Documentation;

/// <summary>
/// Liefert die Command-Dokumentation als einzelne Markdown-Datei.
/// </summary>
public sealed class GetDocumentationMarkdownCommand : IExplorerCommand<FileModel>
{
    /// <summary>Optionaler Namespace oder Command-Name.</summary>
    public string? Scope { get; set; }
}

/// <summary>
/// Führt <see cref="GetDocumentationMarkdownCommand"/> aus.
/// </summary>
public sealed class GetDocumentationMarkdownCommandHandler
    : IExplorerCommandHandler<GetDocumentationMarkdownCommand, FileModel>
{
    private readonly CommandMetadataProvider provider;
    private readonly MarkdownVaultGenerator generator;
    private readonly IOptions<CommandExplorerOptions> options;

    /// <summary>Erzeugt den Handler.</summary>
    /// <param name="provider">Der Metadaten-Provider.</param>
    /// <param name="generator">Der Vault-Generator.</param>
    /// <param name="options">Die Explorer-Optionen.</param>
    public GetDocumentationMarkdownCommandHandler(
        CommandMetadataProvider provider,
        MarkdownVaultGenerator generator,
        IOptions<CommandExplorerOptions> options)
    {
        this.provider = provider;
        this.generator = generator;
        this.options = options;
    }

    /// <inheritdoc />
    public async Task<FileModel> HandleAsync(
        GetDocumentationMarkdownCommand command,
        CancellationToken cancellationToken)
    {
        var commands = ScopeResolver.Resolve(this.provider, command.Scope);
        var title = ScopeResolver.ResolveTitle(this.options.Value.DocumentationTitle, command.Scope);

        var files = this.generator.Generate(commands, title);

        // Alle Notizen zu einem Dokument zusammenfassen – praktisch für Wikis ohne Obsidian.
        // Nur die Startseite behält ihr Frontmatter; bei den übrigen Notizen wäre ein zweiter
        // Frontmatter-Block mitten im Dokument wirkungslos und würde als doppelte `---`-Linie
        // erscheinen.
        var combined = string.Join(
            "\n\n---\n\n",
            files
                .Where(entry => entry.Key != "00 Start.md")
                .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => ScopeResolver.StripFrontMatter(entry.Value)));

        var document = files["00 Start.md"] + "\n\n---\n\n" + combined;

        return await Task.FromResult(FileModelFactory.Create(
            Encoding.UTF8.GetBytes(document),
            "text/markdown; charset=utf-8",
            $"{ScopeResolver.Slug(title)}.md"));
    }
}
