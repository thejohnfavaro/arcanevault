using ArcaneVault.Next.Domain;
using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace ArcaneVault.Next.EDHLab;

/// <summary>
/// One read-only snapshot of eligible EDH Lab deck candidates. This is a
/// suggestion aid, NOT a final tournament-legality verdict or verification
/// of exact physical printings. User edits still go through NativeDeckPreflight.
/// </summary>
public sealed class EdhLabDraftCatalog
{
    public sealed record Card(string Name, string Type, string Oracle,
        string Colors, string RoleHints, long RecordedCopies, bool IsBasicLand,
        int SuggestedCopyLimit)
    {
        public string Display => Name + "  ·  " +
            (RecordedCopies > 0 ? RecordedCopies + " recorded" : "Missing") +
            (RoleHints.Length > 0 ? "  ·  " + RoleHints : "");
    }

    private readonly IReadOnlyList<Card> cards;
    private readonly IReadOnlyDictionary<string, Card> eligibleByName;
    private readonly int recordedEligibleCount;

    private EdhLabDraftCatalog(IReadOnlyList<Card> candidates)
    {
        cards = candidates.OrderByDescending(x => x.RecordedCopies > 0)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        eligibleByName = cards.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        recordedEligibleCount = cards.Count(x => x.RecordedCopies > 0);
    }

    public int EligibleCount => cards.Count;
    public int RecordedEligibleCount => recordedEligibleCount;

    private static IReadOnlyList<string> Roles(string type, string oracle)
    {
        var hints = new List<string>();
        bool Has(string phrase) => oracle.Contains(phrase, StringComparison.OrdinalIgnoreCase);
        bool isLand = type.Contains("Land", StringComparison.OrdinalIgnoreCase);
        if (isLand) hints.Add("Land");
        // Ordinary mana-producing lands fill land slots, not ramp slots.
        // Search-for-land and Treasure effects may still generate extra mana.
        if ((!isLand && (Has("add {") || Has("add one mana"))) ||
            Has("search your library for a basic land") ||
            Has("search your library for a land") || Has("Treasure token"))
            hints.Add("Ramp");
        if (Has("draw a card") || Has("draw two cards") || Has("draw three cards") ||
            Has("draw X cards")) hints.Add("Card Draw");
        if (Has("destroy target") || Has("exile target") || Has("deals damage to target creature"))
            hints.Add("Removal");
        if (Has("destroy all") || Has("exile all") || Has("each creature gets -") ||
            Has("deals") && Has("damage to each creature"))
            hints.Add("Board Wipe");
        if (Has("search your library for a card") || Has("search your library for an artifact") ||
            Has("search your library for a creature")) hints.Add("Tutor");
        if (Has("hexproof") || Has("indestructible") || Has("ward "))
            hints.Add("Protection");
        if (Has("create") && Has("token")) hints.Add("Token Maker");
        return hints;
    }

    private static int CopyLimit(string type, string oracle)
    {
        if ((type.Contains("Basic", StringComparison.OrdinalIgnoreCase) &&
             type.Contains("Land", StringComparison.OrdinalIgnoreCase)) ||
            oracle.Contains("A deck can have any number of cards named",
                StringComparison.OrdinalIgnoreCase))
            return int.MaxValue;
        var special = System.Text.RegularExpressions.Regex.Match(oracle,
            @"A deck can have up to (\w+) cards named",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase |
            System.Text.RegularExpressions.RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(50));
        if (special.Success)
        {
            string value = special.Groups[1].Value.ToLowerInvariant();
            if (int.TryParse(value, out var count) && count > 0) return count;
            if (value == "seven") return 7;
            if (value == "nine") return 9;
        }
        return 1;
    }

    private static string? Legality(string json, EdhLabFormat format)
    {
        try
        {
            using var parsed = JsonDocument.Parse(json);
            if (parsed.RootElement.ValueKind != JsonValueKind.Object) return null;
            string key = format == EdhLabFormat.ArenaBrawl ? "brawl" : "commander";
            if (parsed.RootElement.TryGetProperty(key, out var value))
                return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            if (format == EdhLabFormat.ArenaBrawl &&
                parsed.RootElement.TryGetProperty("historicbrawl", out value))
                return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        }
        catch (JsonException) { }
        return null;
    }

