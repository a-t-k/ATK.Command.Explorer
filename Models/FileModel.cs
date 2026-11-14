namespace ATK.Command.Explorer.Models;

/// <summary>
/// Eine Datei, die die UI speichern soll.
/// </summary>
/// <remarks>
/// <para>
/// Commands, die eine Datei liefern (Vault-ZIP, PDF, TypeScript-ZIP, Framework-ZIP), geben
/// dieses Modell zurück. Die UI erkennt es am Feld <c>base64EncodedContent</c> und speichert
/// die Datei über <c>CommandProcessor.doDownload()</c>.
/// </para>
/// <para>
/// Der Umweg über Base64 ist nötig, weil der Command-Endpunkt JSON liefert – ein
/// <c>FileResult</c> wäre kein Command-Ergebnis mehr.
/// </para>
/// </remarks>
public sealed class FileModel
{
    /// <summary>Dateiname inklusive Endung, z. B. <c>Command-Referenz.pdf</c>.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>MIME-Typ, z. B. <c>application/pdf</c>.</summary>
    public string ContentType { get; init; } = "application/octet-stream";

    /// <summary>Der Dateiinhalt, Base64-kodiert.</summary>
    public string Base64EncodedContent { get; init; } = string.Empty;

    /// <summary>
    /// Erzeugt ein <see cref="FileModel"/> aus Rohbytes.
    /// </summary>
    /// <param name="content">Der Dateiinhalt.</param>
    /// <param name="contentType">Der MIME-Typ.</param>
    /// <param name="name">Der Dateiname.</param>
    /// <returns>Das Modell mit Base64-kodiertem Inhalt.</returns>
    public static FileModel FromBytes(byte[] content, string contentType, string name) =>
        new()
        {
            Name = name,
            ContentType = contentType,
            Base64EncodedContent = Convert.ToBase64String(content),
        };
}
