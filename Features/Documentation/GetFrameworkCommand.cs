using ATK.Command.Communication.Models;
using ATK.Command.Explorer.Commands;
using ATK.Command.Explorer.Configuration;
using ATK.Command.Explorer.Documentation;
using ATK.Command.Explorer.Framework;
using ATK.Command.Explorer.Models;
using Microsoft.Extensions.Options;

namespace ATK.Command.Explorer.Features.Documentation;

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
        var files = this.provider.GetTemplates(command.Framework);

        if (files is null)
        {
            throw new InvalidOperationException(
                $"Für das Framework '{command.Framework}' sind keine Vorlagen hinterlegt.");
        }

        var title = $"{this.options.Value.DocumentationTitle} - {command.Framework}";

        var archive = ZipArchiveBuilder.Build(
            files,
            MarkdownVaultGenerator.SanitizeFileName(title));

        return Task.FromResult(FileModelFactory.Create(
            archive,
            "application/zip",
            $"{ScopeResolver.Slug(title)}-framework.zip"));
    }
}
