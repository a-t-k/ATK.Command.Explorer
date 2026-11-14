namespace ATK.Command.Explorer.Features.Execution;

/// <summary>
/// Ergebnis einer Command-Ausführung, inklusive HTTP-Metadaten.
/// </summary>
/// <remarks>
/// Der Proxy reicht die Antwort der Anwendung unverändert durch. Deshalb stehen hier die
/// HTTP-Metadaten neben dem Body – die UI zeigt Status, Dauer und Content-Type an, ohne
/// die Antwort erneut zu interpretieren.
/// </remarks>
public sealed class AppCommandResult
{
    /// <summary>HTTP-Statuscode.</summary>
    public int Status { get; init; }

    /// <summary>HTTP-Statustext.</summary>
    public string StatusText { get; init; } = string.Empty;

    /// <summary>Gibt an, ob der Aufruf erfolgreich war (2xx).</summary>
    public bool Success { get; init; }

    /// <summary>Dauer in Millisekunden.</summary>
    public long DurationMs { get; init; }

    /// <summary>Der Antwort-Body als Text.</summary>
    public string? Body { get; init; }

    /// <summary>Der Content-Type der Antwort.</summary>
    public string? ContentType { get; init; }
}
