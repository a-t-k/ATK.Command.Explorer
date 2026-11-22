namespace ATK.Command.Explorer.Models;

/// <summary>
/// Zustand der Aufnahme, ohne die aufgezeichneten Einträge.
/// </summary>
/// <remarks>
/// Für das Polling in der UI gedacht: Die Statusabfrage läuft während einer laufenden
/// Aufnahme regelmäßig und soll nicht jedes Mal alle Einträge übertragen.
/// </remarks>
public sealed class RecordingStatus
{
    /// <summary>Gibt an, ob gerade aufgezeichnet wird.</summary>
    public bool IsActive { get; init; }

    /// <summary>Gibt an, ob überhaupt eine Aufnahme existiert (laufend oder beendet).</summary>
    public bool HasSession { get; init; }

    /// <summary>Kennung der aktuellen Aufnahme oder <c>null</c>.</summary>
    public string? SessionId { get; init; }

    /// <summary>Startzeit der Aufnahme (UTC).</summary>
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>Endzeit der Aufnahme (UTC), solange sie läuft <c>null</c>.</summary>
    public DateTimeOffset? StoppedAt { get; init; }

    /// <summary>Anzahl der bisher aufgezeichneten Einträge.</summary>
    public int ExchangeCount { get; init; }
}
