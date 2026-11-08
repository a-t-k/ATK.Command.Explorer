namespace ATK.Command.Explorer.Models;

/// <summary>
/// Beschreibt, welche Authentifizierung für ein Command gilt.
/// </summary>
public sealed class CommandAuthorizationInfo
{
    /// <summary>Gibt an, ob das Command ohne Anmeldung ausgeführt werden darf.</summary>
    public bool AllowAnonymous { get; init; }

    /// <summary>Gibt an, ob eine Authentifizierung erforderlich ist.</summary>
    public bool RequiresAuthentication { get; init; }

    /// <summary>Rollen, die für die Ausführung nötig sind.</summary>
    public IReadOnlyList<string> Roles { get; init; } = [];

    /// <summary>Policies, die für die Ausführung nötig sind.</summary>
    public IReadOnlyList<string> Policies { get; init; } = [];

    /// <summary>Claim-Anforderungen, die für die Ausführung nötig sind.</summary>
    public IReadOnlyList<CommandClaimRequirement> Claims { get; init; } = [];

    /// <summary>Kurze, menschenlesbare Zusammenfassung der Autorisierung.</summary>
    public string Description { get; init; } = string.Empty;
}
