using ATK.Command.Explorer.Commands;
using ATK.Command.Explorer.Models;
using ATK.Command.Explorer.Services;

namespace ATK.Command.Explorer.Features.Metadata;

/// <summary>
/// Liefert alle Commands der Anwendung.
/// </summary>
public sealed class GetCommandsCommand : IExplorerCommand<IReadOnlyList<CommandDescriptor>>
{
}

/// <summary>
/// Führt <see cref="GetCommandsCommand"/> aus.
/// </summary>
public sealed class GetCommandsCommandHandler
    : IExplorerCommandHandler<GetCommandsCommand, IReadOnlyList<CommandDescriptor>>
{
    private readonly CommandMetadataProvider provider;

    /// <summary>Erzeugt den Handler.</summary>
    /// <param name="provider">Der Metadaten-Provider.</param>
    public GetCommandsCommandHandler(CommandMetadataProvider provider)
    {
        this.provider = provider;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<CommandDescriptor>> HandleAsync(
        GetCommandsCommand command,
        CancellationToken cancellationToken) =>
        Task.FromResult(this.provider.GetCommands());
}
