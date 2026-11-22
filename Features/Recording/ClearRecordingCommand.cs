using ATK.Command.Explorer.Commands;
using ATK.Command.Explorer.Models;
using ATK.Command.Explorer.Services;

namespace ATK.Command.Explorer.Features.Recording;

/// <summary>
/// Verwirft die aktuelle Aufnahme vollständig.
/// </summary>
public sealed class ClearRecordingCommand : IExplorerCommand<RecordingStatus>
{
}

/// <summary>
/// Führt <see cref="ClearRecordingCommand"/> aus.
/// </summary>
public sealed class ClearRecordingCommandHandler
    : IExplorerCommandHandler<ClearRecordingCommand, RecordingStatus>
{
    private readonly CommandRecorder recorder;

    /// <summary>Erzeugt den Handler.</summary>
    /// <param name="recorder">Der Recorder.</param>
    public ClearRecordingCommandHandler(CommandRecorder recorder)
    {
        this.recorder = recorder;
    }

    /// <inheritdoc />
    public async Task<RecordingStatus> HandleAsync(
        ClearRecordingCommand command,
        CancellationToken cancellationToken)
    {
        return await Task.FromResult(this.recorder.Clear());
    }
}
