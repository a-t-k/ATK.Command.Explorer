using ATK.Command.Explorer.Commands;
using ATK.Command.Explorer.Models;
using ATK.Command.Explorer.Services;

namespace ATK.Command.Explorer.Features.Metadata;

/// <summary>
/// Liefert einen einzelnen Command anhand seines Namens.
/// </summary>
public sealed class GetCommandCommand : IExplorerCommand<CommandDescriptor?>
{
    /// <summary>Klassenname oder vollqualifizierter Name des Commands.</summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Führt <see cref="GetCommandCommand"/> aus.
/// </summary>
public sealed class GetCommandCommandHandler
    : IExplorerCommandHandler<GetCommandCommand, CommandDescriptor?>
{
    private readonly CommandMetadataProvider provider;

    /// <summary>Erzeugt den Handler.</summary>
    /// <param name="provider">Der Metadaten-Provider.</param>
    public GetCommandCommandHandler(CommandMetadataProvider provider)
    {
        this.provider = provider;
    }

    /// <inheritdoc />
    public async Task<CommandDescriptor?> HandleAsync(
        GetCommandCommand command,
        CancellationToken cancellationToken)
    {
        return await Task.FromResult(this.provider.GetCommand(command.Name));
    }
}
