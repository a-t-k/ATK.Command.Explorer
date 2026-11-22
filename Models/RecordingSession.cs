namespace ATK.Command.Explorer.Models;

/// <summary>
/// Eine abgeschlossene oder laufende Aufnahme mit allen Einträgen.
/// </summary>
/// <remarks>
/// Die Einträge sind nach <see cref="RecordedExchange.Sequence"/> aufsteigend sortiert –
/// also in der Reihenfolge, in der die Requests eingetroffen sind.
/// </remarks>
public sealed class RecordingSession
{
    /// <summary>Kennung der Aufnahme.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Startzeit der Aufnahme (UTC).</summary>
    public DateTimeOffset StartedAt { get; init; }

    /// <summary>Endzeit der Aufnahme (UTC), solange sie läuft <c>null</c>.</summary>
    public DateTimeOffset? StoppedAt { get; init; }

    /// <summary>Gibt an, ob die Aufnahme noch läuft.</summary>
    public bool IsActive { get; init; }

    /// <summary>Die aufgezeichneten Einträge, aufsteigend nach <c>Sequence</c>.</summary>
    public IReadOnlyList<RecordedExchange> Exchanges { get; init; } = [];
}
