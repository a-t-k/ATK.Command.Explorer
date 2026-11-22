namespace ATK.Command.Explorer.Models;

/// <summary>
/// Ein aufgezeichneter Request/Response-Zyklus eines Commands.
/// </summary>
/// <remarks>
/// <para>
/// Der Recorder legt den Eintrag beim Eintreffen des Requests an und füllt die
/// Response-Felder, sobald die Anwendung geantwortet hat. Solange die Antwort aussteht,
/// sind <see cref="ResponseTimestamp"/>, <see cref="StatusCode"/> und
/// <see cref="ResponseBody"/> <c>null</c>.
/// </para>
/// <para>
/// <see cref="Sequence"/> ist monoton steigend und beginnt bei 1. Die UI sortiert danach,
/// weil Zeitstempel bei sehr schnellen Aufrufen identisch sein können.
/// </para>
/// </remarks>
public sealed class RecordedExchange
{
    /// <summary>Eindeutige Kennung des Eintrags.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Laufende Nummer innerhalb der Aufnahme, beginnend bei 1.</summary>
    public int Sequence { get; init; }

    /// <summary>Zeitpunkt, an dem der Request eingetroffen ist (UTC).</summary>
    public DateTimeOffset RequestTimestamp { get; init; }

    /// <summary>Zeitpunkt, an dem die Antwort fertig war (UTC).</summary>
    public DateTimeOffset? ResponseTimestamp { get; set; }

    /// <summary>Dauer vom Eintreffen bis zur fertigen Antwort in Millisekunden.</summary>
    public double? DurationMs { get; set; }

    /// <summary>Klassenname des Commands, aus <c>$type</c> gelesen.</summary>
    public string CommandName { get; init; } = string.Empty;

    /// <summary>Vollständiger <c>$type</c>-Wert inklusive Assembly.</summary>
    public string? CommandType { get; init; }

    /// <summary>Der Request-Body, unverändert.</summary>
    public string RequestBody { get; init; } = string.Empty;

    /// <summary>HTTP-Statuscode der Antwort.</summary>
    public int? StatusCode { get; set; }

    /// <summary>Der Response-Body, unverändert.</summary>
    public string? ResponseBody { get; set; }

    /// <summary>Content-Type der Antwort.</summary>
    public string? ContentType { get; set; }

    /// <summary>HTTP-Methode des Requests.</summary>
    public string Method { get; init; } = string.Empty;

    /// <summary>Pfad des Requests.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>
    /// Id der Authentifizierung, die der Explorer-Proxy gesetzt hat.
    /// </summary>
    /// <remarks>
    /// Wird aus dem Header <see cref="Configuration.CommandExplorerOptions.AuthenticationHeaderName"/>
    /// gelesen. Bei direkten Aufrufen ohne Explorer ist der Wert <c>null</c>.
    /// </remarks>
    public string? AuthenticationId { get; init; }

    /// <summary>Fehlermeldung, falls die Verarbeitung abgebrochen ist.</summary>
    public string? Error { get; set; }
}
