using ATK.Command.Explorer.Configuration;
using Microsoft.AspNetCore.Authorization;

namespace ATK.Command.Explorer.Authorization;

/// <summary>
/// Autorisierungsanforderung für die Endpunkte des Command Explorers.
/// </summary>
/// <remarks>
/// Bündelt die in <see cref="CommandExplorerOptions"/> konfigurierten Claims. Rollen und
/// Policies werden nicht hier geprüft, sondern über die von ASP.NET Core erzeugten
/// Anforderungen der Rollen- und Policy-Auswertung.
/// </remarks>
public sealed class CommandExplorerClaimRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// Erzeugt die Anforderung aus den konfigurierten Claims.
    /// </summary>
    /// <param name="claims">Claims, die der Aufrufer besitzen muss.</param>
    public CommandExplorerClaimRequirement(IEnumerable<CommandExplorerClaim> claims)
    {
        ArgumentNullException.ThrowIfNull(claims);

        this.Claims = claims
            .Where(claim => !string.IsNullOrWhiteSpace(claim.Type))
            .Select(claim => new CommandExplorerClaim
            {
                Type = claim.Type,
                Values = claim.Values
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .ToList(),
            })
            .ToList();
    }

    /// <summary>Claims, die der Aufrufer besitzen muss (UND-Verknüpfung).</summary>
    public IReadOnlyList<CommandExplorerClaim> Claims { get; }
}
