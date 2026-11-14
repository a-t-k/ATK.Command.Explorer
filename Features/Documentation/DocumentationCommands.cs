using System.Text;
using ATK.Command.Explorer.Commands;
using ATK.Command.Explorer.Configuration;
using ATK.Command.Explorer.Documentation;
using ATK.Command.Explorer.Framework;
using ATK.Command.Explorer.Models;
using ATK.Command.Explorer.Services;
using Microsoft.Extensions.Options;

namespace ATK.Command.Explorer.Features.Documentation;

/// <summary>
/// Liefert die Command-Dokumentation als Obsidian-Vault im ZIP.
/// </summary>
public sealed class GetDocumentationVaultCommand : IExplorerCommand<FileModel>
{
    /// <summary>Optionaler Namespace oder Command-Name.</summary>
    public string? Scope { get; set; }
}

/// <summary>
/// Führt <see cref="GetDocumentationVaultCommand"/> aus.
/// </summary>
public sealed class GetDocumentationVaultCommandHandler
    : IExplorerCommandHandler<GetDocumentationVaultCommand, FileModel>
{
    private readonly CommandMetadataProvider provider;
    private readonly MarkdownVaultGenerator generator;
    private readonly IOptions<CommandExplorerOptions> options;

    /// <summary>Erzeugt den Handler.</summary>
    /// <param name="provider">Der Metadaten-Provider.</param>
    /// <param name="generator">Der Vault-Generator.</param>
    /// <param name="options">Die Explorer-Optionen.</param>
    public GetDocumentationVaultCommandHandler(
        CommandMetadataProvider provider,
        MarkdownVaultGenerator generator,
        IOptions<CommandExplorerOptions> options)
    {
        this.provider = provider;
        this.generator = generator;
        this.options = options;
    }

    /// <inheritdoc />
    public Task<FileModel> HandleAsync(
        GetDocumentationVaultCommand command,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<CommandDescriptor> commands = ScopeResolver.Resolve(this.provider, command.Scope);
        string title = ScopeResolver.ResolveTitle(this.options.Value.DocumentationTitle, command.Scope);

        IReadOnlyDictionary<string, string> files = this.generator.Generate(commands, title);
        byte[] archive = VaultArchiveBuilder.Build(
            files,
            MarkdownVaultGenerator.SanitizeFileName(title));

        return Task.FromResult(FileModel.FromBytes(
            archive,
            "application/zip",
            $"{ScopeResolver.Slug(title)}-vault.zip"));
    }
}

/// <summary>
/// Liefert die Command-Dokumentation als PDF.
/// </summary>
public sealed class GetDocumentationPdfCommand : IExplorerCommand<FileModel>
{
    /// <summary>Optionaler Namespace oder Command-Name.</summary>
    public string? Scope { get; set; }
}

/// <summary>
/// Führt <see cref="GetDocumentationPdfCommand"/> aus.
/// </summary>
public sealed class GetDocumentationPdfCommandHandler
    : IExplorerCommandHandler<GetDocumentationPdfCommand, FileModel>
{
    private readonly CommandMetadataProvider provider;
    private readonly CommandPdfGenerator generator;
    private readonly IOptions<CommandExplorerOptions> options;

    /// <summary>Erzeugt den Handler.</summary>
    /// <param name="provider">Der Metadaten-Provider.</param>
    /// <param name="generator">Der PDF-Generator.</param>
    /// <param name="options">Die Explorer-Optionen.</param>
    public GetDocumentationPdfCommandHandler(
        CommandMetadataProvider provider,
        CommandPdfGenerator generator,
        IOptions<CommandExplorerOptions> options)
    {
        this.provider = provider;
        this.generator = generator;
        this.options = options;
    }

    /// <inheritdoc />
    public Task<FileModel> HandleAsync(
        GetDocumentationPdfCommand command,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<CommandDescriptor> commands = ScopeResolver.Resolve(this.provider, command.Scope);
        string title = ScopeResolver.ResolveTitle(this.options.Value.DocumentationTitle, command.Scope);

        byte[] pdf = this.generator.Generate(commands, title);

        return Task.FromResult(FileModel.FromBytes(
            pdf,
            "application/pdf",
            $"{ScopeResolver.Slug(title)}.pdf"));
    }
}

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
    public Task<FileModel> HandleAsync(
        GetDocumentationMarkdownCommand command,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<CommandDescriptor> commands = ScopeResolver.Resolve(this.provider, command.Scope);
        string title = ScopeResolver.ResolveTitle(this.options.Value.DocumentationTitle, command.Scope);

        IReadOnlyDictionary<string, string> files = this.generator.Generate(commands, title);

        // Alle Notizen zu einem Dokument zusammenfassen – praktisch für Wikis ohne Obsidian.
        // Nur die Startseite behält ihr Frontmatter; bei den übrigen Notizen wäre ein zweiter
        // Frontmatter-Block mitten im Dokument wirkungslos und würde als doppelte `---`-Linie
        // erscheinen.
        string combined = string.Join(
            "\n\n---\n\n",
            files
                .Where(entry => entry.Key != "00 Start.md")
                .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => ScopeResolver.StripFrontMatter(entry.Value)));

        string document = files["00 Start.md"] + "\n\n---\n\n" + combined;

        return Task.FromResult(FileModel.FromBytes(
            Encoding.UTF8.GetBytes(document),
            "text/markdown; charset=utf-8",
            $"{ScopeResolver.Slug(title)}.md"));
    }
}

