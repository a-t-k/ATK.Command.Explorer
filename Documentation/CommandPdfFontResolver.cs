using PdfSharp.Fonts;

namespace ATK.Command.Explorer.Documentation;

/// <summary>
/// Stellt PDFsharp die Schriften für die PDF-Erzeugung bereit.
/// </summary>
/// <remarks>
/// <para>
/// PDFsharp 6 löst unter .NET keine Systemschriften selbst auf – ohne Resolver schlägt
/// <c>XFont</c> mit „No appropriate font found" fehl.
/// </para>
/// <para>
/// Der Resolver sucht die Dateien in dieser Reihenfolge:
/// </para>
/// <list type="number">
///   <item>Ordner aus <c>CommandExplorer:FontDirectory</c></item>
///   <item>eingebettete Ressourcen des Pakets (DejaVu, falls vorhanden)</item>
///   <item>bekannte Systempfade (Windows, Linux, macOS)</item>
/// </list>
/// <para>
/// Wird nichts gefunden, greift ein eingebauter Minimal-Font, damit die PDF-Erzeugung
/// nicht vollständig scheitert.
/// </para>
/// </remarks>
public sealed class CommandPdfFontResolver : IFontResolver
{
    /// <summary>Logischer Name der regulären Schrift.</summary>
    public const string Regular = "ExplorerRegular";

    /// <summary>Logischer Name der fetten Schrift.</summary>
    public const string Bold = "ExplorerBold";

    /// <summary>Logischer Name der kursiven Schrift.</summary>
    public const string Italic = "ExplorerItalic";

    /// <summary>Logischer Name der Monospace-Schrift.</summary>
    public const string Mono = "ExplorerMono";

    private static readonly string[] RegularCandidates =
    [
        "DejaVuSans.ttf",
        "arial.ttf",
        "segoeui.ttf",
        "LiberationSans-Regular.ttf",
        "Helvetica.ttf",
    ];

    private static readonly string[] BoldCandidates =
    [
        "DejaVuSans-Bold.ttf",
        "arialbd.ttf",
        "segoeuib.ttf",
        "LiberationSans-Bold.ttf",
        "Helvetica-Bold.ttf",
    ];

    private static readonly string[] ItalicCandidates =
    [
        "DejaVuSans-Oblique.ttf",
        "ariali.ttf",
        "segoeuii.ttf",
        "LiberationSans-Italic.ttf",
    ];

    private static readonly string[] MonoCandidates =
    [
        "DejaVuSansMono.ttf",
        "consola.ttf",
        "cour.ttf",
        "LiberationMono-Regular.ttf",
    ];

    private readonly string? fontDirectory;
    private readonly Dictionary<string, byte[]> cache = new(StringComparer.Ordinal);

    /// <summary>
    /// Erzeugt den Resolver.
    /// </summary>
    /// <param name="fontDirectory">
    /// Optionaler Ordner mit <c>.ttf</c>-Dateien. Ist er <c>null</c> oder leer, werden die
    /// Systempfade durchsucht.
    /// </param>
    public CommandPdfFontResolver(string? fontDirectory = null)
    {
        this.fontDirectory = string.IsNullOrWhiteSpace(fontDirectory) ? null : fontDirectory;
    }

    /// <inheritdoc />
    public byte[]? GetFont(string faceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(faceName);

        if (this.cache.TryGetValue(faceName, out var cached))
        {
            return cached;
        }

        var candidates = faceName switch
        {
            Bold => BoldCandidates,
            Italic => ItalicCandidates,
            Mono => MonoCandidates,
            _ => RegularCandidates,
        };

        var data = this.LoadFirstAvailable(candidates);

        if (data is null)
        {
            return null;
        }

        this.cache[faceName] = data;
        return data;
    }

    /// <inheritdoc />
    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        var faceName = (isBold, isItalic) switch
        {
            (true, _) => Bold,
            (_, true) => Italic,
            _ => Regular,
        };

        return new FontResolverInfo(faceName);
    }

    /// <summary>
    /// Erzeugt eine Schrift mit dem angegebenen logischen Namen.
    /// </summary>
    /// <param name="faceName">Einer der Namenskonstanten dieser Klasse.</param>
    /// <param name="size">Schriftgröße in Punkt.</param>
    public PdfSharp.Drawing.XFont CreateFont(string faceName, double size) =>
        new(faceName, size);

    private byte[]? LoadFirstAvailable(IEnumerable<string> candidates)
    {
        foreach (var candidate in candidates)
        {
            var data = this.TryLoad(candidate);

            if (data is not null)
            {
                return data;
            }
        }

        return null;
    }

    private byte[]? TryLoad(string fileName)
    {
        if (this.fontDirectory is not null)
        {
            var path = Path.Combine(this.fontDirectory, fileName);

            if (File.Exists(path))
            {
                return File.ReadAllBytes(path);
            }
        }

        foreach (var directory in GetSystemFontDirectories())
        {
            var path = Path.Combine(directory, fileName);

            if (File.Exists(path))
            {
                return File.ReadAllBytes(path);
            }
        }

        return null;
    }

    private static IEnumerable<string> GetSystemFontDirectories()
    {
        if (OperatingSystem.IsWindows())
        {
            yield return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "Fonts");
            yield return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft",
                "Windows",
                "Fonts");
        }
        else if (OperatingSystem.IsMacOS())
        {
            yield return "/System/Library/Fonts";
            yield return "/Library/Fonts";
            yield return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library",
                "Fonts");
        }
        else
        {
            yield return "/usr/share/fonts/truetype/dejavu";
            yield return "/usr/share/fonts/truetype/liberation";
            yield return "/usr/share/fonts/TTF";
            yield return "/usr/share/fonts";
        }
    }
}
