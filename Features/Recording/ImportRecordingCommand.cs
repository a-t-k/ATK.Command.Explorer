using System.Text.Json;
using ATK.Command.Explorer.Commands;
using ATK.Command.Explorer.Models;
using ATK.Command.Explorer.Services;

namespace ATK.Command.Explorer.Features.Recording;

/// <summary>
/// Übernimmt eine zuvor exportierte Aufnahme.
/// </summary>
/// <remarks>
/// Der Inhalt ist das JSON, das <see cref="ExportRecordingCommand"/> erzeugt hat. Die
/// Aufnahme wird als beendet übernommen; eine laufende Aufnahme wird dabei verworfen.
/// </remarks>
public sealed class ImportRecordingCommand : IExplorerCommand<RecordingStatus>
{
    /// <summary>
    /// Das JSON der zu importierenden Aufnahme.
    /// </summary>
    public string Json { get; set; } = string.Empty;
}

/// <summary>
/// Führt <see cref="ImportRecordingCommand"/> aus.
/// </summary>
public sealed class ImportRecordingCommandHandler
    : IExplorerCommandHandler<ImportRecordingCommand, RecordingStatus>
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly CommandRecorder recorder;

    /// <summary>Erzeugt den Handler.</summary>
    /// <param name="recorder">Der Recorder.</param>
    public ImportRecordingCommandHandler(CommandRecorder recorder)
    {
        this.recorder = recorder;
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// Wird geworfen, wenn der Inhalt kein gültiges Aufnahme-JSON ist.
    /// </exception>
    public async Task<RecordingStatus> HandleAsync(
        ImportRecordingCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Json))
        {
            throw new InvalidOperationException("Die Datei enthält kein JSON.");
        }

        RecordingSession? session;

        try
        {
            session = JsonSerializer.Deserialize<RecordingSession>(
                command.Json,
                SerializerOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"Die Datei ist kein gültiges Aufnahme-JSON: {exception.Message}",
                exception);
        }

        if (session is null)
        {
            throw new InvalidOperationException("Die Datei enthält keine Aufnahme.");
        }

        return await Task.FromResult(this.recorder.Import(session));
    }
}
