using ATK.Command.Communication.Models;
using ATK.Command.Explorer.Commands;
using ATK.Command.Explorer.Configuration;
using ATK.Command.Explorer.Documentation;
using ATK.Command.Explorer.Models;
using ATK.Command.Explorer.Services;
using Microsoft.Extensions.Options;

namespace ATK.Command.Explorer.Features.Documentation;

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
    public async Task<FileModel> HandleAsync(
        GetDocumentationPdfCommand command,
        CancellationToken cancellationToken)
    {
        var commands = ScopeResolver.Resolve(this.provider, command.Scope);
        var title = ScopeResolver.ResolveTitle(this.options.Value.DocumentationTitle, command.Scope);

        var pdf = this.generator.Generate(commands, title);

        return await Task.FromResult(FileModelFactory.Create(
            pdf,
            "application/pdf",
            $"{ScopeResolver.Slug(title)}.pdf"));
    }
}
