using ATK.Command.Explorer.Commands;
using ATK.Command.Explorer.Models;
using ATK.Command.Explorer.Services;

namespace ATK.Command.Explorer.Features.Metadata;

/// <summary>
/// Liefert alle Commands als Namespace-Hierarchie.
/// </summary>
public sealed class GetNamespaceTreeCommand : IExplorerCommand<IReadOnlyList<NamespaceNode>>
{
}

/// <summary>
/// Führt <see cref="GetNamespaceTreeCommand"/> aus.
/// </summary>
public sealed class GetNamespaceTreeCommandHandler
    : IExplorerCommandHandler<GetNamespaceTreeCommand, IReadOnlyList<NamespaceNode>>
{
    private readonly CommandMetadataProvider provider;

    /// <summary>Erzeugt den Handler.</summary>
    /// <param name="provider">Der Metadaten-Provider.</param>
    public GetNamespaceTreeCommandHandler(CommandMetadataProvider provider)
    {
        this.provider = provider;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<NamespaceNode>> HandleAsync(
        GetNamespaceTreeCommand command,
        CancellationToken cancellationToken) =>
        Task.FromResult(this.provider.GetNamespaceTree());
}
