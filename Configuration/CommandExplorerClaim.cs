namespace ATK.Command.Explorer.Configuration;

/// <summary>
/// Ein Claim, den ein Aufrufer der Explorer-Endpunkte besitzen muss.
/// </summary>
/// <remarks>
/// Wird über <see cref="CommandExplorerOptions.Claims"/> konfiguriert. Alle dort eingetragenen
/// Claims müssen gleichzeitig erfüllt sein (UND-Verknüpfung).
/// </remarks>
public sealed class CommandExplorerClaim
{
    /// <summary>Typ des Claims, z. B. <c>department</c>.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Erlaubte Werte des Claims. Ist die Liste leer, genügt das Vorhandensein des Claims.
    /// </summary>
    public IList<string> Values { get; set; } = [];
}
