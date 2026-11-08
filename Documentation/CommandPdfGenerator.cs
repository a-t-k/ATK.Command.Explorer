using System.Text;
using ATK.Command.Explorer.Models;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace ATK.Command.Explorer.Documentation;

/// <summary>
/// Erzeugt aus den Command-Metadaten ein PDF-Dokument.
/// </summary>
/// <remarks>
/// <para>
/// Das PDF ist bewusst schlicht: Titelseite, Inhaltsverzeichnis, ein Abschnitt pro Command
/// mit Basisinformationen, Property-Tabelle und Beispiel-Request.
/// </para>
/// <para>
/// PDFsharp 6 benötigt unter .NET eine Font-Resolver-Implementierung, weil es keine
/// Systemschriften selbst auflöst. <see cref="CommandPdfFontResolver"/> übernimmt das.
/// </para>
/// </remarks>
public sealed class CommandPdfGenerator
{
    private const double PageMargin = 50;

    private readonly CommandPdfFontResolver fontResolver;

    /// <summary>
    /// Erzeugt den Generator.
    /// </summary>
    /// <param name="fontResolver">Resolver, der die Schriften bereitstellt.</param>
    public CommandPdfGenerator(CommandPdfFontResolver fontResolver)
    {
        ArgumentNullException.ThrowIfNull(fontResolver);

        this.fontResolver = fontResolver;
    }

    /// <summary>
    /// Erzeugt das PDF.
    /// </summary>
    /// <param name="commands">Die zu dokumentierenden Commands.</param>
    /// <param name="title">Titel des Dokuments.</param>
    /// <returns>Der Inhalt der PDF-Datei.</returns>
    public byte[] Generate(IReadOnlyList<CommandDescriptor> commands, string title)
    {
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        // PDFsharp erlaubt nur einen Resolver pro Prozess.
        if (GlobalFontSettings.FontResolver is null)
        {
            GlobalFontSettings.FontResolver = this.fontResolver;
        }

        using PdfDocument document = new();
        document.Info.Title = title;
        document.Info.Creator = "ATK.Command.Explorer";

        using PdfWriter writer = new(document, this.fontResolver);

        this.WriteTitlePage(writer, commands, title);
        this.WriteTableOfContents(writer, commands);
        this.WriteCommands(writer, commands);

        writer.Finish(title);

        using MemoryStream stream = new();
        document.Save(stream, closeStream: false);

        return stream.ToArray();
    }

    private void WriteTitlePage(
        PdfWriter writer,
        IReadOnlyList<CommandDescriptor> commands,
        string title)
    {
        writer.NewPage();

        writer.Heading(title, 26);
        writer.Gap(2);
        writer.Text("Dokumentation der Commands", 13);
        writer.Gap(12);
        writer.Text($"Erzeugt am {DateTimeOffset.Now:yyyy-MM-dd HH:mm}", 10);
        writer.Text(
            $"{Plural(commands.Count, "Command", "Commands")} in "
            + Plural(
                commands.Select(command => command.Namespace).Distinct().Count(),
                "Namespace",
                "Namespaces"),
            10);
        writer.Gap(16);

        writer.Heading("Aufbau", 13);

        foreach (string line in new[]
                 {
                     "1. Inhaltsverzeichnis als Namespace-Baum",
                     "2. Ein Abschnitt pro Command mit Basisinformationen",
                     "3. Property-Tabelle mit Typ und Pflichtfeld-Kennzeichnung",
                     "4. Beispiel-Request als JSON",
                 })
        {
            writer.Text(line, 10);
        }
    }

    /// <summary>
    /// Formatiert eine Anzahl mit passender Endung.
    /// </summary>
    /// <param name="count">Die Anzahl.</param>
    /// <param name="singular">Endung für genau ein Element.</param>
    /// <param name="plural">Endung für null oder mehrere Elemente.</param>
    /// <returns>Etwa <c>1 Command</c> oder <c>3 Commands</c>.</returns>
    private static string Plural(int count, string singular, string plural) =>
        count == 1 ? $"1 {singular}" : $"{count} {plural}";

    private void WriteTableOfContents(PdfWriter writer, IReadOnlyList<CommandDescriptor> commands)
    {
        writer.NewPage();
        writer.Bookmark("Inhalt");
        writer.Heading("Inhalt", 18);
        writer.Gap(8);

        IReadOnlyList<DocNode> tree = MarkdownVaultGenerator.BuildTree(commands);

        foreach (DocNode node in tree)
        {
            this.WriteTocNode(writer, node, depth: 0);
        }
    }

