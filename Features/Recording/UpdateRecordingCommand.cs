using ATK.Command.Explorer.Commands;
using ATK.Command.Explorer.Models;
using ATK.Command.Explorer.Services;

namespace ATK.Command.Explorer.Features.Recording;

/// <summary>
/// Ersetzt den Request-Body eines aufgezeichneten Aufrufs.
/// </summary>
/// <remarks>
/// <para>
/// Damit lässt sich ein aufgezeichneter Aufruf mit anderen Parametern erneut senden, ohne
/// die Aufnahme zu verlassen. Das Feld <c>$type</c> bleibt unverändert – es bestimmt,
/// welches Command die Anwendung ausführt.
/// </para>
/// <para>
/// Die Prüfung gegen das JSON-Schema des Commands übernimmt die UI: Nur der Explorer
/// kennt die Metadaten der Anwendungs-Commands, und nur dort liegt das Schema bereits vor.
/// </para>
/// </remarks>
public sealed class UpdateRecordingCommand : IExplorerCommand<UpdateRecordingResult>
{
    /// <summary>Id des zu ändernden Eintrags.</summary>
    public string ExchangeId { get; set; } = string.Empty;

    /// <summary>Der neue Request-Body als JSON-Text.</summary>
    public string RequestBody { get; set; } = string.Empty;
}

/// <summary>
/// Führt <see cref="UpdateRecordingCommand"/> aus.
/// </summary>
public sealed class UpdateRecordingCommandHandler
    : IExplorerCommandHandler<UpdateRecordingCommand, UpdateRecordingResult>
{
    private readonly CommandRecorder recorder;

    /// <summary>Erzeugt den Handler.</summary>
    /// <param name="recorder">Der Recorder.</param>
    public UpdateRecordingCommandHandler(CommandRecorder recorder)
    {
        this.recorder = recorder;
    }

    /// <inheritdoc />
    public async Task<UpdateRecordingResult> HandleAsync(
        UpdateRecordingCommand command,
        CancellationToken cancellationToken)
    {
        return await Task.FromResult(
            this.recorder.UpdateRequestBody(command.ExchangeId, command.RequestBody));
    }
}
