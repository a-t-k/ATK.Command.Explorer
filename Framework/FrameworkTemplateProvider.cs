using System.Reflection;
using System.Text;

namespace ATK.Command.Explorer.Framework;

/// <summary>
/// Stellt die Framework-Vorlagen für den Download bereit.
/// </summary>
/// <remarks>
/// <para>
/// Die Vorlagen liegen als eingebettete Ressourcen im Paket. Sie zeigen, wie der
/// <c>CommandProcessor</c> in ein Frontend-Framework eingebaut wird – derzeit Angular.
/// </para>
/// <para>
/// Der Export enthält bewusst <b>keine</b> generierten Commands: Die kommen aus dem
/// TypeScript-Export und ändern sich mit jedem Build. Hier geht es nur um das Gerüst,
/// das man einmal einbaut.
/// </para>
/// </remarks>
public sealed class FrameworkTemplateProvider
{
    private const string ResourcePrefix = "ATK.Command.Explorer.Framework.Templates.";

    /// <summary>
    /// Trenner zwischen Dateiname und Endung im logischen Ressourcennamen.
    /// </summary>
    /// <remarks>
    /// Nötig, weil Dateinamen selbst Punkte enthalten (<c>base.http.ts</c>). Ohne Trenner
    /// ließe sich nicht entscheiden, wo der Name aufhört und die Endung beginnt.
    /// </remarks>
    private const string ExtensionSeparator = "__";

    private readonly Assembly assembly = typeof(FrameworkTemplateProvider).Assembly;

    /// <summary>
    /// Liefert die verfügbaren Frameworks.
    /// </summary>
    public IReadOnlyList<string> GetFrameworks() =>
        this.GetResourceNames()
            .Select(name => name[ResourcePrefix.Length..])
            .Select(name => name.Split('.', '\\', '/')[0])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

    /// <summary>
    /// Liefert die Vorlagen eines Frameworks.
    /// </summary>
    /// <param name="framework">Name des Frameworks, z. B. <c>angular</c>.</param>
    /// <returns>Dateipfad im Archiv → Inhalt oder <c>null</c>, wenn das Framework unbekannt ist.</returns>
    public IReadOnlyDictionary<string, string>? GetTemplates(string framework)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(framework);

        Dictionary<string, string> files = new(StringComparer.Ordinal);

        foreach (var resource in this.GetResourceNames())
        {
            var path = ToPath(resource, framework);

            if (path is null)
            {
                continue;
            }

            files[path] = this.Read(resource);
        }

        return files.Count == 0 ? null : files;
    }

    /// <summary>
    /// Baut den Dateipfad aus dem logischen Ressourcennamen zurück.
    /// </summary>
    /// <param name="resource">Der logische Name.</param>
    /// <param name="framework">Der gesuchte Framework-Name.</param>
    /// <returns>Etwa <c>config/config.ts</c> oder <c>null</c>, wenn die Ressource nicht passt.</returns>
    /// <remarks>
    /// Der logische Name sieht so aus:
    /// <c>ATK.Command.Explorer.Framework.Templates.angular\config\config__.ts</c>.
    /// Ordner sind durch Backslash getrennt, Dateiname und Endung durch <c>__</c>.
    /// Dadurch bleiben Punkte im Dateinamen (<c>base.http.ts</c>) erhalten.
    /// </remarks>
    private static string? ToPath(string resource, string framework)
    {
        var relative = resource[ResourcePrefix.Length..];

        var separator = relative.IndexOfAny(['\\', '/', '.']);
        if (separator < 0)
        {
            return null;
        }

        if (!relative[..separator].Equals(framework, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var rest = relative[(separator + 1)..];

        var extensionSeparator = rest.LastIndexOf(ExtensionSeparator, StringComparison.Ordinal);
        if (extensionSeparator < 0)
        {
            return rest.Replace('\\', '/');
        }

        var name = rest[..extensionSeparator];

        // `%(Extension)` enthält den führenden Punkt – der Trenner steht davor.
        var extension = rest[(extensionSeparator + ExtensionSeparator.Length)..].TrimStart('.');

        return $"{name.Replace('\\', '/')}.{extension}";
    }

    private string[] GetResourceNames() =>
        this.assembly
            .GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            .ToArray();

    private string Read(string resource)
    {
        using var stream = this.assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Ressource '{resource}' fehlt.");

        using StreamReader reader = new(stream, Encoding.UTF8);

        return reader.ReadToEnd();
    }
}
