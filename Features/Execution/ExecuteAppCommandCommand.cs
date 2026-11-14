using System.Text;
using ATK.Command.Explorer.Commands;
using ATK.Command.Explorer.Configuration;
using ATK.Command.Explorer.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace ATK.Command.Explorer.Features.Execution;

/// <summary>
/// Führt ein Command der Anwendung aus und setzt dabei die gewählte Authentifizierung.
/// </summary>
/// <remarks>
/// <para>
/// Warum ein Proxy und kein direkter Aufruf aus dem Browser: Der Token darf den Server
/// nicht verlassen. Die UI kennt nur die <c>Id</c> der Authentifizierung; dieses Command
/// holt den Token, setzt den passenden Header und leitet den Request an den
/// Command-Endpunkt der Anwendung weiter.
/// </para>
/// <para>
/// Der Body wird unverändert durchgereicht, die Antwort ebenfalls – inklusive Fehlerstatus
/// und <c>ProblemDetails</c>.
/// </para>
/// </remarks>
public sealed class ExecuteAppCommandCommand : IExplorerCommand<AppCommandResult>
{
    /// <summary>
    /// Das vollständige Command-JSON inklusive <c>$type</c>.
    /// </summary>
    /// <remarks>
    /// Bewusst als String: Der Explorer kennt die Command-Typen der Anwendung nicht und
    /// darf sie nicht deserialisieren. Er reicht den Text unverändert weiter.
    /// </remarks>
    public string Payload { get; set; } = string.Empty;

    /// <summary>
    /// Id der gewählten Authentifizierung oder <c>null</c>.
    /// </summary>
    public string? AuthenticationId { get; set; }
}

/// <summary>
/// Ergebnis einer Command-Ausführung, inklusive HTTP-Metadaten.
/// </summary>
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

/// <summary>
/// Führt <see cref="ExecuteAppCommandCommand"/> aus.
/// </summary>
public sealed class ExecuteAppCommandCommandHandler
    : IExplorerCommandHandler<ExecuteAppCommandCommand, AppCommandResult>
{
    private readonly CommandAuthenticationResolver resolver;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly IOptions<CommandExplorerOptions> options;

    /// <summary>Erzeugt den Handler.</summary>
    /// <param name="resolver">Löst die Authentifizierung auf.</param>
    /// <param name="httpClientFactory">Erzeugt den HTTP-Client.</param>
    /// <param name="httpContextAccessor">Liefert die aktuelle Anfrage für die Basis-URL.</param>
    /// <param name="options">Die Explorer-Optionen.</param>
    public ExecuteAppCommandCommandHandler(
        CommandAuthenticationResolver resolver,
        IHttpClientFactory httpClientFactory,
        IHttpContextAccessor httpContextAccessor,
        IOptions<CommandExplorerOptions> options)
    {
        this.resolver = resolver;
        this.httpClientFactory = httpClientFactory;
        this.httpContextAccessor = httpContextAccessor;
        this.options = options;
    }

    /// <inheritdoc />
    public async Task<AppCommandResult> HandleAsync(
        ExecuteAppCommandCommand command,
        CancellationToken cancellationToken)
    {
        long started = Environment.TickCount64;

        IReadOnlyDictionary<string, string> authHeaders =
            await this.resolver.ResolveHeadersAsync(command.AuthenticationId, cancellationToken);

        using HttpRequestMessage request = new(HttpMethod.Post, this.BuildCommandUri());

        request.Content = new StringContent(
            command.Payload,
            Encoding.UTF8,
            "application/json");

        foreach (KeyValuePair<string, string> header in authHeaders)
        {
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        using HttpClient client = this.httpClientFactory.CreateClient(nameof(ExecuteAppCommandCommandHandler));

        using HttpResponseMessage response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        string body = await response.Content.ReadAsStringAsync(cancellationToken);

        return new AppCommandResult
        {
            Status = (int)response.StatusCode,
            StatusText = response.ReasonPhrase ?? string.Empty,
            Success = response.IsSuccessStatusCode,
            DurationMs = Environment.TickCount64 - started,
            Body = body,
            ContentType = response.Content.Headers.ContentType?.ToString(),
        };
    }

    /// <summary>
    /// Baut die absolute URI des Command-Endpunkts der Anwendung.
    /// </summary>
    /// <remarks>
    /// Bevorzugt <see cref="CommandExplorerOptions.CommandBaseUrl"/>. Ist der Wert leer,
    /// wird die Adresse aus der aktuellen Anfrage ermittelt – dadurch funktioniert der Proxy
    /// auch hinter einem Reverse-Proxy ohne Konfiguration.
    /// </remarks>
    private Uri BuildCommandUri()
    {
        string path = "/" + this.options.Value.CommandEndpoint.Trim('/');

        string? configured = this.options.Value.CommandBaseUrl;

        if (!string.IsNullOrWhiteSpace(configured))
        {
            return new Uri($"{configured.TrimEnd('/')}{path}");
        }

        HttpRequest? current = this.httpContextAccessor.HttpContext?.Request;

        if (current is null)
        {
            throw new InvalidOperationException(
                "Die Basis-URL des Command-Endpunkts ist nicht ermittelbar. Setze "
                + $"'{CommandExplorerOptions.SectionName}:CommandBaseUrl' in der Konfiguration.");
        }

        return new Uri($"{current.Scheme}://{current.Host}{path}");
    }
}
