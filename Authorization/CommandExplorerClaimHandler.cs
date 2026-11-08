using ATK.Command.Explorer.Configuration;
using Microsoft.AspNetCore.Authorization;

namespace ATK.Command.Explorer.Authorization;

/// <summary>
/// Prüft die in <see cref="CommandExplorerOptions.Claims"/> konfigurierten Claims.
/// </summary>
/// <remarks>
/// Alle konfigurierten Claims müssen vorhanden sein (UND). Innerhalb eines Claims genügt einer
/// der erlaubten Werte (ODER); eine leere Werteliste verlangt nur das Vorhandensein.
/// </remarks>
public sealed class CommandExplorerClaimHandler
    : AuthorizationHandler<CommandExplorerClaimRequirement>
{
    /// <inheritdoc />
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        CommandExplorerClaimRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requirement);

        foreach (CommandExplorerClaim claim in requirement.Claims)
        {
            IEnumerable<string> actualValues = context.User
                .FindAll(claim.Type)
                .Select(c => c.Value);

            bool satisfied = claim.Values.Count == 0
                ? context.User.HasClaim(c => c.Type == claim.Type)
                : actualValues.Any(value => claim.Values.Contains(value, StringComparer.Ordinal));

            if (!satisfied)
            {
                // Nicht "Fail" – ein anderer Handler darf die Anforderung noch erfüllen.
                return Task.CompletedTask;
            }
        }

        context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
