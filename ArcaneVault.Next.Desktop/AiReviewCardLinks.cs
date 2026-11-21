using ArcaneVault.Next.Collection;
using ArcaneVault.Next.Domain;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// Cross-platform AI card link policy: only names exactly verified against the
/// installed local catalog can become links. AI text is untrusted text.
/// </summary>
internal static class AiReviewCardLinks
{
    internal static IReadOnlyList<string> Candidates(string response,
        string mainboard, string commander)
    {
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return;
            var cleaned = Regex.Replace(raw.Trim(), @"^[\s•*+\-]+", "");
            cleaned = Regex.Replace(cleaned, @"^\d{1,3}\s+(?:x\s+)?", "");
            cleaned = Regex.Replace(cleaned, @"(?i)^(?:add|cut|swap|remove|replace):\s*", "");
            cleaned = cleaned.Trim(' ', '*', '\'', '"', '.', ':', ';');
            cleaned = ArenaDeckCardName.Identity(cleaned);
            if (cleaned.Length is >= 2 and <= 110 &&
                cleaned is not ("Cut" or "Add" or "Card" or "Why"))
                candidates.Add(cleaned);
        }
        // The response goes FIRST. Otherwise a 100-card deck can exhaust
        // the lookup budget before any proposed additions are considered.
        foreach (Match m in Regex.Matches(response ?? "",
                     @"\*\*([^*\r\n]{2,110})\*\*"))
        {
            if (candidates.Count >= 80) break;
            Add(m.Groups[1].Value);
        }
        foreach (string line in (response ?? "").Split('\n'))
        {
            if (candidates.Count >= 100) break;
            if (!line.TrimStart().StartsWith('|')) continue;
            string[] cells = line.Trim().Trim('|').Split('|');
            var headers = cells.Select(x => x.Trim().ToLowerInvariant()).ToArray();
            if (headers.Contains("cut") || headers.Contains("add") ||
                headers.Contains("priority")) continue;
            // Standard AI layout: Priority | Cut | Add | Why.
            // Tables without priority commonly use Cut | Add | Why.
            int offset = cells.Length >= 4 &&
                int.TryParse(cells[0].Trim(), out _) ? 1 : 0;
            for (int col = offset; col < Math.Min(offset + 2, cells.Length); col++)
                if (!Regex.IsMatch(cells[col].Trim(), @"^[-:\s]+$"))
                    Add(cells[col]);
        }
        Add(commander);
        try
        {
            foreach (var entry in NativeDeckEditing.ParseMainboard(mainboard))
                Add(entry.Name);
        }
        catch (Exception)
        {
            // A malformed unsaved draft cannot prevent links to valid,
            // catalog-verified recommendations from rendering.
        }
        return candidates.Take(250).ToArray();
    }

    internal static void Apply(FlowDocument document, IEnumerable<string> validated,
        Action<string> inspect)
    {
        var names = validated.Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(x => x.Length).ToArray();
        if (names.Length == 0) return;
        foreach (Block block in document.Blocks) VisitBlock(block, names, inspect);
    }

    private static void VisitBlock(Block block, string[] names, Action<string> inspect)
    {
        switch (block)
        {
            case Paragraph p: Visit(p.Inlines, names, inspect); break;
            case Table t:
                foreach (TableRowGroup group in t.RowGroups)
                foreach (TableRow row in group.Rows)
                foreach (TableCell cell in row.Cells)
                foreach (Block child in cell.Blocks)
                    VisitBlock(child, names, inspect);
                break;
            case Section section:
                foreach (Block child in section.Blocks)
                    VisitBlock(child, names, inspect);
                break;
            case System.Windows.Documents.List list:
                foreach (ListItem item in list.ListItems)
                foreach (Block child in item.Blocks)
                    VisitBlock(child, names, inspect);
                break;
        }
    }

    private static void Visit(InlineCollection inlines, string[] names,
        Action<string> inspect)
    {
        // Snapshot because every Run may be replaced with several new inlines.
        foreach (Inline original in inlines.Cast<Inline>().ToArray())
        {
            if (original is Run run)
            {
                string content = run.Text;
                var ranges = new List<(int Start, int End, string Card)>();
                foreach (string name in names)
                {
                    var expression = @"(?<![\p{L}\p{N}])" +
                        Regex.Escape(name) + @"(?![\p{L}\p{N}])";
                    foreach (Match match in Regex.Matches(content, expression,
                                 RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                        ranges.Add((match.Index, match.Index + match.Length, name));
                }
                if (ranges.Count == 0) continue;
                ranges.Sort((a, b) =>
                {
                    int first = a.Start.CompareTo(b.Start);
                    return first != 0 ? first : (b.End - b.Start).CompareTo(a.End - a.Start);
                });
                int offset = 0;
                bool linked = false;
                foreach (var range in ranges)
                {
                    if (range.Start < offset) continue;
                    if (range.Start > offset)
                        inlines.InsertBefore(run, new Run(content[offset..range.Start]));
                    var link = new Hyperlink(new Run(content[range.Start..range.End]))
                    {
                        Foreground = Brushes.Goldenrod,
                        TextDecorations = TextDecorations.Underline,
                        ToolTip = "Open full card details: " + range.Card
                    };
                    string selected = range.Card;
                    link.Click += (_, _) => inspect(selected);
                    inlines.InsertBefore(run, link);
                    linked = true;
                    offset = range.End;
                }
                if (linked)
                {
                    if (offset < content.Length)
                        inlines.InsertBefore(run, new Run(content[offset..]));
                    inlines.Remove(run);
                }
            }
            else if (original is Span span)
                Visit(span.Inlines, names, inspect);
        }
    }
}
