using ATK.Command.Explorer.Commands;
using ATK.Command.Explorer.Models;
using ATK.Command.Explorer.Services;

namespace ATK.Command.Explorer.Features.Recording;

/// <summary>
/// Startet eine neue Aufnahme und verwirft eine vorhandene.
/// </summary>
public sealed class StartRecordingCommand : IExplorerCommand<RecordingStatus>
{
}

/// <summary>
/// Führt <see cref="StartRecordingCommand"/> aus.
/// </summary>
public sealed class StartRecordingCommandHandler
    : IExplorerCommandHandler<StartRecordingCommand, RecordingStatus>
{
    private readonly CommandRecorder recorder;

    /// <summary>Erzeugt den Handler.</summary>
    /// <param name="recorder">Der Recorder.</param>
    public StartRecordingCommandHandler(CommandRecorder recorder)
    {
        this.recorder = recorder;
    }

    /// <inheritdoc />
    public async Task<RecordingStatus> HandleAsync(
        StartRecordingCommand command,
        CancellationToken cancellationToken)
    {
        return await Task.FromResult(this.recorder.Start());
    }
}
