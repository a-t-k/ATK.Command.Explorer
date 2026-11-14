using ATK.Command.Communication.Models;
using ATK.Command.Explorer.Commands;
using ATK.Command.Explorer.Configuration;
using ATK.Command.Explorer.Documentation;
using ATK.Command.Explorer.Models;
using ATK.Command.Explorer.Services;
using Microsoft.Extensions.Options;

namespace ATK.Command.Explorer.Features.Documentation;

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
        var types = ScopeResolver.ResolveTypes(this.provider, command.Scope);
        var files = this.generator.Generate(types);
        var title = ScopeResolver.ResolveTitle(this.options.Value.DocumentationTitle, command.Scope);

        var archive = ZipArchiveBuilder.Build(
            files,
            MarkdownVaultGenerator.SanitizeFileName(title));

        return Task.FromResult(FileModelFactory.Create(
            archive,
            "application/zip",
            $"{ScopeResolver.Slug(title)}-typescript.zip"));
    }
}
