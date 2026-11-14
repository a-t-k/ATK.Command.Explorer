using System.IO.Compression;
using System.Text;

namespace ATK.Command.Explorer.Documentation;

/// <summary>
/// Packt die erzeugten Markdown-Dateien in ein ZIP-Archiv.
/// </summary>
/// <remarks>
/// Das Archiv enthält zusätzlich eine leere <c>.obsidian/app.json</c>, damit Obsidian den
/// Ordner beim Öffnen direkt als Vault erkennt.
/// </remarks>
public static class VaultArchiveBuilder
{
    /// <summary>
    /// Erzeugt das ZIP-Archiv.
    /// </summary>
    /// <param name="files">Dateiname → Inhalt.</param>
    /// <param name="rootFolder">Ordnername im Archiv.</param>
    /// <returns>Der Inhalt des ZIP-Archivs.</returns>
    public static byte[] Build(IReadOnlyDictionary<string, string> files, string rootFolder)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootFolder);

        using MemoryStream stream = new();

        using (ZipArchive archive = new(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach ((var name, var content) in files)
            {
                WriteEntry(archive, $"{rootFolder}/{name}", content);
            }

            // Markiert den Ordner als Obsidian-Vault.
            WriteEntry(archive, $"{rootFolder}/.obsidian/app.json", "{}");
        }

        return stream.ToArray();
    }

    private static void WriteEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);

        using var entryStream = entry.Open();
        using StreamWriter writer = new(entryStream, new UTF8Encoding(false));
        writer.Write(content);
    }
}
