using System.Collections.Concurrent;
using System.Text.Json;
using ATK.Command.Explorer.Models;

namespace ATK.Command.Explorer.Services;

/// <summary>
/// Zeichnet Command-Requests und ihre Antworten auf.
/// </summary>
/// <remarks>
/// <para>
/// Der Dienst ist ein Singleton und lebt über die gesamte Laufzeit der Anwendung. Er hält
/// höchstens eine Aufnahme gleichzeitig: Eine neue Aufnahme ersetzt die vorherige, damit
/// der Speicher nicht unbegrenzt wächst.
/// </para>
/// <para>
/// Die Einträge liegen in einer <see cref="ConcurrentDictionary{TKey,TValue}"/> und werden
/// über eine laufende Nummer sortiert. Ein Eintrag entsteht beim Eintreffen des Requests
/// und wird nach der Antwort ergänzt – die UI sieht also auch noch laufende Aufrufe.
/// </para>
/// <para>
/// Der Zugriff ist bewusst threadsicher, weil mehrere Requests gleichzeitig eintreffen
/// können und die Aufnahme selbst ebenfalls über einen Request gesteuert wird.
/// </para>
/// </remarks>
public sealed class CommandRecorder
{
    private readonly ConcurrentDictionary<string, RecordedExchange> exchanges = new();
    private readonly Lock gate = new();

    private string? sessionId;
    private DateTimeOffset? startedAt;
    private DateTimeOffset? stoppedAt;
    private int sequence;

    /// <summary>Gibt an, ob gerade aufgezeichnet wird.</summary>
    public bool IsActive
    {
        get
        {
            lock (this.gate)
            {
                return this.sessionId is not null && this.stoppedAt is null;
            }
        }
    }

    /// <summary>Gibt an, ob eine Aufnahme existiert (laufend oder beendet).</summary>
    public bool HasSession
    {
        get
        {
            lock (this.gate)
            {
                return this.sessionId is not null;
            }
        }
    }

    /// <summary>
    /// Startet eine neue Aufnahme und verwirft eine vorhandene.
    /// </summary>
    /// <returns>Der Zustand der neuen Aufnahme.</returns>
    public RecordingStatus Start()
    {
        lock (this.gate)
        {
            this.exchanges.Clear();
            this.sequence = 0;
            this.sessionId = Guid.NewGuid().ToString("N");
            this.startedAt = DateTimeOffset.UtcNow;
            this.stoppedAt = null;

            return this.CreateStatus();
        }
    }

    /// <summary>
    /// Beendet die laufende Aufnahme.
    /// </summary>
    /// <returns>
    /// Der Zustand nach dem Stoppen. Läuft keine Aufnahme, bleibt der Zustand unverändert.
    /// </returns>
    public RecordingStatus Stop()
    {
        lock (this.gate)
        {
            if (this.sessionId is not null && this.stoppedAt is null)
            {
                this.stoppedAt = DateTimeOffset.UtcNow;
            }

            return this.CreateStatus();
        }
    }

    /// <summary>
    /// Verwirft die aktuelle Aufnahme vollständig.
    /// </summary>
    /// <returns>Der leere Zustand.</returns>
    public RecordingStatus Clear()
    {
        lock (this.gate)
        {
            this.exchanges.Clear();
            this.sequence = 0;
            this.sessionId = null;
            this.startedAt = null;
            this.stoppedAt = null;

            return this.CreateStatus();
        }
    }

    /// <summary>
    /// Übernimmt eine zuvor exportierte Aufnahme.
    /// </summary>
    /// <param name="session">Die zu übernehmende Aufnahme.</param>
    /// <returns>Der Zustand nach dem Import.</returns>
    /// <remarks>
    /// <para>
    /// Die Aufnahme wird als beendet übernommen – ein Import während einer laufenden
    /// Aufnahme beendet diese. Die laufende Nummer wird neu vergeben, damit sie lückenlos
    /// bei 1 beginnt, auch wenn die Datei von Hand bearbeitet wurde.
    /// </para>
    /// <para>
    /// Die Einträge werden nach ihrem Zeitstempel sortiert übernommen, damit die
    /// Reihenfolge auch dann stimmt, wenn die Datei unsortiert ist.
    /// </para>
    /// </remarks>
    public RecordingStatus Import(RecordingSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        lock (this.gate)
        {
            this.exchanges.Clear();
            this.sequence = 0;

            this.sessionId = string.IsNullOrWhiteSpace(session.Id)
                ? Guid.NewGuid().ToString("N")
                : session.Id;

            this.startedAt = session.StartedAt == default
                ? DateTimeOffset.UtcNow
                : session.StartedAt;

            // Importierte Aufnahmen gelten als beendet – sonst würde der Recorder
            // weiter mitschneiden, ohne dass der Benutzer das erwartet.
            this.stoppedAt = session.StoppedAt ?? this.startedAt;

            foreach (var entry in session.Exchanges.OrderBy(item => item.RequestTimestamp))
            {
                RecordedExchange copy = new()
                {
                    Id = string.IsNullOrWhiteSpace(entry.Id)
                        ? Guid.NewGuid().ToString("N")
                        : entry.Id,
                    Sequence = ++this.sequence,
                    RequestTimestamp = entry.RequestTimestamp,
                    ResponseTimestamp = entry.ResponseTimestamp,
                    DurationMs = entry.DurationMs,
                    CommandName = entry.CommandName,
                    CommandType = entry.CommandType,
                    RequestBody = entry.RequestBody,
                    StatusCode = entry.StatusCode,
                    ResponseBody = entry.ResponseBody,
                    ContentType = entry.ContentType,
                    Method = entry.Method,
                    Path = entry.Path,
                    AuthenticationId = entry.AuthenticationId,
                    Error = entry.Error,
                };

                this.exchanges[copy.Id] = copy;
            }

            return this.CreateStatus();
        }
    }