    /// <summary>
    /// Schreibt einen Namespace-Knoten samt Commands und Unter-Namespaces.
    /// </summary>
    /// <remarks>
    /// Ketten aus Namespaces ohne eigene Commands werden zusammengefasst, damit das
    /// Inhaltsverzeichnis nicht über mehrere Seiten läuft.
    /// </remarks>
    private void WriteTocNode(PdfWriter writer, DocNode node, int depth)
    {
        string indent = new(' ', depth * 2);
        DocNode current = node;
        StringBuilder name = new(node.Name);

        while (current.Commands.Count == 0 && current.Children.Count == 1)
        {
            current = current.Children[0];
            name.Append('.').Append(current.Name);
        }

        writer.Text($"{indent}{name}  ({current.TotalCount})", 10, bold: true);

        foreach (CommandDescriptor command in current.Commands)
        {
            writer.Text($"{indent}  {command.Name}", 9);
        }

        foreach (DocNode child in current.Children)
        {
            this.WriteTocNode(writer, child, depth + 1);
        }
    }

    private void WriteCommands(PdfWriter writer, IReadOnlyList<CommandDescriptor> commands)
    {
        foreach (CommandDescriptor command in commands.OrderBy(c => c.Name, StringComparer.Ordinal))
        {
            writer.NewPage();
            writer.Bookmark(command.Name);
            writer.Heading(command.Name, 18);

            if (!string.IsNullOrWhiteSpace(command.Summary))
            {
                writer.Wrapped(command.Summary, 10);
            }

            if (!string.IsNullOrWhiteSpace(command.Remarks))
            {
                writer.Wrapped(command.Remarks, 9, italic: true);
            }

            writer.Gap(6);
            writer.Heading("Basisinformationen", 12);

            List<TableRow> basics =
            [
                new(["Namespace", command.Namespace], Bold: true),
                new(["Response Type", command.ResultType], Bold: true),
                new(["Autorisierung", command.Authorization.Description], Bold: true),
                new(["$type", command.TypeName], Bold: true),
            ];

            writer.Table(
                ["Merkmal", "Wert"],
                [0.22, 0.78],
                basics);

            writer.Gap(4);

            this.WriteProperties(writer, command.Properties);

            writer.Heading("Beispiel-Request", 12);
            writer.CodeBlock(command.SampleJson);
        }
    }

    private void WriteProperties(PdfWriter writer, IReadOnlyList<CommandPropertyDescriptor> properties)
    {
        if (properties.Count == 0)
        {
            return;
        }

        writer.Heading("Properties", 12);

        List<TableRow> rows = [];
        CollectPropertyRows(properties, depth: 0, rows);

        writer.Table(
            ["Name", "Typ", "Pflicht", "Beschreibung"],
            [0.26, 0.20, 0.10, 0.44],
            rows);

        writer.Gap(8);
    }

    /// <summary>
    /// Flacht die Property-Hierarchie zu Tabellenzeilen ab.
    /// </summary>
    /// <remarks>
    /// Verschachtelte Properties werden eingerückt und mit einem vorangestellten
    /// <c>-</c> markiert, damit die Zugehörigkeit auch ohne Baumstruktur erkennbar bleibt.
    /// Bewusst nur ASCII: die eingebettete Schrift nutzt WinAnsi, Zeichen wie <c>↳</c>
    /// fehlen dort und würden die ganze Zelle verschlucken.
    /// </remarks>
    private static void CollectPropertyRows(
        IReadOnlyList<CommandPropertyDescriptor> properties,
        int depth,
        List<TableRow> rows)
    {
        foreach (CommandPropertyDescriptor property in properties)
        {
            string indent = depth == 0 ? string.Empty : new string(' ', depth * 3);
            string marker = depth == 0 ? string.Empty : "- ";
            string required = property.IsValueType && !property.IsNullable ? "ja" : "nein";

            string description = property.Summary ?? string.Empty;

            if (property.EnumValues.Count > 0)
            {
                string values = string.Join(
                    ", ",
                    property.EnumValues.Select(value => $"{value.Name}={value.Value}"));

                description = string.IsNullOrEmpty(description)
                    ? $"Werte: {values}"
                    : $"{description} Werte: {values}";
            }

            rows.Add(new TableRow(
                [$"{indent}{marker}{property.JsonName}", property.TypeName, required, description],
                Bold: depth == 0));

            if (property.Properties.Count > 0)
            {
                CollectPropertyRows(property.Properties, depth + 1, rows);
            }
        }
    }

