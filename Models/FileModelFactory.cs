using ATK.Command.Communication.Models;

namespace ATK.Command.Explorer.Models;

/// <summary>
/// Erzeugt <see cref="FileModel"/>-Instanzen für Command-Ergebnisse.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="FileModel"/> kommt aus dem Paket <c>ATK.Command.Communication.Models</c> und
/// kennt zwei Wege, den Inhalt zu halten: <see cref="FileModel.Content"/> (Bytes) und
/// <see cref="FileModel.Base64EncodedContent"/> (Text). Für die Übertragung als JSON wird
/// ausschließlich die Base64-Variante gebraucht.
/// </para>
/// <para>
/// <see cref="FileModel.ToBase64"/> setzt <see cref="FileModel.Base64EncodedContent"/> und
/// leert <see cref="FileModel.Content"/>. Dadurch landet der Inhalt nur einmal in der
/// Antwort – sonst würde er als Byte-Array ein zweites Mal serialisiert.
/// </para>
/// </remarks>
internal static class FileModelFactory
{
    /// <summary>
    /// Erzeugt ein <see cref="FileModel"/> aus Rohbytes.
    /// </summary>
    /// <param name="content">Der Dateiinhalt.</param>
    /// <param name="contentType">Der MIME-Typ.</param>
    /// <param name="name">Der Dateiname inklusive Endung.</param>
    /// <returns>Das Modell mit Base64-kodiertem Inhalt.</returns>
    public static FileModel Create(byte[] content, string contentType, string name)
    {
        var file = new FileModel
        {
            Name = name,
            ContentType = contentType,
            Content = content,
        };

        file.ToBase64();

        return file;
    }
}