    public static EdhLabDraftCatalog OpenReadOnly(string catalogPath,
        NativeBackupView inventory, string commander, string commanderColors,
        EdhLabFormat format, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        if (string.IsNullOrWhiteSpace(commander) || commander.Length > 255)
            throw new ArgumentException("Select one commander before drafting.", nameof(commander));
        if (string.IsNullOrWhiteSpace(catalogPath) || !File.Exists(catalogPath))
            throw new FileNotFoundException("EDH Lab needs a synced local Scryfall catalog.", catalogPath);
        int allowed = EdhLabCommanderRules.ColorBits(commanderColors);
        var counts = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        if (format == EdhLabFormat.PaperCommander)
        {
            foreach (var x in inventory.PhysicalCards)
                if (x.Quantity > 0 && !string.IsNullOrWhiteSpace(x.Name))
                    counts[x.Name] = checked(counts.GetValueOrDefault(x.Name) + x.Quantity);
        }
        else
        {
            foreach (var x in inventory.ArenaCards)
                if (x.Quantity > 0 && !string.IsNullOrWhiteSpace(x.Name))
                    counts[x.Name] = checked(counts.GetValueOrDefault(x.Name) + x.Quantity);
        }

        using var db = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = catalogPath, Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString());
        db.Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT name,type,oracle,colors,legalities,arena FROM cards ORDER BY name COLLATE NOCASE";
        var results = new List<Card>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            cancellation.ThrowIfCancellationRequested();
            if (reader.IsDBNull(0) || reader.IsDBNull(3) || reader.IsDBNull(4)) continue;
            string name = reader.GetString(0);
            if (name.Equals(commander, StringComparison.OrdinalIgnoreCase)) continue;
            string colors = reader.GetString(3);
            int identity;
            try { identity = EdhLabCommanderRules.ColorBits(colors); }
            catch (ArgumentException) { continue; } // malformed identity is not colorless
            if ((identity & ~allowed) != 0) continue;
            bool arena = !reader.IsDBNull(5) && reader.GetInt64(5) != 0;
            if (format == EdhLabFormat.ArenaBrawl && !arena) continue;
            string legality = Legality(reader.GetString(4), format) ?? "";
            if (!legality.Equals("legal", StringComparison.OrdinalIgnoreCase))
                continue;
            string type = reader.IsDBNull(1) ? "" : reader.GetString(1);
            string oracle = reader.IsDBNull(2) ? "" : reader.GetString(2);
            bool basic = type.Contains("Basic", StringComparison.OrdinalIgnoreCase) &&
                type.Contains("Land", StringComparison.OrdinalIgnoreCase);
            results.Add(new Card(name, type, oracle, colors,
                string.Join(" · ", Roles(type, oracle)),
                counts.GetValueOrDefault(name), basic, CopyLimit(type, oracle)));
        }
        return new EdhLabDraftCatalog(results);
    }

    public IReadOnlyList<Card> Search(string? text, bool recordedOnly,
        string? role = null, int limit = 200)
    {
        if (limit is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(limit));
        text = (text ?? "").Trim();
        if (text.Length > 255) throw new ArgumentException("Search is too long.", nameof(text));
        var selectedRole = (role ?? "").Trim();
        return cards.Where(x => (!recordedOnly || x.RecordedCopies > 0) &&
                (selectedRole.Length == 0 || x.RoleHints.Split(" · ",
                    StringSplitOptions.RemoveEmptyEntries)
                    .Contains(selectedRole, StringComparer.OrdinalIgnoreCase)) &&
                (text.Length == 0 || x.Name.Contains(text, StringComparison.OrdinalIgnoreCase)))
            .Take(limit).ToArray();
    }

    public bool Contains(string name) => eligibleByName.ContainsKey(name);

    public Card? Find(string name) =>
        eligibleByName.TryGetValue(name, out var card) ? card : null;
}