    /// <summary>
    /// Ein Textabschnitt mit eigener Schrift innerhalb einer Zeile.
    /// </summary>
    /// <param name="Text">Der Text.</param>
    /// <param name="FaceName">Logischer Schriftname aus <see cref="CommandPdfFontResolver"/>.</param>
    /// <param name="Size">Schriftgröße in Punkt.</param>
    private sealed record TextRun(string Text, string FaceName, double Size);

    /// <summary>
    /// Eine Zeile einer Tabelle.
    /// </summary>
    /// <param name="Cells">Zellinhalte in Spaltenreihenfolge.</param>
    /// <param name="Bold">Gibt an, ob die erste Spalte fett gesetzt wird.</param>
    private sealed record TableRow(string[] Cells, bool Bold);

    /// <summary>
    /// Schreibt Text auf Seiten und bricht bei Bedarf um.
    /// </summary>
    private sealed class PdfWriter : IDisposable
    {
        private readonly PdfDocument document;
        private readonly CommandPdfFontResolver resolver;

        private PdfPage page = null!;
        private XGraphics graphics = null!;
        private double y;

        public PdfWriter(PdfDocument document, CommandPdfFontResolver resolver)
        {
            this.document = document;
            this.resolver = resolver;
        }

        public void NewPage()
        {
            this.graphics?.Dispose();

            this.page = this.document.AddPage();
            this.page.Size = PdfSharp.PageSize.A4;
            this.graphics = XGraphics.FromPdfPage(this.page);
            this.y = PageMargin;
        }

        public void Heading(string text, double size)
        {
            XFont font = this.resolver.CreateFont(CommandPdfFontResolver.Bold, size);
            this.Write(text, font, spacingAfter: 4);
        }

        public void Text(string text, double size, bool mono = false, bool bold = false)
        {
            string faceName = mono
                ? CommandPdfFontResolver.Mono
                : bold
                    ? CommandPdfFontResolver.Bold
                    : CommandPdfFontResolver.Regular;

            XFont font = this.resolver.CreateFont(faceName, size);
            this.Write(text, font, spacingAfter: 1);
        }

        public void Wrapped(string text, double size, bool italic = false)
        {
            XFont font = this.resolver.CreateFont(
                italic ? CommandPdfFontResolver.Italic : CommandPdfFontResolver.Regular,
                size);

            foreach (string line in Wrap(text, font, this.ContentWidth))
            {
                this.Write(line, font, spacingAfter: 0);
            }
        }

        /// <summary>
        /// Schreibt mehrere Textabschnitte mit unterschiedlichen Schriften in eine Zeile.
        /// </summary>
        /// <remarks>
        /// Passt die ganze Zeile, wird jeder Abschnitt als <b>ein</b> String gezeichnet –
        /// das hält Text-Extraktion und Copy-Paste im PDF-Reader sauber. Nur wenn umgebrochen
        /// werden muss, wird wortweise gezeichnet.
        /// </remarks>
        public void Runs(IReadOnlyList<TextRun> runs)
        {
            if (runs.Count == 0)
            {
                return;
            }

            double lineHeight = runs
                .Max(run => this.resolver.CreateFont(run.FaceName, run.Size).GetHeight());

            double totalWidth = runs.Sum(run =>
                this.graphics.MeasureString(
                    run.Text,
                    this.resolver.CreateFont(run.FaceName, run.Size)).Width);

            if (totalWidth <= this.ContentWidth)
            {
                if (this.y + lineHeight > this.page.Height.Point - PageMargin)
                {
                    this.NewPage();
                }

                double cursor = PageMargin;

                foreach (TextRun run in runs)
                {
                    XFont font = this.resolver.CreateFont(run.FaceName, run.Size);

                    this.graphics.DrawString(
                        Sanitize(run.Text),
                        font,
                        XBrushes.Black,
                        new XPoint(cursor, this.y),
                        XStringFormats.TopLeft);

                    cursor += this.graphics.MeasureString(Sanitize(run.Text), font).Width;
                }

                this.y += lineHeight + 1;
                return;
            }

            this.WriteRunsWrapped(runs, lineHeight);
        }