/// <summary>
/// Liefert die Commands als TypeScript-Typen im ZIP.
/// </summary>
public sealed class GetTypeScriptCommand : IExplorerCommand<FileModel>
{
    /// <summary>Optionaler Namespace oder Command-Name.</summary>
    public string? Scope { get; set; }
}

/// <summary>
/// Führt <see cref="GetTypeScriptCommand"/> aus.
/// </summary>
public sealed class GetTypeScriptCommandHandler
    : IExplorerCommandHandler<GetTypeScriptCommand, FileModel>
{
    private readonly CommandMetadataProvider provider;
    private readonly TypeScriptExportGenerator generator;
    private readonly IOptions<CommandExplorerOptions> options;

    /// <summary>Erzeugt den Handler.</summary>
    /// <param name="provider">Der Metadaten-Provider.</param>
    /// <param name="generator">Der TypeScript-Generator.</param>
    /// <param name="options">Die Explorer-Optionen.</param>
    public GetTypeScriptCommandHandler(
        CommandMetadataProvider provider,
        TypeScriptExportGenerator generator,
        IOptions<CommandExplorerOptions> options)
    {
        this.provider = provider;
        this.generator = generator;
        this.options = options;
    }

    /// <inheritdoc />
    public Task<FileModel> HandleAsync(
        GetTypeScriptCommand command,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Type> types = ScopeResolver.ResolveTypes(this.provider, command.Scope);
        IReadOnlyDictionary<string, string> files = this.generator.Generate(types);
        string title = ScopeResolver.ResolveTitle(this.options.Value.DocumentationTitle, command.Scope);

        byte[] archive = ZipArchiveBuilder.Build(
            files,
            MarkdownVaultGenerator.SanitizeFileName(title));

        return Task.FromResult(FileModel.FromBytes(
            archive,
            "application/zip",
            $"{ScopeResolver.Slug(title)}-typescript.zip"));
    }
}

/// <summary>
/// Liefert das Framework-Gerüst für den TypeScript-Client im ZIP.
/// </summary>
public sealed class GetFrameworkCommand : IExplorerCommand<FileModel>
{
    /// <summary>Framework-Name, Standard <c>angular</c>.</summary>
    public string Framework { get; set; } = "angular";
}

/// <summary>
/// Führt <see cref="GetFrameworkCommand"/> aus.
/// </summary>
public sealed class GetFrameworkCommandHandler
    : IExplorerCommandHandler<GetFrameworkCommand, FileModel>
{
    private readonly FrameworkTemplateProvider provider;
    private readonly IOptions<CommandExplorerOptions> options;

    /// <summary>Erzeugt den Handler.</summary>
    /// <param name="provider">Der Vorlagen-Provider.</param>
    /// <param name="options">Die Explorer-Optionen.</param>
    public GetFrameworkCommandHandler(
        FrameworkTemplateProvider provider,
        IOptions<CommandExplorerOptions> options)
    {
        this.provider = provider;
        this.options = options;
    }

    /// <inheritdoc />
    public Task<FileModel> HandleAsync(
        GetFrameworkCommand command,
        CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, string>? files = this.provider.GetTemplates(command.Framework);

        if (files is null)
        {
            throw new InvalidOperationException(
                $"Für das Framework '{command.Framework}' sind keine Vorlagen hinterlegt.");
        }

        string title = $"{this.options.Value.DocumentationTitle} - {command.Framework}";

        byte[] archive = ZipArchiveBuilder.Build(
            files,
            MarkdownVaultGenerator.SanitizeFileName(title));

        return Task.FromResult(FileModel.FromBytes(
            archive,
            "application/zip",
            $"{ScopeResolver.Slug(title)}-framework.zip"));
    }
}