    /// <summary>Liefert den aktuellen Zustand ohne Einträge.</summary>
    /// <returns>Der Zustand der Aufnahme.</returns>
    public RecordingStatus GetStatus()
    {
        lock (this.gate)
        {
            return this.CreateStatus();
        }
    }

    /// <summary>
    /// Liefert die aktuelle Aufnahme mit allen Einträgen.
    /// </summary>
    /// <returns>
    /// Die Aufnahme, sortiert nach laufender Nummer. Existiert keine Aufnahme, ist
    /// <see cref="RecordingSession.Id"/> leer und <see cref="RecordingSession.Exchanges"/>
    /// leer.
    /// </returns>
    public RecordingSession GetSession()
    {
        lock (this.gate)
        {
            return this.CreateSession();
        }
    }

    /// <summary>
    /// Baut die Aufnahme aus den aktuellen Feldern. Muss innerhalb der Sperre laufen.
    /// </summary>
    private RecordingSession CreateSession() => new()
    {
        Id = this.sessionId ?? string.Empty,
        StartedAt = this.startedAt ?? default,
        StoppedAt = this.stoppedAt,
        IsActive = this.sessionId is not null && this.stoppedAt is null,
        Exchanges = [.. this.exchanges.Values.OrderBy(entry => entry.Sequence)],
    };

    /// <summary>
    /// Legt einen Eintrag für einen eintreffenden Request an.
    /// </summary>
    /// <param name="method">HTTP-Methode.</param>
    /// <param name="path">Pfad des Requests.</param>
    /// <param name="body">Der Request-Body.</param>
    /// <param name="authenticationId">Id der gewählten Authentifizierung oder <c>null</c>.</param>
    /// <returns>
    /// Der neue Eintrag oder <c>null</c>, wenn gerade nicht aufgezeichnet wird.
    /// </returns>
    public RecordedExchange? BeginExchange(
        string method,
        string path,
        string body,
        string? authenticationId)
    {
        lock (this.gate)
        {
            if (this.sessionId is null || this.stoppedAt is not null)
            {
                return null;
            }

            var (commandName, commandType) = ReadCommandType(body);

            RecordedExchange exchange = new()
            {
                Id = Guid.NewGuid().ToString("N"),
                Sequence = ++this.sequence,
                RequestTimestamp = DateTimeOffset.UtcNow,
                CommandName = commandName,
                CommandType = commandType,
                RequestBody = body,
                Method = method,
                Path = path,
                AuthenticationId = authenticationId,
            };

            this.exchanges[exchange.Id] = exchange;

            return exchange;
        }
    }