        /// <summary>
        /// Zeichnet die Abschnitte wortweise und bricht an der Seitengrenze um.
        /// </summary>
        private void WriteRunsWrapped(IReadOnlyList<TextRun> runs, double lineHeight)
        {
            double x = PageMargin;
            bool lineHasContent = false;

            foreach (TextRun run in runs)
            {
                XFont font = this.resolver.CreateFont(run.FaceName, run.Size);

                foreach (string word in Tokenize(run.Text))
                {
                    double width = this.graphics.MeasureString(word, font).Width;

                    if (lineHasContent && x + width > PageMargin + this.ContentWidth)
                    {
                        this.y += lineHeight;
                        x = PageMargin;
                        lineHasContent = false;
                    }

                    if (this.y + lineHeight > this.page.Height.Point - PageMargin)
                    {
                        this.NewPage();
                        x = PageMargin;
                        lineHasContent = false;
                    }

                    this.graphics.DrawString(
                        Sanitize(word),
                        font,
                        XBrushes.Black,
                        new XPoint(x, this.y),
                        XStringFormats.TopLeft);

                    x += width;
                    lineHasContent = true;
                }
            }

            this.y += lineHeight + 1;
        }

        /// <summary>
        /// Zeichnet Code in einen Rahmen mit grauer Hinterlegung.
        /// </summary>
        /// <remarks>
        /// Der Rahmen wird pro Seite neu begonnen, wenn der Code über eine Seitengrenze
        /// läuft. So bleibt der Codeblock optisch zusammenhängend.
        /// </remarks>
        public void CodeBlock(string code)
        {
            XFont font = this.resolver.CreateFont(CommandPdfFontResolver.Mono, 8.5);
            double lineHeight = font.GetHeight();
            const double padding = 6;
            const double indent = 4;

            string[] lines = code.Replace("\r\n", "\n").Split('\n');
            int index = 0;

            while (index < lines.Length)
            {
                double available = this.page.Height.Point - PageMargin - this.y;
                int fit = Math.Max(1, (int)((available - (2 * padding)) / lineHeight));
                int count = Math.Min(fit, lines.Length - index);

                double boxHeight = (count * lineHeight) + (2 * padding);
                double boxWidth = this.ContentWidth;

                XRect box = new(PageMargin, this.y, boxWidth, boxHeight);

                this.graphics.DrawRectangle(
                    new XSolidBrush(XColor.FromArgb(245, 245, 245)),
                    box);
                this.graphics.DrawRectangle(
                    new XPen(XColor.FromArgb(200, 200, 200), 0.75),
                    box);

                double textY = this.y + padding;

                for (int offset = 0; offset < count; offset++)
                {
                    this.graphics.DrawString(
                        Sanitize(lines[index + offset].TrimEnd()),
                        font,
                        XBrushes.Black,
                        new XPoint(PageMargin + padding + indent, textY),
                        XStringFormats.TopLeft);

                    textY += lineHeight;
                }

                this.y += boxHeight;
                index += count;

                if (index < lines.Length)
                {
                    this.NewPage();
                }
            }

            this.y += 6;
        }

        /// <summary>
        /// Zeichnet eine Tabelle mit Kopfzeile, Gitternetz und Seitenumbruch.
        /// </summary>
        /// <param name="headers">Spaltenüberschriften.</param>
        /// <param name="widths">
        /// Spaltenbreiten als Anteil der Inhaltsbreite. Die Summe sollte 1 ergeben.
        /// </param>
        /// <param name="rows">Die Zeilen.</param>
        /// <remarks>
        /// Bei einem Seitenumbruch wird die Kopfzeile auf der neuen Seite wiederholt.
        /// Zellen werden an Wortgrenzen umgebrochen; die Zeilenhöhe richtet sich nach der
        /// höchsten Zelle.
        /// </remarks>
        public void Table(
            IReadOnlyList<string> headers,
            IReadOnlyList<double> widths,
            IReadOnlyList<TableRow> rows)
        {
            const double cellPadding = 3;
            const double fontSize = 8.5;

            XFont headerFont = this.resolver.CreateFont(CommandPdfFontResolver.Bold, fontSize);
            XFont bodyFont = this.resolver.CreateFont(CommandPdfFontResolver.Regular, fontSize);
            XFont boldFont = this.resolver.CreateFont(CommandPdfFontResolver.Bold, fontSize);

            double[] columns = widths.Select(width => width * this.ContentWidth).ToArray();
            double lineHeight = bodyFont.GetHeight();

            this.DrawTableHeader(headers, columns, headerFont, cellPadding, lineHeight);

            foreach (TableRow row in rows)
            {
                List<string[]> wrapped = [];

                for (int column = 0; column < columns.Length; column++)
                {
                    string cell = column < row.Cells.Length ? row.Cells[column] : string.Empty;
                    XFont font = column == 0 && row.Bold ? boldFont : bodyFont;

                    wrapped.Add(
                        Wrap(cell, font, columns[column] - (2 * cellPadding)).ToArray());
                }

                int lines = Math.Max(1, wrapped.Max(cell => cell.Length));
                double rowHeight = (lines * lineHeight) + (2 * cellPadding);

                if (this.y + rowHeight > this.page.Height.Point - PageMargin)
                {
                    this.NewPage();
                    this.DrawTableHeader(headers, columns, headerFont, cellPadding, lineHeight);
                }

                double x = PageMargin;

                for (int column = 0; column < columns.Length; column++)
                {
                    XFont font = column == 0 && row.Bold ? boldFont : bodyFont;
                    double textY = this.y + cellPadding;

                    foreach (string line in wrapped[column])
                    {
                        this.graphics.DrawString(
                            Sanitize(line),
                            font,
                            XBrushes.Black,
                            new XPoint(x + cellPadding, textY),
                            XStringFormats.TopLeft);

                        textY += lineHeight;
                    }

                    x += columns[column];
                }

                this.DrawRowLines(columns, rowHeight);
                this.y += rowHeight;
            }

            this.y += 4;
        }

