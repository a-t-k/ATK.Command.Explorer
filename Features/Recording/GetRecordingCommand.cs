using ATK.Command.Explorer.Commands;
using ATK.Command.Explorer.Models;
using ATK.Command.Explorer.Services;

namespace ATK.Command.Explorer.Features.Recording;

/// <summary>
/// Liefert die aktuelle Aufnahme mit allen Einträgen.
/// </summary>
/// <remarks>
/// Die Einträge sind aufsteigend nach ihrer laufenden Nummer sortiert – also in der
/// Reihenfolge, in der die Requests eingetroffen sind.
/// </remarks>
public sealed class GetRecordingCommand : IExplorerCommand<RecordingSession>
{
}

/// <summary>
/// Führt <see cref="GetRecordingCommand"/> aus.
/// </summary>
public sealed class GetRecordingCommandHandler
    : IExplorerCommandHandler<GetRecordingCommand, RecordingSession>
{
    private readonly CommandRecorder recorder;

    /// <summary>Erzeugt den Handler.</summary>
    /// <param name="recorder">Der Recorder.</param>
    public GetRecordingCommandHandler(CommandRecorder recorder)
    {
        this.recorder = recorder;
    }

    /// <inheritdoc />
    public async Task<RecordingSession> HandleAsync(
        GetRecordingCommand command,
        CancellationToken cancellationToken)
    {
        return await Task.FromResult(this.recorder.GetSession());
    }
}
