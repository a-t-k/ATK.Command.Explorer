using System.IO.Compression;
using System.Text;

namespace ATK.Command.Explorer.Documentation;

/// <summary>
/// Packt Textdateien in ein ZIP-Archiv.
/// </summary>
/// <remarks>
/// Bewusst ohne Kenntnis der Inhalte: Der Aufrufer bestimmt Pfade und Texte. So lässt sich
/// derselbe Baustein für den Obsidian-Vault und für den TypeScript-Export verwenden.
/// </remarks>
public static class ZipArchiveBuilder
{
    /// <summary>
    /// Erzeugt das ZIP-Archiv.
    /// </summary>
    /// <param name="files">Pfad im Archiv → Inhalt.</param>
    /// <param name="rootFolder">Ordnername im Archiv. Leer bedeutet „kein Wurzelordner“.</param>
    /// <returns>Der Inhalt des ZIP-Archivs.</returns>
    public static byte[] Build(IReadOnlyDictionary<string, string> files, string rootFolder)
    {
        ArgumentNullException.ThrowIfNull(files);

        using MemoryStream stream = new();

        using (ZipArchive archive = new(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach ((string name, string content) in files)
            {
                string path = string.IsNullOrWhiteSpace(rootFolder)
                    ? name
                    : $"{rootFolder}/{name}";

                WriteEntry(archive, path, content);
            }
        }

        return stream.ToArray();
    }

    /// <summary>
    /// Schreibt einen einzelnen Text als Archiv-Eintrag.
    /// </summary>
    /// <param name="archive">Das Archiv.</param>
    /// <param name="path">Pfad im Archiv.</param>
    /// <param name="content">Der Inhalt.</param>
    public static void WriteEntry(ZipArchive archive, string path, string content)
    {
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        ZipArchiveEntry entry = archive.CreateEntry(path, CompressionLevel.Optimal);

        using Stream entryStream = entry.Open();
        using StreamWriter writer = new(entryStream, new UTF8Encoding(false));
        writer.Write(content);
    }
}
