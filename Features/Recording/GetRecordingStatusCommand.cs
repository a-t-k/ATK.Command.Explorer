using ATK.Command.Explorer.Commands;
using ATK.Command.Explorer.Models;
using ATK.Command.Explorer.Services;

namespace ATK.Command.Explorer.Features.Recording;

/// <summary>
/// Liefert den Zustand der Aufnahme ohne die Einträge.
/// </summary>
/// <remarks>
/// Für das Polling gedacht: Die UI fragt während einer laufenden Aufnahme regelmäßig nach,
/// ob neue Einträge dazugekommen sind, und lädt die Einträge erst beim Stoppen.
/// </remarks>
public sealed class GetRecordingStatusCommand : IExplorerCommand<RecordingStatus>
{
}

/// <summary>
/// Führt <see cref="GetRecordingStatusCommand"/> aus.
/// </summary>
public sealed class GetRecordingStatusCommandHandler
    : IExplorerCommandHandler<GetRecordingStatusCommand, RecordingStatus>
{
    private readonly CommandRecorder recorder;

    /// <summary>Erzeugt den Handler.</summary>
    /// <param name="recorder">Der Recorder.</param>
    public GetRecordingStatusCommandHandler(CommandRecorder recorder)
    {
        this.recorder = recorder;
    }

    /// <inheritdoc />
    public async Task<RecordingStatus> HandleAsync(
        GetRecordingStatusCommand command,
        CancellationToken cancellationToken)
    {
        return await Task.FromResult(this.recorder.GetStatus());
    }
}
