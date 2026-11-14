namespace ATK.Command.Explorer.Configuration;

/// <summary>
/// Art der Authentifizierung, die der Explorer beim Ausführen von Commands mitsendet.
/// </summary>
/// <remarks>
/// Die Art bestimmt, welcher Header gesetzt wird und wie der Token eingebettet wird.
/// Der Token selbst kommt entweder aus der Konfiguration oder aus einer im Code
/// registrierten Fabrikmethode.
/// </remarks>
public enum CommandExplorerAuthenticationType
{
    /// <summary>
    /// Keine Authentifizierung – es wird kein Header gesetzt.
    /// </summary>
    None = 0,

    /// <summary>
    /// <c>Authorization: Bearer &lt;token&gt;</c>.
    /// </summary>
    Bearer = 1,

    /// <summary>
    /// <c>Authorization: Basic &lt;base64&gt;</c>.
    /// </summary>
    /// <remarks>
    /// Ist <see cref="CommandExplorerAuthentication.Token"/> gesetzt, wird der Wert direkt
    /// verwendet (bereits base64-kodiert). Andernfalls wird
    /// <c>Benutzername:Passwort</c> kodiert.
    /// </remarks>
    Basic = 2,

    /// <summary>
    /// Frei wählbarer Header, standardmäßig <c>X-Api-Key: &lt;token&gt;</c>.
    /// </summary>
    ApiKey = 3,

    /// <summary>
    /// Eigener Header mit eigenem Präfix, z. B. <c>X-Auth: Token &lt;wert&gt;</c>.
    /// </summary>
    Custom = 4,
}
