namespace ATK.Command.Explorer.Configuration;

/// <summary>
/// Eine Authentifizierung, die der Explorer in der UI zur Auswahl anbietet.
/// </summary>
/// <remarks>
/// <para>
/// Einträge lassen sich auf zwei Wegen bereitstellen:
/// </para>
/// <list type="bullet">
/// <item>
/// <b>Über die Konfiguration</b> (<c>appsettings.json</c>, Abschnitt
/// <c>CommandExplorer:Authentications</c>). Dann steht der Token statisch in
/// <see cref="Token"/>.
/// </item>
/// <item>
/// <b>Im Code</b> über <c>AddCommandExplorer(..., options =&gt; options.Authentications.Add(...))</c>.
/// Dann kann statt <see cref="Token"/> eine <see cref="TokenFactory"/> gesetzt werden, die den
/// Token zur Laufzeit beschafft – etwa aus einem Identity-Service oder per Client-Credentials.
/// </item>
/// </list>
/// <para>
/// Der Token verlässt den Server nie: Die UI wählt nur die <see cref="Id"/> aus, das
/// Backend setzt den passenden Header.
/// </para>
/// </remarks>
public sealed class CommandExplorerAuthentication
{
    /// <summary>
    /// Eindeutige Kennung, die die UI an das Backend sendet.
    /// </summary>
    /// <remarks>
    /// Wird im Header <c>X-Explorer-Auth</c> übertragen. Muss innerhalb der Liste eindeutig
    /// sein; die Prüfung erfolgt beim Start.
    /// </remarks>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Anzeigename im Dropdown der UI.
    /// </summary>
    /// <remarks>
    /// Bleibt der Wert leer, zeigt die UI die <see cref="Id"/> an.
    /// </remarks>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Art der Authentifizierung.
    /// </summary>
    public CommandExplorerAuthenticationType Type { get; set; } = CommandExplorerAuthenticationType.None;

    /// <summary>
    /// Statischer Token aus der Konfiguration.
    /// </summary>
    /// <remarks>
    /// Wird verwendet, wenn <see cref="TokenFactory"/> nicht gesetzt ist. Bei
    /// <see cref="CommandExplorerAuthenticationType.Basic"/> ist der Wert bereits
    /// base64-kodiert zu erwarten.
    /// </remarks>
    public string? Token { get; set; }

    /// <summary>
    /// Beschafft den Token zur Laufzeit.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Hat Vorrang vor <see cref="Token"/>. Der Delegate erhält den
    /// <see cref="IServiceProvider"/> der Anwendung, sodass beliebige registrierte Dienste
    /// verwendet werden können:
    /// </para>
    /// <code>
    /// options.Authentications.Add(new CommandExplorerAuthentication
    /// {
    ///     Id = "service",
    ///     Name = "Service-Konto",
    ///     Type = CommandExplorerAuthenticationType.Bearer,
    ///     TokenFactory = async (services, ct) =>
    ///     {
    ///         ITokenService tokens = services.GetRequiredService&lt;ITokenService&gt;();
    ///         return await tokens.GetAccessTokenAsync(ct);
    ///     }
    /// });
    /// </code>
    /// <para>
    /// Der Delegate wird bei jeder Anfrage aufgerufen – ein Cache ist Sache der Anwendung.
    /// </para>
    /// </remarks>
    public Func<IServiceProvider, CancellationToken, Task<string?>>? TokenFactory { get; set; }

    /// <summary>
    /// Name des Headers für <see cref="CommandExplorerAuthenticationType.ApiKey"/> und
    /// <see cref="CommandExplorerAuthenticationType.Custom"/>.
    /// </summary>
    /// <remarks>
    /// Standard ist <c>X-Api-Key</c>. Für <c>Bearer</c> und <c>Basic</c> ohne Bedeutung –
    /// dort wird immer <c>Authorization</c> verwendet.
    /// </remarks>
    public string? HeaderName { get; set; }

    /// <summary>
    /// Präfix vor dem Token bei <see cref="CommandExplorerAuthenticationType.Custom"/>.
    /// </summary>
    /// <remarks>
    /// Beispiel: <c>"Token "</c> ergibt <c>X-Auth: Token abc123</c>. Ohne Angabe wird der
    /// Token ohne Präfix gesendet.
    /// </remarks>
    public string? Prefix { get; set; }

    /// <summary>
    /// Benutzername für <see cref="CommandExplorerAuthenticationType.Basic"/>.
    /// </summary>
    /// <remarks>
    /// Wird nur ausgewertet, wenn <see cref="Token"/> leer ist.
    /// </remarks>
    public string? UserName { get; set; }

    /// <summary>
    /// Passwort für <see cref="CommandExplorerAuthenticationType.Basic"/>.
    /// </summary>
    /// <remarks>
    /// Wird nur ausgewertet, wenn <see cref="Token"/> leer ist.
    /// </remarks>
    public string? Password { get; set; }
}
