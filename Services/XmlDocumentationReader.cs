using System.Reflection;
using System.Xml.Linq;

namespace ATK.Command.Explorer.Services;

/// <summary>
/// Liest die XML-Dokumentationsdatei einer Assembly und stellt Summary- und Remarks-Texte bereit.
/// </summary>
/// <remarks>
/// Die Datei wird neben der Assembly erwartet (<c>{AssemblyName}.xml</c>). Sie entsteht nur,
/// wenn im Projekt <c>GenerateDocumentationFile</c> aktiviert ist.
/// </remarks>
public sealed class XmlDocumentationReader
{
    private readonly Dictionary<string, string> summaries = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> remarks = new(StringComparer.Ordinal);

    /// <summary>
    /// Erzeugt einen Leser für die angegebene Assembly.
    /// </summary>
    /// <param name="assembly">Assembly, deren XML-Dokumentation gelesen werden soll.</param>
    public XmlDocumentationReader(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var path = Path.Combine(AppContext.BaseDirectory, $"{assembly.GetName().Name}.xml");
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            var document = XDocument.Load(path);
            foreach (var member in document.Descendants("member"))
            {
                var name = member.Attribute("name")?.Value;
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var summary = Normalize(member.Element("summary")?.Value);
                if (!string.IsNullOrWhiteSpace(summary))
                {
                    this.summaries[name] = summary;
                }

                var remark = Normalize(member.Element("remarks")?.Value);
                if (!string.IsNullOrWhiteSpace(remark))
                {
                    this.remarks[name] = remark;
                }
            }
        }
        catch (Exception)
        {
            // Eine defekte XML-Datei darf die API nicht lahmlegen.
            this.summaries.Clear();
            this.remarks.Clear();
        }
    }

    /// <summary>
    /// Liefert die Summary eines Typs.
    /// </summary>
    public string? GetTypeSummary(Type type) => this.Lookup(this.summaries, $"T:{type.FullName}");

    /// <summary>
    /// Liefert die Remarks eines Typs.
    /// </summary>
    public string? GetTypeRemarks(Type type) => this.Lookup(this.remarks, $"T:{type.FullName}");

    /// <summary>
    /// Liefert die Summary einer Property.
    /// </summary>
    public string? GetPropertySummary(MemberInfo property) =>
        this.Lookup(this.summaries, $"P:{property.DeclaringType?.FullName}.{property.Name}");

    /// <summary>
    /// Liefert die Summary eines Enum-Mitglieds.
    /// </summary>
    public string? GetFieldSummary(FieldInfo field) =>
        this.Lookup(this.summaries, $"F:{field.DeclaringType?.FullName}.{field.Name}");

    private string? Lookup(Dictionary<string, string> source, string key) =>
        source.TryGetValue(key, out var value) ? value : null;

    private static string? Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var lines = text
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToArray();

        return lines.Length == 0 ? null : string.Join(' ', lines);
    }
}