    /// <summary>
    /// Ersetzt den Request-Body eines aufgezeichneten Aufrufs.
    /// </summary>
    /// <param name="exchangeId">Id des Eintrags.</param>
    /// <param name="body">Der neue Request-Body.</param>
    /// <returns>
    /// Das Ergebnis mit dem aktuellen Stand der Aufnahme. Wird die Änderung abgelehnt,
    /// steht der Grund in <see cref="UpdateRecordingResult.Error"/>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Das Feld <c>$type</c> darf sich nicht ändern: Es bestimmt, welches Command die
    /// Anwendung ausführt. Ein anderer Wert würde den Eintrag zu einem anderen Command
    /// machen – die Aufnahme soll aber denselben Aufruf mit anderen Parametern zeigen.
    /// </para>
    /// <para>
    /// Der Body muss gültiges JSON sein. Die inhaltliche Prüfung gegen das JSON-Schema
    /// übernimmt die UI; hier wird nur die Struktur geprüft, weil der Explorer die
    /// Command-Typen der Anwendung nicht kennt.
    /// </para>
    /// </remarks>
    public UpdateRecordingResult UpdateRequestBody(string exchangeId, string body)
    {
        lock (this.gate)
        {
            // Bewusst kein ThrowIfNullOrWhiteSpace: Eine fehlende Id ist ein Bedienfehler
            // der UI, kein Serverfehler. Ein 500 mit Stacktrace wäre für den Benutzer
            // unbrauchbar – die UI zeigt stattdessen diese Meldung.
            if (string.IsNullOrWhiteSpace(exchangeId))
            {
                return this.Reject("Es ist kein Aufruf ausgewählt.");
            }

            if (!this.exchanges.TryGetValue(exchangeId, out var exchange))
            {
                return this.Reject("Der Aufruf existiert nicht mehr.");
            }

            if (string.IsNullOrWhiteSpace(body))
            {
                return this.Reject("Der Request-Body ist leer.");
            }

            string? newType;

            try
            {
                using var document = JsonDocument.Parse(body);

                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return this.Reject("Der Request-Body muss ein JSON-Objekt sein.");
                }

                newType = document.RootElement.TryGetProperty("$type", out var typeElement)
                           && typeElement.ValueKind == JsonValueKind.String
                    ? typeElement.GetString()
                    : null;
            }
            catch (JsonException exception)
            {
                return this.Reject($"Der Request-Body ist kein gültiges JSON: {exception.Message}");
            }

            if (string.IsNullOrWhiteSpace(newType))
            {
                return this.Reject("Das Feld '$type' fehlt im Request-Body.");
            }

            if (!string.Equals(newType, exchange.CommandType, StringComparison.Ordinal))
            {
                return this.Reject(
                    "Das Feld '$type' darf nicht geändert werden – es bestimmt das Command.");
            }

            exchange.RequestBody = body;

            return new UpdateRecordingResult
            {
                Success = true,
                Session = this.CreateSession(),
            };
        }
    }

    /// <summary>
    /// Baut eine Ablehnung mit dem aktuellen Stand der Aufnahme.
    /// </summary>
    private UpdateRecordingResult Reject(string reason) => new()
    {
        Success = false,
        Error = reason,
        Session = this.CreateSession(),
    };

    /// <summary>
    /// Ergänzt einen Eintrag um die Antwort.
    /// </summary>
    /// <param name="exchange">Der Eintrag aus <see cref="BeginExchange"/>.</param>
    /// <param name="statusCode">HTTP-Statuscode der Antwort.</param>
    /// <param name="body">Der Response-Body.</param>
    /// <param name="contentType">Content-Type der Antwort.</param>
    /// <param name="error">Fehlermeldung, falls die Verarbeitung abgebrochen ist.</param>
    public void CompleteExchange(
        RecordedExchange exchange,
        int statusCode,
        string? body,
        string? contentType,
        string? error = null)
    {
        ArgumentNullException.ThrowIfNull(exchange);

        var completedAt = DateTimeOffset.UtcNow;

        exchange.ResponseTimestamp = completedAt;
        exchange.DurationMs = (completedAt - exchange.RequestTimestamp).TotalMilliseconds;
        exchange.StatusCode = statusCode;
        exchange.ResponseBody = body;
        exchange.ContentType = contentType;
        exchange.Error = error;
    }

    /// <summary>
    /// Liest den Command-Namen aus dem <c>$type</c>-Feld des Bodys.
    /// </summary>
    /// <remarks>
    /// Der Wert hat die Form <c>Namespace.Klasse, Assembly</c>. Fehlt das Feld oder ist der
    /// Body kein JSON, bleibt der Name leer – der Eintrag wird trotzdem aufgezeichnet.
    /// </remarks>
    private static (string Name, string? Type) ReadCommandType(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return (string.Empty, null);
        }

        try
        {
            using var document = JsonDocument.Parse(body);

            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("$type", out var typeElement)
                || typeElement.ValueKind != JsonValueKind.String)
            {
                return (string.Empty, null);
            }

            var type = typeElement.GetString();

            if (string.IsNullOrWhiteSpace(type))
            {
                return (string.Empty, null);
            }

            // "Namespace.Klasse, Assembly" → "Klasse"
            var withoutAssembly = type.Split(',')[0].Trim();
            var name = withoutAssembly[(withoutAssembly.LastIndexOf('.') + 1)..];

            return (name, type);
        }
        catch (JsonException)
        {
            return (string.Empty, null);
        }
    }

    /// <summary>
    /// Baut den Zustand aus den aktuellen Feldern. Muss innerhalb der Sperre laufen.
    /// </summary>
    private RecordingStatus CreateStatus() => new()
    {
        IsActive = this.sessionId is not null && this.stoppedAt is null,
        HasSession = this.sessionId is not null,
        SessionId = this.sessionId,
        StartedAt = this.startedAt,
        StoppedAt = this.stoppedAt,
        ExchangeCount = this.exchanges.Count,
    };
}
