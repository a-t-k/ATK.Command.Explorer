using ATK.Command.Explorer.Configuration;
using Microsoft.Extensions.Options;

namespace ATK.Command.Explorer.Services;

/// <summary>
/// Prüft die Authentifizierungs-Konfiguration beim Start der Anwendung.
/// </summary>
/// <remarks>
/// Fehlerhafte Einträge sollen nicht erst beim Ausführen eines Commands auffallen, sondern
/// sofort beim Start mit einer klaren Meldung. Geprüft werden:
/// <list type="bullet">
/// <item>leere <c>Id</c>,</item>
/// <item>doppelte <c>Id</c>,</item>
/// <item>fehlender Token bei Typen, die einen brauchen.</item>
/// </list>
/// </remarks>
public sealed class CommandExplorerOptionsValidator : IValidateOptions<CommandExplorerOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, CommandExplorerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> failures = [];

        HashSet<string> seen = new(StringComparer.Ordinal);

        foreach (CommandExplorerAuthentication authentication in options.Authentications)
        {
            if (string.IsNullOrWhiteSpace(authentication.Id))
            {
                failures.Add(
                    "CommandExplorer:Authentications enthält einen Eintrag ohne 'Id'. "
                    + "Die Id wird im Header X-Explorer-Auth übertragen und muss gesetzt sein.");
                continue;
            }

            if (!seen.Add(authentication.Id))
            {
                failures.Add(
                    $"CommandExplorer:Authentications enthält die Id '{authentication.Id}' mehrfach. "
                    + "Jede Id muss eindeutig sein.");
            }

            bool hasToken = !string.IsNullOrWhiteSpace(authentication.Token)
                            || authentication.TokenFactory is not null;

            bool hasBasicCredentials = authentication.Type == CommandExplorerAuthenticationType.Basic
                                       && !string.IsNullOrWhiteSpace(authentication.UserName);

            if (authentication.Type != CommandExplorerAuthenticationType.None
                && !hasToken
                && !hasBasicCredentials)
            {
                failures.Add(
                    $"CommandExplorer:Authentications['{authentication.Id}'] hat den Typ "
                    + $"'{authentication.Type}', aber weder 'Token' noch 'TokenFactory'"
                    + (authentication.Type == CommandExplorerAuthenticationType.Basic
                        ? " noch 'UserName'/'Password'"
                        : string.Empty)
                    + ". Ohne Token kann kein Header gesetzt werden.");
            }
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
