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
    // Only deliberate [[card:Full Card Name]] references can become links.
    // Never scan ordinary prose for catalog names: "Brawl", "Fell" and many
    // other legitimate cards are also everyday words.
    private static readonly Regex ExplicitCard = new(
        @"\[\[card:([^\[\]\r\n]{2,150})\]\]",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static IReadOnlyList<string> ExplicitNames(string response) =>
        ExplicitCard.Matches(response ?? string.Empty)
            .Select(m => m.Groups[1].Value.Trim())
            .Where(n => n.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(150).ToArray();

    internal static void AppendVerifiedText(InlineCollection target, string text,
        IReadOnlyList<string>? verifiedNames, Action<string>? inspect)
    {
        if (string.IsNullOrEmpty(text)) return;
        int cursor = 0;
        foreach (Match match in ExplicitCard.Matches(text))
        {
            if (match.Index > cursor)
                target.Add(new Run(text[cursor..match.Index]));

            string shown = match.Groups[1].Value.Trim();
            string? verified = verifiedNames?.FirstOrDefault(name =>
                string.Equals(name, shown, StringComparison.OrdinalIgnoreCase));
            if (verified is not null && inspect is not null)
            {
                var link = new Hyperlink(new Run(shown))
                {
                    Foreground = Brushes.Goldenrod,
                    TextDecorations = TextDecorations.Underline,
                    ToolTip = "Open full card details: " + verified
                };
                link.Click += (_, _) => inspect(verified);
                target.Add(link);
            }
            else
                target.Add(new Run(shown));
            cursor = match.Index + match.Length;
        }
        if (cursor < text.Length)
            target.Add(new Run(text[cursor..]));
    }
}
