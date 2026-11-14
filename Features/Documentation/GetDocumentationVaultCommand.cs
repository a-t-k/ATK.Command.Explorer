using ATK.Command.Communication.Models;
using ATK.Command.Explorer.Commands;
using ATK.Command.Explorer.Configuration;
using ATK.Command.Explorer.Documentation;
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
    public async Task<FileModel> HandleAsync(
        GetDocumentationVaultCommand command,
        CancellationToken cancellationToken)
    {
        var commands = ScopeResolver.Resolve(this.provider, command.Scope);
        var title = ScopeResolver.ResolveTitle(this.options.Value.DocumentationTitle, command.Scope);

        var files = this.generator.Generate(commands, title);
        var archive = VaultArchiveBuilder.Build(
            files,
            MarkdownVaultGenerator.SanitizeFileName(title));

        return await Task.FromResult(FileModelFactory.Create(
            archive,
            "application/zip",
            $"{ScopeResolver.Slug(title)}-vault.zip"));
    }
}
