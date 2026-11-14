using ATK.Command.Explorer.Commands;
using ATK.Command.Explorer.Models;

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
    private readonly Services.CommandMetadataProvider provider;

    /// <summary>Erzeugt den Handler.</summary>
    /// <param name="provider">Der Metadaten-Provider.</param>
    public GetCommandsCommandHandler(Services.CommandMetadataProvider provider)
    {
        this.provider = provider;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<CommandDescriptor>> HandleAsync(
        GetCommandsCommand command,
        CancellationToken cancellationToken) =>
        Task.FromResult(this.provider.GetCommands());
}

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
    private readonly Services.CommandMetadataProvider provider;

    /// <summary>Erzeugt den Handler.</summary>
    /// <param name="provider">Der Metadaten-Provider.</param>
    public GetNamespaceTreeCommandHandler(Services.CommandMetadataProvider provider)
    {
        this.provider = provider;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<NamespaceNode>> HandleAsync(
        GetNamespaceTreeCommand command,
        CancellationToken cancellationToken) =>
        Task.FromResult(this.provider.GetNamespaceTree());
}

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
    private readonly Services.CommandMetadataProvider provider;

    /// <summary>Erzeugt den Handler.</summary>
    /// <param name="provider">Der Metadaten-Provider.</param>
    public GetCommandCommandHandler(Services.CommandMetadataProvider provider)
    {
        this.provider = provider;
    }

    /// <inheritdoc />
    public Task<CommandDescriptor?> HandleAsync(
        GetCommandCommand command,
        CancellationToken cancellationToken) =>
        Task.FromResult(this.provider.GetCommand(command.Name));
}

/// <summary>
/// Liefert die konfigurierten Authentifizierungen für das Dropdown.
/// </summary>
public sealed class GetAuthenticationsCommand
    : IExplorerCommand<IReadOnlyList<CommandAuthenticationDescriptor>>
{
}

/// <summary>
/// Führt <see cref="GetAuthenticationsCommand"/> aus.
/// </summary>
public sealed class GetAuthenticationsCommandHandler
    : IExplorerCommandHandler<GetAuthenticationsCommand, IReadOnlyList<CommandAuthenticationDescriptor>>
{
    private readonly Services.CommandAuthenticationResolver resolver;

    /// <summary>Erzeugt den Handler.</summary>
    /// <param name="resolver">Der Authentifizierungs-Resolver.</param>
    public GetAuthenticationsCommandHandler(Services.CommandAuthenticationResolver resolver)
    {
        this.resolver = resolver;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<CommandAuthenticationDescriptor>> HandleAsync(
        GetAuthenticationsCommand command,
        CancellationToken cancellationToken) =>
        Task.FromResult(this.resolver.GetDescriptors());
}