        private void DrawTableHeader(
            IReadOnlyList<string> headers,
            IReadOnlyList<double> columns,
            XFont font,
            double cellPadding,
            double lineHeight)
        {
            double height = lineHeight + (2 * cellPadding);

            if (this.y + height > this.page.Height.Point - PageMargin)
            {
                this.NewPage();
            }

            this.graphics.DrawRectangle(
                new XSolidBrush(XColor.FromArgb(235, 235, 235)),
                new XRect(PageMargin, this.y, this.ContentWidth, height));

            double x = PageMargin;

            for (int column = 0; column < columns.Count; column++)
            {
                this.graphics.DrawString(
                    Sanitize(headers[column]),
                    font,
                    XBrushes.Black,
                    new XPoint(x + cellPadding, this.y + cellPadding),
                    XStringFormats.TopLeft);

                x += columns[column];
            }

            this.DrawRowLines(columns, height);
            this.y += height;
        }

        /// <summary>
        /// Zeichnet die Trennlinien einer Zeile.
        /// </summary>
        private void DrawRowLines(IReadOnlyList<double> columns, double height)
        {
            XPen pen = new(XColor.FromArgb(190, 190, 190), 0.5);

            this.graphics.DrawLine(
                pen,
                PageMargin,
                this.y + height,
                PageMargin + this.ContentWidth,
                this.y + height);

            double x = PageMargin;

            foreach (double column in columns)
            {
                this.graphics.DrawLine(pen, x, this.y, x, this.y + height);
                x += column;
            }

            this.graphics.DrawLine(
                pen,
                PageMargin + this.ContentWidth,
                this.y,
                PageMargin + this.ContentWidth,
                this.y + height);
        }

        public void Gap(double height) => this.y += height;

        /// <summary>
        /// Legt ein Lesezeichen auf die aktuelle Seite.
        /// </summary>
        /// <param name="title">Beschriftung im Lesezeichen-Baum des PDF-Readers.</param>
        public void Bookmark(string title)
        {
            this.document.Outlines.Add(title, this.page, opened: true);
        }

        /// <summary>
        /// Schreibt Fußzeile und Seitenzahlen auf alle Seiten außer der Titelseite.
        /// </summary>
        /// <param name="title">Titel für die linke Seite der Fußzeile.</param>
        /// <remarks>
        /// Muss nach dem letzten Inhalt laufen, weil die Gesamtseitenzahl erst dann feststeht.
        /// <see cref="XGraphicsPdfPageOptions.Append"/> sorgt dafür, dass der vorhandene
        /// Seiteninhalt erhalten bleibt.
        /// </remarks>
        public void Finish(string title)
        {
            this.graphics?.Dispose();
            this.graphics = null!;

            XFont font = this.resolver.CreateFont(CommandPdfFontResolver.Regular, 8);
            int total = this.document.PageCount;

            for (int index = 1; index < total; index++)
            {
                PdfPage current = this.document.Pages[index];
                double width = current.Width.Point;
                double height = current.Height.Point;

                using XGraphics footer = XGraphics.FromPdfPage(current, XGraphicsPdfPageOptions.Append);

                footer.DrawLine(
                    new XPen(XColor.FromArgb(200, 200, 200), 0.5),
                    PageMargin,
                    height - PageMargin + 8,
                    width - PageMargin,
                    height - PageMargin + 8);

                footer.DrawString(
                    Sanitize(title),
                    font,
                    XBrushes.Gray,
                    new XRect(PageMargin, height - PageMargin + 12, width - (2 * PageMargin), 12),
                    XStringFormats.TopLeft);

                footer.DrawString(
                    $"Seite {index + 1} von {total}",
                    font,
                    XBrushes.Gray,
                    new XRect(PageMargin, height - PageMargin + 12, width - (2 * PageMargin), 12),
                    XStringFormats.TopRight);
            }
        }

