using ArcaneVault.Next.Domain;
using Microsoft.Data.Sqlite;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ArcaneVault.Next.Collection;

/// <summary>
/// Read-only, best-effort saved-deck preflight. A warning is advisory, never
/// a mutation or a claim that Arena accepted the deck. Query only the offline
/// Scryfall catalog and the independently verified native inventory.
/// </summary>
public static class NativeDeckPreflight
{
    public enum Level { Warning, Review }
    public sealed record Finding(Level Severity, string Message);
    public sealed record Report(bool CatalogChecked, string OwnershipPool,
        IReadOnlyList<Finding> Findings)
    {
        public int Warnings => Findings.Count(x => x.Severity == Level.Warning);
        public int Reviews => Findings.Count(x => x.Severity == Level.Review);
        public string Summary =>
            $"{Warnings} warning(s), {Reviews} review note(s) — {OwnershipPool} inventory";
    }

    private sealed record Card(string Type, string Oracle, string Colors,
        string Legalities, bool HasArenaPrinting);

    private static readonly Regex LimitText = new(
        @"A deck can have up to (\w+) cards named",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static bool IsBasic(Card card) =>
        card.Type.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Any(t => t.Equals("Basic", StringComparison.OrdinalIgnoreCase)) &&
        card.Type.Contains("Land", StringComparison.OrdinalIgnoreCase);

    private static int CopyLimit(Card card, bool singleton)
    {
        if (IsBasic(card) ||
            card.Oracle.Contains("A deck can have any number of cards named",
                StringComparison.OrdinalIgnoreCase)) return int.MaxValue;

        var match = LimitText.Match(card.Oracle);
        if (match.Success)
        {
            string word = match.Groups[1].Value.ToLowerInvariant();
            if (int.TryParse(word, out int number) && number >= 1) return number;
            if (word == "seven") return 7;
            if (word == "nine") return 9;
        }
        return singleton ? 1 : 4;
    }

    private static string? CommanderLegality(Card card)
    {
        try
        {
            using var doc = JsonDocument.Parse(card.Legalities);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("commander", out var property) &&
                property.ValueKind == JsonValueKind.String)
                return property.GetString();
        }
        catch (JsonException) { }
        return null;
    }

    public static Report Check(NativeBackupView ownership,
        IReadOnlyList<NativeDeckEditing.Entry> entries, string commander,
        string format, string? catalogPath)
    {
        ArgumentNullException.ThrowIfNull(ownership);
        ArgumentNullException.ThrowIfNull(entries);
        commander = (commander ?? "").Trim();
        format = (format ?? "").Trim();
        bool edh = format.Equals("Commander", StringComparison.OrdinalIgnoreCase);
        bool brawl = format.Equals("Arena Brawl", StringComparison.OrdinalIgnoreCase);
        bool regular = format.Equals("60-card", StringComparison.OrdinalIgnoreCase);
        bool singleton = edh || brawl;
        string pool = brawl ? "Arena" : edh ? "Physical" : "Physical / Arena (separate)";
        var notes = new List<Finding>();
        void Warn(string message) => notes.Add(new Finding(Level.Warning, message));
        void Review(string message) => notes.Add(new Finding(Level.Review, message));

        int mainboardCount = entries.Sum(x => x.Quantity);
        if (singleton)
        {
            if (commander.Length == 0)
                Warn("A Commander / Arena Brawl deck needs a commander.");
            int total = mainboardCount + (commander.Length > 0 ? 1 : 0);
            if (total != 100)
                Warn($"{format}: {total} cards including the separate commander; expected 100.");
            if (commander.Length > 0 && entries.Any(e =>
                e.Name.Equals(commander, StringComparison.OrdinalIgnoreCase)))
                Warn("The chosen commander is also listed in the mainboard; remove the extra mainboard copy.");
        }
        else if (regular && mainboardCount < 60)
            Warn($"60-card: {mainboardCount} mainboard copies; expected at least 60.");
        else if (!regular && !singleton)
            Review("Custom format: deck-size, singleton and format legality rules are not assumed.");

        if (!singleton && commander.Length > 0)
            Review("Commander field is set for a non-Commander format; it is not included in the mainboard count.");

        var physical = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var arena = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var x in ownership.PhysicalCards)
            physical[x.Name] = checked(physical.GetValueOrDefault(x.Name) + x.Quantity);
        foreach (var x in ownership.ArenaCards)
            arena[x.Name] = checked(arena.GetValueOrDefault(x.Name) + x.Quantity);

        var desired = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
            desired[entry.Name] = checked(desired.GetValueOrDefault(entry.Name) + entry.Quantity);
        if (singleton && commander.Length > 0)
            desired[commander] = checked(desired.GetValueOrDefault(commander) + 1);

        foreach (var pair in desired.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            long paper = physical.GetValueOrDefault(pair.Key);
            long digital = arena.GetValueOrDefault(pair.Key);
            if (edh && paper < pair.Value)
                Warn($"Physical inventory: {pair.Key} needs {pair.Value}, owned {paper}.");
            else if (brawl && digital < pair.Value)
                Warn($"Arena inventory: {pair.Key} needs {pair.Value}, owned {digital}.");
            else if (!singleton && regular && paper < pair.Value && digital < pair.Value)
                Review($"Neither inventory individually covers {pair.Value}× {pair.Key} (paper {paper}, Arena {digital}).");
        }

