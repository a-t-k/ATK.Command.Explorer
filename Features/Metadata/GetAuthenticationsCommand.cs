using ATK.Command.Explorer.Commands;
using ATK.Command.Explorer.Models;
using ATK.Command.Explorer.Services;

namespace ATK.Command.Explorer.Features.Metadata;

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
    private readonly CommandAuthenticationResolver resolver;

    /// <summary>Erzeugt den Handler.</summary>
    /// <param name="resolver">Der Authentifizierungs-Resolver.</param>
    public GetAuthenticationsCommandHandler(CommandAuthenticationResolver resolver)
    {
        this.resolver = resolver;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<CommandAuthenticationDescriptor>> HandleAsync(
        GetAuthenticationsCommand command,
        CancellationToken cancellationToken) =>
        Task.FromResult(this.resolver.GetDescriptors());
}
