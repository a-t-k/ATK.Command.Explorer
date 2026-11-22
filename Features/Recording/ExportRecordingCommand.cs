using System.Text;
using System.Text.Json;
using ATK.Command.Communication.Models;
using ATK.Command.Explorer.Commands;
using ATK.Command.Explorer.Models;
using ATK.Command.Explorer.Services;

namespace ATK.Command.Explorer.Features.Recording;

/// <summary>
/// Liefert die aktuelle Aufnahme als JSON-Datei zum Herunterladen.
/// </summary>
/// <remarks>
/// Das Format ist dasselbe, das <see cref="ImportRecordingCommand"/> wieder einliest –
/// eine Aufnahme lässt sich damit sichern, weitergeben und später erneut öffnen.
/// </remarks>
public sealed class ExportRecordingCommand : IExplorerCommand<FileModel>
{
}

/// <summary>
/// Führt <see cref="ExportRecordingCommand"/> aus.
/// </summary>
public sealed class ExportRecordingCommandHandler
    : IExplorerCommandHandler<ExportRecordingCommand, FileModel>
{
    /// <summary>Dateiendung der exportierten Aufnahme.</summary>
    private const string FileExtension = ".json";

    /// <summary>MIME-Typ der exportierten Aufnahme.</summary>
    private const string ContentType = "application/json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly CommandRecorder recorder;

    /// <summary>Erzeugt den Handler.</summary>
    /// <param name="recorder">Der Recorder.</param>
    public ExportRecordingCommandHandler(CommandRecorder recorder)
    {
        this.recorder = recorder;
    }

    /// <inheritdoc />
    public async Task<FileModel> HandleAsync(
        ExportRecordingCommand command,
        CancellationToken cancellationToken)
    {
        var session = this.recorder.GetSession();

        var json = JsonSerializer.Serialize(session, SerializerOptions);
        var bytes = Encoding.UTF8.GetBytes(json);

        var name = string.IsNullOrWhiteSpace(session.Id)
            ? "aufnahme"
            : $"aufnahme-{session.Id}";

        return await Task.FromResult(
            FileModelFactory.Create(bytes, ContentType, name + FileExtension));
    }
}
