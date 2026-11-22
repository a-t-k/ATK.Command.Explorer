using System.Text;
using ATK.Command.Explorer.Configuration;
using ATK.Command.Explorer.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace ATK.Command.Explorer.Recording;

/// <summary>
/// Schreibt alle Requests auf den Command-Endpunkt mit, solange eine Aufnahme läuft.
/// </summary>
/// <remarks>
/// <para>
/// Die Middleware ist ein reiner Mitschnitt: Sie liest den Request-Body, reicht ihn
/// unverändert weiter, fängt die Antwort ab und schickt sie ebenfalls unverändert zum
/// Client. Die Anwendung verhält sich dadurch exakt wie ohne Aufnahme.
/// </para>
/// <para>
/// Aufgezeichnet wird nur, was auf <see cref="CommandExplorerOptions.CommandEndpoint"/>
/// eintrifft – der Explorer selbst und statische Dateien bleiben außen vor. Sonst würde
/// die Statusabfrage der UI ihre eigene Aufnahme erzeugen.
/// </para>
/// <para>
/// Der Response-Body wird über einen <see cref="MemoryStream"/> abgefangen und danach
/// erneut in den Antwortstrom geschrieben. Das ist nur für JSON-Antworten sinnvoll; bei
/// großen Downloads (PDF, ZIP) wird deshalb auf das Mitschnitt des Bodys verzichtet und
/// nur der Status notiert.
/// </para>
/// </remarks>
public sealed class CommandRecordingMiddleware
{
    /// <summary>
    /// Grenze, bis zu der ein Response-Body mitgeschnitten wird.
    /// </summary>
    /// <remarks>
    /// Downloads des Explorers (PDF, ZIP) liegen schnell im Megabyte-Bereich. Sie werden
    /// nicht aufgezeichnet, weil sie für die Analyse von Command-Aufrufen nichts beitragen
    /// und den Speicher füllen würden.
    /// </remarks>
    private const int MaxRecordedBodyBytes = 1024 * 1024;

    private readonly RequestDelegate next;
    private readonly IOptions<CommandExplorerOptions> options;

    /// <summary>Erzeugt die Middleware.</summary>
    /// <param name="next">Die nächste Middleware.</param>
    /// <param name="options">Die Explorer-Optionen.</param>
    public CommandRecordingMiddleware(
        RequestDelegate next,
        IOptions<CommandExplorerOptions> options)
    {
        this.next = next;
        this.options = options;
    }

    /// <summary>
    /// Führt die Middleware aus.
    /// </summary>
    /// <param name="context">Der HTTP-Kontext.</param>
    /// <param name="recorder">Der Recorder.</param>
    public async Task InvokeAsync(HttpContext context, CommandRecorder recorder)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(recorder);

        if (!recorder.IsActive || !this.IsCommandRequest(context.Request))
        {
            await this.next(context);
            return;
        }

        var body = await ReadRequestBodyAsync(context.Request);

        var authenticationId = context.Request.Headers[
            this.options.Value.AuthenticationHeaderName].FirstOrDefault();

        var exchange = recorder.BeginExchange(
            context.Request.Method,
            context.Request.Path.Value ?? string.Empty,
            body,
            authenticationId);

        if (exchange is null)
        {
            await this.next(context);
            return;
        }

        // Antwort abfangen: Der Body wird in einen eigenen Stream umgeleitet und nach dem
        // nächsten Middleware-Schritt zurück in die echte Antwort kopiert.
        var originalBody = context.Response.Body;

        using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await this.next(context);

            var responseBody = await ReadResponseBodyAsync(context.Response, buffer);

            recorder.CompleteExchange(
                exchange,
                context.Response.StatusCode,
                responseBody,
                context.Response.ContentType);

            buffer.Position = 0;
            await buffer.CopyToAsync(originalBody, context.RequestAborted);
        }
        catch (Exception exception)
        {
            recorder.CompleteExchange(
                exchange,
                context.Response.StatusCode,
                null,
                context.Response.ContentType,
                exception.Message);

            throw;
        }
        finally
        {
            context.Response.Body = originalBody;
        }
    }

    /// <summary>
    /// Prüft, ob der Request auf den Command-Endpunkt zielt.
    /// </summary>
    /// <remarks>
    /// Verglichen wird der Pfad ohne führenden Schrägstrich und ohne Beachtung der
    /// Groß-/Kleinschreibung. Ein Query-String bleibt unberücksichtigt.
    /// </remarks>
    private bool IsCommandRequest(HttpRequest request)
    {
        var endpoint = this.options.Value.CommandEndpoint.Trim('/');

        if (endpoint.Length == 0)
        {
            return false;
        }

        var path = request.Path.Value?.Trim('/') ?? string.Empty;

        return string.Equals(path, endpoint, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Liest den Request-Body und setzt ihn für die nächste Middleware zurück.
    /// </summary>
    /// <remarks>
    /// Ohne das Zurücksetzen wäre der Body für den ModelBinder leer, weil ein Stream nur
    /// einmal gelesen werden kann.
    /// </remarks>
    private static async Task<string> ReadRequestBodyAsync(HttpRequest request)
    {
        if (request.ContentLength is null or 0)
        {
            return string.Empty;
        }

        request.EnableBuffering();

        using StreamReader reader = new(
            request.Body,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            leaveOpen: true);

        var body = await reader.ReadToEndAsync();

        request.Body.Position = 0;

        return body;
    }

    /// <summary>
    /// Liest den abgefangenen Response-Body, sofern er klein genug ist.
    /// </summary>
    /// <param name="response">Die Antwort.</param>
    /// <param name="buffer">Der Puffer mit dem Body.</param>
    /// <returns>Der Body als Text oder <c>null</c>, wenn er nicht aufgezeichnet wird.</returns>
    private static async Task<string?> ReadResponseBodyAsync(
        HttpResponse response,
        MemoryStream buffer)
    {
        if (buffer.Length == 0 || buffer.Length > MaxRecordedBodyBytes)
        {
            return null;
        }

        // Nur textuelle Antworten sind für die Analyse interessant.
        var contentType = response.ContentType;

        if (contentType is not null
            && !contentType.Contains("json", StringComparison.OrdinalIgnoreCase)
            && !contentType.Contains("text", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        buffer.Position = 0;

        using StreamReader reader = new(buffer, Encoding.UTF8, leaveOpen: true);

        var body = await reader.ReadToEndAsync();

        buffer.Position = 0;

        return body;
    }
}
