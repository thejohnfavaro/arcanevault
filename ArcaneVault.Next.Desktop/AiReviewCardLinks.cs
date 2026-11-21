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

    /// <summary>
    /// Emit verified card hyperlinks as the document is built. Never alter a
    /// live InlineCollection during enumeration; WPF invalidates its iterator
    /// when a Run is inserted or removed.
    /// </summary>
    internal static void AppendVerifiedText(InlineCollection target, string text,
        IReadOnlyList<string>? verifiedNames, Action<string>? inspect)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (inspect is null || verifiedNames is null || verifiedNames.Count == 0)
        {
            target.Add(new Run(text));
            return;
        }

        // Construct a standalone list of intervals before adding any content.
        // Prefer the longest matching card on overlap, e.g. "Tithe" versus
        // "Smothering Tithe", and preserve every unlinked character.
        var ranges = new List<(int Start, int End, string Name)>();
        foreach (string name in verifiedNames)
        {
            if (string.IsNullOrWhiteSpace(name)) continue;
            string pattern = @"(?<![\p{L}\p{N}])" + Regex.Escape(name) +
                @"(?![\p{L}\p{N}])";
            foreach (Match match in Regex.Matches(text, pattern,
                         RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                ranges.Add((match.Index, match.Index + match.Length, name));
        }
        if (ranges.Count == 0)
        {
            target.Add(new Run(text));
            return;
        }
        ranges.Sort((left, right) =>
        {
            int start = left.Start.CompareTo(right.Start);
            return start != 0 ? start : (right.End - right.Start).CompareTo(left.End - left.Start);
        });

        int cursor = 0;
        foreach (var range in ranges)
        {
            if (range.Start < cursor) continue;
            if (range.Start > cursor)
                target.Add(new Run(text[cursor..range.Start]));

            string selectedName = range.Name;
            var link = new Hyperlink(new Run(text[range.Start..range.End]))
            {
                Foreground = Brushes.Goldenrod,
                TextDecorations = TextDecorations.Underline,
                ToolTip = "Open full card details: " + selectedName
            };
            link.Click += (_, _) => inspect(selectedName);
            target.Add(link);
            cursor = range.End;
        }
        if (cursor < text.Length)
            target.Add(new Run(text[cursor..]));
    }
}
