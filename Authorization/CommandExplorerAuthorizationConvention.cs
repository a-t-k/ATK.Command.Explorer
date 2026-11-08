using ATK.Command.Explorer.Configuration;
using ATK.Command.Explorer.Controller;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Extensions.Options;

namespace ATK.Command.Explorer.Authorization;

/// <summary>
/// Hängt die Autorisierung des Explorers an seinen Controller.
/// </summary>
/// <remarks>
/// <para>
/// Attribute können keine Werte aus der Konfiguration aufnehmen. Deshalb setzt diese
/// Konvention zur Laufzeit entweder einen <see cref="AllowAnonymousFilter"/> oder einen
/// <see cref="AuthorizeFilter"/> mit der aus den Optionen gebauten Policy auf den
/// <see cref="CommandExplorerController"/>.
/// </para>
/// <para>
/// Rollen, Policies, Claims und Authentifizierungsschemata wirken damit ohne Neukompilierung.
/// </para>
/// </remarks>
public sealed class CommandExplorerAuthorizationConvention : IControllerModelConvention
{
    private readonly IOptionsMonitor<CommandExplorerOptions> options;
    private readonly IOptionsMonitor<AuthorizationOptions> authorizationOptions;
    private readonly IOptionsMonitor<AuthenticationOptions> authenticationOptions;

    /// <summary>
    /// Erzeugt die Konvention.
    /// </summary>
    /// <param name="options">Die Explorer-Optionen.</param>
    /// <param name="authorizationOptions">
    /// Optionen der Anwendung; darüber werden referenzierte Policies aufgelöst.
    /// </param>
    /// <param name="authenticationOptions">
    /// Optionen der Anwendung; darüber wird geprüft, ob ein Authentifizierungsschema
    /// registriert ist.
    /// </param>
    public CommandExplorerAuthorizationConvention(
        IOptionsMonitor<CommandExplorerOptions> options,
        IOptionsMonitor<AuthorizationOptions> authorizationOptions,
        IOptionsMonitor<AuthenticationOptions> authenticationOptions)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(authorizationOptions);
        ArgumentNullException.ThrowIfNull(authenticationOptions);

        this.options = options;
        this.authorizationOptions = authorizationOptions;
        this.authenticationOptions = authenticationOptions;
    }

    /// <inheritdoc />
    public void Apply(ControllerModel controller)
    {
        ArgumentNullException.ThrowIfNull(controller);

        if (controller.ControllerType != typeof(CommandExplorerController))
        {
            return;
        }

        CommandExplorerOptions explorer = this.options.CurrentValue;

        if (explorer.AllowAnonymous)
        {
            controller.Filters.Add(new AllowAnonymousFilter());
            return;
        }

        this.EnsureAuthenticationIsConfigured(explorer);

        AuthorizationPolicy policy = CommandExplorerPolicyBuilder.Build(
            explorer,
            this.authorizationOptions.CurrentValue);

        controller.Filters.Add(new AuthorizeFilter(policy));
    }

    /// <summary>
    /// Stellt sicher, dass die Anwendung ein Authentifizierungsschema registriert hat.
    /// </summary>
    /// <remarks>
    /// Ohne Schema würde ASP.NET Core beim ersten Aufruf mit einem
    /// <see cref="InvalidOperationException"/> antworten („No authenticationScheme was
    /// specified"). Der Fehler wird deshalb schon beim Start mit einer umsetzbaren Meldung
    /// gemeldet.
    /// </remarks>
    private void EnsureAuthenticationIsConfigured(CommandExplorerOptions explorer)
    {
        AuthenticationOptions authentication = this.authenticationOptions.CurrentValue;

        bool hasScheme = explorer.AuthenticationSchemes.Count > 0
            || !string.IsNullOrEmpty(authentication.DefaultScheme)
            || !string.IsNullOrEmpty(authentication.DefaultChallengeScheme);

        if (hasScheme)
        {
            return;
        }

        throw new InvalidOperationException(
            "Der Command Explorer ist mit 'CommandExplorer:AllowAnonymous = false' "
            + "konfiguriert, aber die Anwendung hat kein Authentifizierungsschema registriert. "
            + "Registriere eines über 'services.AddAuthentication(...)' oder trage in "
            + "'CommandExplorer:AuthenticationSchemes' ein vorhandenes Schema ein.");
    }
}