        public void Dispose() => this.graphics?.Dispose();

        private double ContentWidth => this.page.Width.Point - (2 * PageMargin);

        /// <summary>
        /// Ersetzt Zeichen, die in der eingebetteten Schrift nicht darstellbar sind.
        /// </summary>
        /// <remarks>
        /// PDFsharp bettet die Schrift mit WinAnsi-Kodierung ein. Zeichen außerhalb dieses
        /// Zeichensatzes – etwa <c>↳</c>, <c>→</c> oder Emoji – führen dazu, dass die
        /// <b>gesamte</b> Zeichenkette verschluckt wird. Deshalb wird vor dem Zeichnen
        /// ersetzt.
        /// </remarks>
        private static string Sanitize(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            StringBuilder builder = new(text.Length);

            foreach (char character in text)
            {
                builder.Append(character switch
                {
                    '\u2192' => "->",   // →
                    '\u2190' => "<-",   // ←
                    '\u2191' => "^",    // ↑
                    '\u2193' => "v",    // ↓
                    '\u21b3' => "-",    // ↳
                    '\u21d2' => "=>",   // ⇒
                    '\u2013' => "-",    // –
                    '\u2014' => "-",    // —
                    '\u2018' or '\u2019' => "'",
                    '\u201c' or '\u201d' => "\"",
                    '\u2026' => "...",  // …
                    '\u2022' => "*",    // •
                    '\u00a0' => " ",    // geschütztes Leerzeichen
                    _ => character <= '\u00ff' ? character.ToString() : "?",
                });
            }

            return builder.ToString();
        }

        private void Write(string text, XFont font, double spacingAfter)
        {
            double height = font.GetHeight();

            if (this.y + height > this.page.Height.Point - PageMargin)
            {
                this.NewPage();
            }

            this.graphics.DrawString(
                Sanitize(text),
                font,
                XBrushes.Black,
                new XRect(PageMargin, this.y, this.ContentWidth, height),
                XStringFormats.TopLeft);

            this.y += height + spacingAfter;
        }

        /// <summary>
        /// Zerlegt Text in Wörter und hängt das Leerzeichen an das jeweilige Wort.
        /// </summary>
        /// <remarks>
        /// So bleibt die Breitenmessung korrekt, ohne dass beim Zeichnen Leerzeichen
        /// verloren gehen.
        /// </remarks>
        private static IEnumerable<string> Tokenize(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                yield break;
            }

            int start = 0;

            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] != ' ')
                {
                    continue;
                }

                yield return text[start..(i + 1)];
                start = i + 1;
            }

            if (start < text.Length)
            {
                yield return text[start..];
            }
        }

        private static IEnumerable<string> Wrap(string text, XFont font, double maxWidth)
        {
            foreach (string paragraph in text.Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(paragraph))
                {
                    yield return string.Empty;
                    continue;
                }

                StringBuilder line = new();

                foreach (string word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    string candidate = line.Length == 0 ? word : $"{line} {word}";

                    if (line.Length == 0 || EstimateWidth(candidate, font) <= maxWidth)
                    {
                        line.Clear();
                        line.Append(candidate);
                        continue;
                    }

                    yield return line.ToString();
                    line.Clear();
                    line.Append(word);
                }

                if (line.Length > 0)
                {
                    yield return line.ToString();
                }
            }
        }

        /// <summary>
        /// Schätzt die Textbreite. Exaktes Messen bräuchte ein <see cref="XGraphics"/>,
        /// das hier nicht pro Zeile erzeugt werden soll.
        /// </summary>
        private static double EstimateWidth(string text, XFont font) =>
            text.Length * font.Size * 0.5;
    }
}
