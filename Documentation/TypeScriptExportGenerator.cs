using System.Reflection;
using ATK.Command.CsToTsGenerator;
using ATK.Command.CsToTsGenerator.Generators.TypeScript.SeparatedFiles;
using ATK.Command.CsToTsGenerator.Models;

namespace ATK.Command.Explorer.Documentation;

/// <summary>
/// Erzeugt TypeScript-Typen aus den Commands einer Assembly.
/// </summary>
/// <remarks>
/// <para>
/// Nutzt das Paket <c>ATK.Command.CsToTsGenerator</c>. Der Generator arbeitet über Reflection
/// und schreibt normalerweise direkt ins Dateisystem. Für einen HTTP-Download ist das
/// ungeeignet, deshalb wird in ein temporäres Verzeichnis gebaut und das Ergebnis wieder
/// eingelesen.
/// </para>
/// <para>
/// Die erzeugten Dateien werden als <c>Pfad → Inhalt</c> zurückgegeben, damit der Aufrufer
/// sie in ein ZIP packen kann.
/// </para>
/// </remarks>
public sealed class TypeScriptExportGenerator
{
    private readonly Assembly assembly;
    private readonly Type markerInterface;

    /// <summary>
    /// Erzeugt den Generator.
    /// </summary>
    /// <param name="assembly">Assembly, die nach Commands durchsucht wird.</param>
    /// <param name="markerInterface">Marker-Interface, das alle Commands implementieren.</param>
    public TypeScriptExportGenerator(Assembly assembly, Type markerInterface)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(markerInterface);

        this.assembly = assembly;
        this.markerInterface = markerInterface;
    }

    /// <summary>
    /// Erzeugt die TypeScript-Dateien.
    /// </summary>
    /// <param name="commands">
    /// Die zu exportierenden Commands. Leer bedeutet „alle Commands der Assembly“.
    /// </param>
    /// <returns>Dateipfad im Archiv → Inhalt, alphabetisch sortiert.</returns>
    /// <remarks>
    /// Der Generator kennt keinen Filter auf einzelne Typen – er bekommt genau die Typen,
    /// die exportiert werden sollen. Abhängige Typen (Enums, verschachtelte Klassen) zieht
    /// er selbst nach.
    /// </remarks>
    public IReadOnlyDictionary<string, string> Generate(IReadOnlyList<Type> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);

        var types = commands.Count == 0
            ? this.GetAllCommandTypes()
            : commands.ToArray();

        if (types.Length == 0)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var directory = Path.Combine(
            Path.GetTempPath(),
            $"atk-ts-export-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(directory);

            var built = new Generator()
                .TypeScript()
                .SeparatedFiles()
                .SetRequestCommandInterfaceNameForGeneratedCommands("ICommand")
                .SetInterfaceFilter(this.markerInterface)
                .SetReturnTypeOfCommands(typeof(Cortex.Mediator.Commands.ICommand<>))
                .AddRangeOfCommandTypesToGenerate(types)
                .GenerateMetadata()
                .Generate([typeof(Cortex.Mediator.Commands.ICommand<>), this.markerInterface])
                .Build(directory);

            // `Build` berechnet nur die Dateien – geschrieben wird erst mit `Save`.
            built.Save(clearDestinationFolder: false);

            return ReadFiles(directory, built);
        }
        finally
        {
            TryDelete(directory);
        }
    }

    /// <summary>
    /// Liest die erzeugten Dateien aus dem temporären Verzeichnis.
    /// </summary>
    /// <remarks>
    /// Maßgeblich ist der Inhalt auf der Platte, nicht <c>BuildFiles</c>: Der Generator
    /// schreibt beim Aufruf von <c>Build</c> bereits alles, was er erzeugen kann.
    /// </remarks>
    private static IReadOnlyDictionary<string, string> ReadFiles(
        string directory,
        BuildedSeparatedFiles built)
    {
        Dictionary<string, string> files = new(StringComparer.Ordinal);

        foreach (var path in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(directory, path).Replace('\\', '/');
            files[relative] = File.ReadAllText(path);
        }

        // Der Generator liefert zusätzlich den C#-Quelltext seiner Eingaben. Der gehört
        // nicht in einen TypeScript-Export.
        foreach (var file in built.BuildFiles)
        {
            if (file.BuildFiLeType == BuildFiLeType.GeneratedCSharpSource)
            {
                files.Remove(file.Path.Replace('\\', '/'));
            }
        }

        return files
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
    }

    private Type[] GetAllCommandTypes() =>
        this.assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false }
                           && this.markerInterface.IsAssignableFrom(type))
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .ToArray();

    private static void TryDelete(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
            // Temporäre Dateien dürfen liegen bleiben – der nächste Lauf räumt nicht auf,
            // das Betriebssystem schon.
        }
        catch (UnauthorizedAccessException)
        {
            // Siehe oben.
        }
    }
}
