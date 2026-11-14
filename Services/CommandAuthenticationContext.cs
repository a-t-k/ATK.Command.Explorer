namespace ATK.Command.Explorer.Services;

/// <summary>
/// Stellt die für die aktuelle Anfrage gewählte Authentifizierung bereit.
/// </summary>
/// <remarks>
/// <para>
/// Explorer-Commands, die selbst HTTP-Aufrufe machen (etwa ein Command, das eine externe API
/// abfragt), können die Header hier abrufen, ohne dass der Token durch den Command-Payload
/// läuft.
/// </para>
/// <para>
/// Der Kontext ist an den asynchronen Ablauf gebunden (<see cref="AsyncLocal{T}"/>), sodass
/// parallele Anfragen sich nicht gegenseitig überschreiben.
/// </para>
/// </remarks>
public sealed class CommandAuthenticationContext
{
    private static readonly AsyncLocal<CommandAuthenticationContext?> Current = new();

    /// <summary>
    /// Erzeugt einen Kontext.
    /// </summary>
    /// <param name="authenticationId">Die gewählte Id oder <c>null</c>.</param>
    /// <param name="headers">Die daraus gebauten Header.</param>
    public CommandAuthenticationContext(
        string? authenticationId,
        IReadOnlyDictionary<string, string> headers)
    {
        this.AuthenticationId = authenticationId;
        this.Headers = headers;
    }

    /// <summary>Die gewählte Authentifizierungs-Id oder <c>null</c>.</summary>
    public string? AuthenticationId { get; }

    /// <summary>Die daraus gebauten Header.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; }

    /// <summary>
    /// Der Kontext der aktuellen Anfrage.
    /// </summary>
    /// <remarks>
    /// <c>null</c>, wenn kein Kontext gesetzt ist – etwa bei direkter Ausführung außerhalb
    /// einer HTTP-Anfrage.
    /// </remarks>
    public static CommandAuthenticationContext? CurrentContext => Current.Value;

    /// <summary>
    /// Setzt den Kontext für den aktuellen Ablauf.
    /// </summary>
    /// <param name="context">Der zu setzende Kontext.</param>
    /// <returns>
    /// Ein <see cref="IDisposable"/>, das den vorherigen Kontext wiederherstellt.
    /// </returns>
    /// <example>
    /// <code>
    /// using (CommandAuthenticationContext.Begin(context))
    /// {
    ///     await this.dispatcher.ExecuteAsync(command, cancellationToken);
    /// }
    /// </code>
    /// </example>
    public static IDisposable Begin(CommandAuthenticationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var previous = Current.Value;
        Current.Value = context;

        return new Restore(previous);
    }
    /// <summary>
    /// Stellt den vorherigen Kontext wieder her.
    /// </summary>
    private sealed class Restore(CommandAuthenticationContext? previous) : IDisposable
    {
        public void Dispose() => Current.Value = previous;
    }
}