        var records = new Dictionary<string, Card>(StringComparer.OrdinalIgnoreCase);
        bool catalogChecked = false;
        if (!string.IsNullOrWhiteSpace(catalogPath))
        {
            try
            {
                using var db = new SqliteConnection(new SqliteConnectionStringBuilder
                {
                    DataSource = catalogPath,
                    Mode = SqliteOpenMode.ReadOnly,
                    Pooling = false
                }.ToString());
                db.Open();
                using var command = db.CreateCommand();
                command.CommandText = """
                    SELECT type,oracle,colors,legalities,arena
                    FROM cards WHERE name=$name COLLATE NOCASE LIMIT 1
                    """;
                var param = command.Parameters.AddWithValue("$name", "");
                foreach (string name in desired.Keys)
                {
                    param.Value = name;
                    using var reader = command.ExecuteReader();
                    if (!reader.Read()) continue;
                    records.Add(name, new Card(reader.GetString(0),
                        reader.GetString(1), reader.GetString(2),
                        reader.GetString(3), reader.GetInt64(4) != 0));
                }
                catalogChecked = true;
            }
            catch (Exception ex) when (ex is SqliteException or IOException or ArgumentException or
                UnauthorizedAccessException)
            {
                Review("Local Scryfall catalog was unavailable or missing required columns; " +
                    "card metadata was not validated. No collection data was changed.");
            }
        }
        if (!catalogChecked)
            Review("No readable offline card catalog: color identity, printing availability, " +
                "card-specific copy exceptions and Commander ban data are unverified.");

        string commanderColors = "";
        bool commanderColorKnown = false;
        if (singleton && commander.Length > 0 && records.TryGetValue(commander, out var cmd))
        {
            commanderColors = cmd.Colors.ToUpperInvariant();
            commanderColorKnown = commanderColors.All(c => "WUBRG".Contains(c));
            bool isLegend = cmd.Type.Contains("Legendary", StringComparison.OrdinalIgnoreCase);
            bool creature = cmd.Type.Contains("Creature", StringComparison.OrdinalIgnoreCase);
            bool walker = cmd.Type.Contains("Planeswalker", StringComparison.OrdinalIgnoreCase);
            bool special = cmd.Oracle.Contains("can be your commander", StringComparison.OrdinalIgnoreCase);
            // Brawl commander rules include some noncreature card types; do not
            // categorically reject vehicles, spacecraft, backgrounds or partner decks.
            if (!special && !((isLegend && creature) || (brawl && isLegend && walker)))
                Review($"{commander}: commander eligibility requires a manual rules check.");
            if (brawl && !cmd.HasArenaPrinting)
                Warn($"{commander}: no Arena printing found in this local catalog.");
        }
        else if (singleton && commander.Length > 0 && catalogChecked)
            Review($"Commander not found in offline catalog: {commander}; color checks are incomplete.");

        foreach (var entry in entries)
        {
            if (!records.TryGetValue(entry.Name, out var card))
            {
                if (catalogChecked)
                    Review($"Card not found in offline catalog: {entry.Name}; legality and copies unverified.");
                continue;
            }

            if (singleton && commanderColorKnown && card.Colors.All(c => "WUBRG".Contains(char.ToUpperInvariant(c))))
            {
                var invalid = card.Colors.ToUpperInvariant().Distinct()
                    .Where(c => !commanderColors.Contains(c)).ToArray();
                if (invalid.Length > 0)
                    Warn($"{entry.Name}: color identity {card.Colors} is outside commander identity " +
                         (commanderColors.Length == 0 ? "colorless" : commanderColors) + ".");
            }
            else if (singleton && !commanderColorKnown)
            {
                // Only one review note (above) is needed for a missing commander.
            }
            else if (singleton && card.Colors.Any(c => !"WUBRG".Contains(char.ToUpperInvariant(c))))
                Review($"{entry.Name}: color identity metadata is malformed; manual check required.");

            if ((singleton || regular) && entry.Quantity > CopyLimit(card, singleton))
                Warn($"{entry.Name}: {entry.Quantity} copies exceed its catalog-supported " +
                    $"{(singleton ? "singleton" : "four-of")} limit.");

            if (brawl && !card.HasArenaPrinting)
                Warn($"{entry.Name}: no Arena printing found in this local catalog.");

            if (edh)
            {
                string? status = CommanderLegality(card);
                if (status is "banned" or "not_legal")
                    Warn($"{entry.Name}: cached Scryfall Commander legality is '{status}'.");
                else if (status is null)
                    Review($"{entry.Name}: Commander legality metadata is missing or unreadable.");
            }
        }

        if (edh && commander.Length > 0 && records.TryGetValue(commander, out var leader))
        {
            string? status = CommanderLegality(leader);
            if (status is "banned" or "not_legal")
                Warn($"{commander}: cached Scryfall Commander legality is '{status}'.");
        }
        if (brawl)
            Review("Arena Brawl's live ban list, commander-specific bans and queue restrictions " +
                "are NOT verified by this offline Scryfall snapshot.");
        if (edh)
            Review("Commander checks use the local catalog snapshot and do not cover " +
                "partner/background combinations or every special deckbuilding exception.");
        return new Report(catalogChecked, pool, notes.AsReadOnly());
    }
}
