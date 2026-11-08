namespace ATK.Command.Explorer.Models;

/// <summary>
/// Eine Claim-Anforderung, die über <c>ClaimRequirementAttribute</c> am Command definiert wurde.
/// </summary>
public sealed class CommandClaimRequirement
{
    /// <summary>Typ des Claims, z. B. <c>department</c>.</summary>
    public string ClaimType { get; init; } = string.Empty;

    /// <summary>Erwarteter Wert des Claims, z. B. <c>hr</c>.</summary>
    public string ClaimValue { get; init; } = string.Empty;
}
