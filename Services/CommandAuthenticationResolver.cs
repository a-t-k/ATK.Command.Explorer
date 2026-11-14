using System.Text;
using ATK.Command.Explorer.Configuration;
using ATK.Command.Explorer.Models;
using Microsoft.Extensions.Options;

namespace ATK.Command.Explorer.Services;

/// <summary>
/// Löst die vom Aufrufer gewählte Authentifizierung auf und baut daraus die Header.
/// </summary>
/// <remarks>
/// <para>
/// Die UI sendet nur die <c>Id</c> einer konfigurierten Authentifizierung. Dieses Service
/// ermittelt den zugehörigen Eintrag, beschafft den Token (statisch aus der Konfiguration
/// oder über die registrierte <see cref="CommandExplorerAuthentication.TokenFactory"/>) und
/// liefert die fertigen Header.
/// </para>
/// <para>
/// Der Token verlässt den Server nie – die UI sieht nur <c>Id</c>, <c>Name</c> und <c>Type</c>.
/// </para>
/// </remarks>
public sealed class CommandAuthenticationResolver
{
    private readonly IOptionsMonitor<CommandExplorerOptions> options;
    private readonly IServiceProvider services;

    /// <summary>
    /// Erzeugt den Resolver.
    /// </summary>
    /// <param name="options">Die Explorer-Optionen mit den Authentifizierungen.</param>
    /// <param name="services">
    /// Service-Provider, der an die <see cref="CommandExplorerAuthentication.TokenFactory"/>
    /// übergeben wird.
    /// </param>
    public CommandAuthenticationResolver(
        IOptionsMonitor<CommandExplorerOptions> options,
        IServiceProvider services)
    {
        this.options = options;
        this.services = services;
    }

    /// <summary>
    /// Liefert die Authentifizierungen für das Dropdown der UI.
    /// </summary>
    /// <returns>Beschreibungen ohne Token.</returns>
    public IReadOnlyList<CommandAuthenticationDescriptor> GetDescriptors()
    {
        CommandExplorerOptions current = this.options.CurrentValue;

        if (!current.ShowAuthenticationSelector)
        {
            return [];
        }

        return current.Authentications
            .Select(authentication => new CommandAuthenticationDescriptor
            {
                Id = authentication.Id,
                Name = string.IsNullOrWhiteSpace(authentication.Name)
                    ? authentication.Id
                    : authentication.Name,
                Type = authentication.Type.ToString(),
                HeaderName = ResolveHeaderName(authentication),
                IsDynamic = authentication.TokenFactory is not null,
            })
            .ToList();
    }

    /// <summary>
    /// Baut die Header für die angegebene Authentifizierung.
    /// </summary>
    /// <param name="id">
    /// Die <c>Id</c> aus dem Header <c>X-Explorer-Auth</c>. <c>null</c> oder leer bedeutet
    /// „keine Authentifizierung“.
    /// </param>
    /// <param name="cancellationToken">Abbruch-Token.</param>
    /// <returns>
    /// Die zu setzenden Header. Leer, wenn keine Authentifizierung gewählt wurde oder der
    /// Eintrag unbekannt ist.
    /// </returns>
    public async Task<IReadOnlyDictionary<string, string>> ResolveHeadersAsync(
        string? id,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return new Dictionary<string, string>();
        }

        CommandExplorerAuthentication? authentication = this.options.CurrentValue.Authentications
            .FirstOrDefault(entry => string.Equals(entry.Id, id.Trim(), StringComparison.Ordinal));

        if (authentication is null)
        {
            return new Dictionary<string, string>();
        }

        string? token = await this.ResolveTokenAsync(authentication, cancellationToken);

        if (string.IsNullOrWhiteSpace(token))
        {
            return new Dictionary<string, string>();
        }

        return BuildHeaders(authentication, token);
    }

    /// <summary>
    /// Beschafft den Token – bevorzugt über die Fabrikmethode, sonst aus der Konfiguration.
    /// </summary>
    private async Task<string?> ResolveTokenAsync(
        CommandExplorerAuthentication authentication,
        CancellationToken cancellationToken)
    {
        if (authentication.TokenFactory is not null)
        {
            return await authentication.TokenFactory(this.services, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(authentication.Token))
        {
            return authentication.Token;
        }

        // Basic ohne fertigen Token: aus Benutzername und Passwort kodieren.
        if (authentication.Type == CommandExplorerAuthenticationType.Basic
            && !string.IsNullOrWhiteSpace(authentication.UserName))
        {
            string raw = $"{authentication.UserName}:{authentication.Password}";
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
        }

        return null;
    }

    /// <summary>
    /// Baut aus Typ, Token und optionalem Präfix die Header.
    /// </summary>
    private static IReadOnlyDictionary<string, string> BuildHeaders(
        CommandExplorerAuthentication authentication,
        string token)
    {
        Dictionary<string, string> headers = [];

        switch (authentication.Type)
        {
            case CommandExplorerAuthenticationType.None:
                break;

            case CommandExplorerAuthenticationType.Bearer:
                headers["Authorization"] = $"Bearer {token}";
                break;

            case CommandExplorerAuthenticationType.Basic:
                headers["Authorization"] = $"Basic {token}";
                break;

            case CommandExplorerAuthenticationType.ApiKey:
                headers[ResolveHeaderName(authentication)] = token;
                break;

            case CommandExplorerAuthenticationType.Custom:
                string prefix = authentication.Prefix ?? string.Empty;
                headers[ResolveHeaderName(authentication)] = prefix.Length == 0
                    ? token
                    : $"{prefix}{token}";
                break;

            default:
                break;
        }

        return headers;
    }

    /// <summary>
    /// Ermittelt den Headernamen, der für diese Authentifizierung gesetzt wird.
    /// </summary>
    /// <remarks>
    /// <c>Bearer</c> und <c>Basic</c> nutzen immer <c>Authorization</c>; nur
    /// <c>ApiKey</c> und <c>Custom</c> verwenden einen eigenen Header.
    /// </remarks>
    private static string ResolveHeaderName(CommandExplorerAuthentication authentication) =>
        authentication.Type switch
        {
            CommandExplorerAuthenticationType.Bearer => "Authorization",
            CommandExplorerAuthenticationType.Basic => "Authorization",
            CommandExplorerAuthenticationType.None => string.Empty,
            _ => string.IsNullOrWhiteSpace(authentication.HeaderName)
                ? "X-Api-Key"
                : authentication.HeaderName,
        };
}
