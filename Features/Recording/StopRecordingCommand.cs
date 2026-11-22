using ATK.Command.Explorer.Commands;
using ATK.Command.Explorer.Models;
using ATK.Command.Explorer.Services;

namespace ATK.Command.Explorer.Features.Recording;

/// <summary>
/// Beendet die laufende Aufnahme.
/// </summary>
public sealed class StopRecordingCommand : IExplorerCommand<RecordingStatus>
{
}

/// <summary>
/// Führt <see cref="StopRecordingCommand"/> aus.
/// </summary>
public sealed class StopRecordingCommandHandler
    : IExplorerCommandHandler<StopRecordingCommand, RecordingStatus>
{
    private readonly CommandRecorder recorder;

    /// <summary>Erzeugt den Handler.</summary>
    /// <param name="recorder">Der Recorder.</param>
    public StopRecordingCommandHandler(CommandRecorder recorder)
    {
        this.recorder = recorder;
    }

    /// <inheritdoc />
    public async Task<RecordingStatus> HandleAsync(
        StopRecordingCommand command,
        CancellationToken cancellationToken)
    {
        return await Task.FromResult(this.recorder.Stop());
    }
}
