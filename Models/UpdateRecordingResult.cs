namespace ATK.Command.Explorer.Models;

/// <summary>
/// Ergebnis einer Änderung an einem aufgezeichneten Aufruf.
/// </summary>
/// <remarks>
/// <para>
/// Die Änderung kann abgelehnt werden – etwa wenn der Eintrag nicht mehr existiert oder
/// das Feld <c>$type</c> verändert wurde. In diesem Fall ist <see cref="Success"/>
/// <c>false</c> und <see cref="Error"/> nennt den Grund.
/// </para>
/// <para>
/// <see cref="Session"/> enthält immer den aktuellen Stand der Aufnahme, damit die UI
/// nach einer erfolgreichen Änderung nicht erneut laden muss.
/// </para>
/// </remarks>
public sealed class UpdateRecordingResult
{
    /// <summary>Gibt an, ob die Änderung übernommen wurde.</summary>
    public bool Success { get; init; }

    /// <summary>Grund der Ablehnung oder <c>null</c> bei Erfolg.</summary>
    public string? Error { get; init; }

    /// <summary>Der aktuelle Stand der Aufnahme.</summary>
    public RecordingSession Session { get; init; } = new();
}
