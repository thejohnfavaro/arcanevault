
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace ArcaneVault.Next.Desktop;

/// <summary>Safe WPF rendering of Markdown; no HTML, scripts or web navigation.</summary>
internal static class AiReviewMarkdown
{
    public static FlowDocument Render(string markdown)
    {
        var doc = new FlowDocument
        {
            PagePadding = new Thickness(12, 10, 20, 16),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 13,
            LineHeight = 20,
            Foreground = Brushes.WhiteSmoke
        };
        var lines = (markdown ?? "").Replace("\r\n", "\n").Split('\n');
        for (int i = 0; i < lines.Length;)
        {
            string line = lines[i].Trim();
            if (line.Length == 0) { i++; continue; }
            if (line.StartsWith("|") && i + 1 < lines.Length &&
                Regex.IsMatch(lines[i + 1], @"^\s*\|?[\s:\-|]+\|?\s*$") &&
                lines[i + 1].Contains('-'))
            {
                var rows = new List<string> { line };
                i += 2;
                while (i < lines.Length && lines[i].TrimStart().StartsWith("|"))
                    rows.Add(lines[i++].Trim());
                int cols = rows[0].Trim('|').Split('|').Length;
                if (cols > 1 && cols <= 6 && rows.All(r => r.Trim('|').Split('|').Length == cols))
                {
                    var table = new Table { CellSpacing = 0, Margin = new Thickness(0, 7, 0, 12) };
                    for (int col = 0; col < cols; col++) table.Columns.Add(new TableColumn());
                    var group = new TableRowGroup();
                    table.RowGroups.Add(group);
                    for (int r = 0; r < rows.Count; r++)
                    {
                        var tableRow = new TableRow();
                        if (r == 0) tableRow.Background = new SolidColorBrush(Color.FromRgb(55, 60, 74));
                        foreach (var cell in rows[r].Trim('|').Split('|'))
                        {
                            var p = new Paragraph { Margin = new Thickness(0) };
                            AddInline(p.Inlines, cell.Trim());
                            tableRow.Cells.Add(new TableCell(p)
                            {
                                Padding = new Thickness(7, 5, 7, 6),
                                BorderBrush = Brushes.DimGray,
                                BorderThickness = new Thickness(0, 0, 0, 1)
                            });
                        }
                        group.Rows.Add(tableRow);
                    }
                    doc.Blocks.Add(table);
                    continue;
                }
                foreach (var row in rows) AddParagraph(doc, row, false);
                continue;
            }
            int level = line.TakeWhile(c => c == '#').Count();
            if (level is >= 1 and <= 4 && line.Length > level && line[level] == ' ')
            {
                var h = new Paragraph
                {
                    FontSize = level == 1 ? 20 : level == 2 ? 16 : 14,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(230, 187, 113)),
                    Margin = new Thickness(0, 12, 0, 7)
                };
                AddInline(h.Inlines, line[(level + 1)..]);
                doc.Blocks.Add(h);
                i++;
                continue;
            }
            bool bullet = Regex.IsMatch(line, @"^(?:[-*+]\s+|\d+\.\s+)");
            if (bullet)
                line = "•  " + Regex.Replace(line, @"^(?:[-*+]\s+|\d+\.\s+)", "");
            if (!bullet)
            {
                i++;
                while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]) &&
                    !Regex.IsMatch(lines[i].Trim(), @"^(?:#{1,4}\s|[-*+]\s|\d+\.\s|\|)"))
                    line += " " + lines[i++].Trim();
            }
            else i++;
            AddParagraph(doc, line, bullet);
        }
        return doc;
    }

    private static void AddParagraph(FlowDocument doc, string text, bool bullet)
    {
        var p = new Paragraph { Margin = new Thickness(bullet ? 9 : 0, 2, 0, bullet ? 5 : 10) };
        AddInline(p.Inlines, text);
        doc.Blocks.Add(p);
    }

    private static void AddInline(InlineCollection collection, string text)
    {
        var tokens = Regex.Matches(text, @"\*\*[^*]+\*\*|\*[^*]+\*");
        int start = 0;
        foreach (Match token in tokens)
        {
            if (token.Index > start) collection.Add(new Run(text[start..token.Index]));
            var value = token.Value;
            if (value.StartsWith("**")) collection.Add(new Bold(new Run(value[2..^2])));
            else collection.Add(new Italic(new Run(value[1..^1])));
            start = token.Index + token.Length;
        }
        if (start < text.Length) collection.Add(new Run(text[start..]));
    }
}
