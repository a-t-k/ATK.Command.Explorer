using ATK.Command.Explorer.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace ATK.Command.Explorer.Authorization;

/// <summary>
/// Baut die Autorisierungs-Policy des Command Explorers aus den Optionen.
/// </summary>
/// <remarks>
/// Attribute können keine Laufzeitwerte aus der Konfiguration aufnehmen. Deshalb wird die
/// Policy beim Start aus <see cref="CommandExplorerOptions"/> gebaut und von
/// <see cref="CommandExplorerAuthorizationConvention"/> an den Controller gehängt.
/// </remarks>
public static class CommandExplorerPolicyBuilder
{
    /// <summary>
    /// Erzeugt die Policy aus den Optionen.
    /// </summary>
    /// <param name="options">Die Explorer-Optionen.</param>
    /// <param name="authorizationOptions">
    /// Optionen der Anwendung; darüber werden referenzierte Policies aufgelöst.
    /// </param>
    /// <returns>Die fertige Policy.</returns>
    public static AuthorizationPolicy Build(
        CommandExplorerOptions options,
        AuthorizationOptions authorizationOptions)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(authorizationOptions);

        AuthorizationPolicyBuilder builder = new();

        if (options.AuthenticationSchemes.Count > 0)
        {
            builder.AddAuthenticationSchemes([.. options.AuthenticationSchemes]);
        }

        // Ohne Rollen, Policies und Claims bleibt es bei "nur angemeldet".
        builder.RequireAuthenticatedUser();

        if (options.Roles.Count > 0)
        {
            builder.RequireRole([.. options.Roles]);
        }

        // Referenzierte Policies werden aufgelöst und ihre Anforderungen übernommen.
        foreach (var policyName in options.Policies)
        {
            var referenced = authorizationOptions.GetPolicy(policyName);

            if (referenced is null)
            {
                // Unbekannte Policy: ASP.NET Core würde hier ebenfalls verweigern.
                builder.AddRequirements(new DenyRequirement(policyName));
                continue;
            }

            builder.AddRequirements([.. referenced.Requirements]);

            if (referenced.AuthenticationSchemes.Count > 0)
            {
                builder.AddAuthenticationSchemes([.. referenced.AuthenticationSchemes]);
            }
        }

        if (options.Claims.Count > 0)
        {
            builder.AddRequirements(new CommandExplorerClaimRequirement(options.Claims));
        }

        return builder.Build();
    }

    /// <summary>
    /// Anforderung, die nie erfüllt wird – für konfigurierte, aber unbekannte Policies.
    /// </summary>
    private sealed record DenyRequirement(string PolicyName) : IAuthorizationRequirement;
}
