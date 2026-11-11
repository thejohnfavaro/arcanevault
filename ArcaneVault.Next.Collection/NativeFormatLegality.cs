using System.Text.Json;

namespace ArcaneVault.Next.Collection;

/// <summary>
/// Presentation-only adapter for Scryfall's cached legalities JSON.
/// Stored JSON stays unchanged for offline validation and future catalog syncs.
/// This parser never interprets an unknown status as legal.
/// </summary>
public static class NativeFormatLegality
{
    public sealed record Row(string Code, string Name, string StatusCode, string StatusText);
    public sealed record View(IReadOnlyList<Row> Rows, string Summary, bool Available)
    {
        public int LegalCount => Rows.Count(r => r.StatusCode == "legal");
        public int BannedCount => Rows.Count(r => r.StatusCode == "banned");
        public int RestrictedCount => Rows.Count(r => r.StatusCode == "restricted");
    }

    private static readonly string[] PreferredOrder =
    {
        "standard", "pioneer", "modern", "legacy", "vintage", "pauper",
        "commander", "brawl", "standardbrawl", "historic", "timeless",
        "alchemy", "explorer", "paupercommander", "oathbreaker", "duel",
        "predh", "premodern", "oldschool", "gladiator", "future", "penny"
    };

    private static readonly Dictionary<string, string> Names =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["standard"] = "Standard", ["future"] = "Future Standard",
            ["pioneer"] = "Pioneer", ["modern"] = "Modern",
            ["legacy"] = "Legacy", ["vintage"] = "Vintage",
            ["pauper"] = "Pauper", ["commander"] = "Commander",
            ["brawl"] = "Brawl", ["standardbrawl"] = "Standard Brawl",
            ["historic"] = "Historic", ["timeless"] = "Timeless",
            ["alchemy"] = "Alchemy", ["explorer"] = "Explorer",
            ["paupercommander"] = "Pauper Commander",
            ["oathbreaker"] = "Oathbreaker", ["duel"] = "Duel Commander",
            ["predh"] = "PreDH", ["premodern"] = "Premodern",
            ["oldschool"] = "Old School", ["gladiator"] = "Gladiator",
            ["penny"] = "Penny Dreadful"
        };

    private static string FriendlyName(string code)
    {
        if (Names.TryGetValue(code, out string? name)) return name;
        string normalized = code.Replace('_', ' ').Replace('-', ' ').Trim();
        if (normalized.Length > 45) normalized = normalized[..45];
        return string.Join(" ", normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => char.ToUpperInvariant(word[0]) + word[1..]));
    }

    public static View Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new View(Array.Empty<Row>(), "Format legality unavailable", false);
        if (raw.Length > 64_000)
            return new View(Array.Empty<Row>(), "Format legality metadata is invalid", false);

        try
        {
            using var json = JsonDocument.Parse(raw, new JsonDocumentOptions
            {
                MaxDepth = 8, AllowTrailingCommas = false
            });
            if (json.RootElement.ValueKind != JsonValueKind.Object)
                return new View(Array.Empty<Row>(), "Format legality metadata is invalid", false);

            var results = new List<Row>();
            foreach (var item in json.RootElement.EnumerateObject())
            {
                if (string.IsNullOrWhiteSpace(item.Name) || item.Name.Length > 64 ||
                    item.Value.ValueKind != JsonValueKind.String)
                    continue;
                string key = item.Name.Trim().ToLowerInvariant();
                string status = item.Value.GetString()?.Trim().ToLowerInvariant() ?? "";
                string label = status switch
                {
                    "legal" => "Legal",
                    "not_legal" => "Not legal",
                    "banned" => "Banned",
                    "restricted" => "Restricted",
                    _ => "Unknown"
                };
                if (label == "Unknown") status = "unknown";
                results.Add(new Row(key, FriendlyName(key), status, label));
            }
            if (results.Count == 0)
                return new View(Array.Empty<Row>(), "No cached format legality data", false);

            results.Sort((a, b) =>
            {
                int x = Array.IndexOf(PreferredOrder, a.Code);
                int y = Array.IndexOf(PreferredOrder, b.Code);
                x = x == -1 ? int.MaxValue : x;
                y = y == -1 ? int.MaxValue : y;
                return x != y ? x.CompareTo(y) : string.Compare(
                    a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
            int legal = results.Count(r => r.StatusCode == "legal");
            int banned = results.Count(r => r.StatusCode == "banned");
            int restricted = results.Count(r => r.StatusCode == "restricted");
            string summary = $"{results.Count} formats · {legal} legal";
            if (banned > 0) summary += $" · {banned} banned";
            if (restricted > 0) summary += $" · {restricted} restricted";
            return new View(results.AsReadOnly(), summary, true);
        }
        catch (JsonException)
        {
            return new View(Array.Empty<Row>(),
                "Format legality metadata is invalid", false);
        }
    }
}
