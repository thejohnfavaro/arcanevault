using Microsoft.Data.Sqlite;

namespace ArcaneVault.Next.Collection;

/// <summary>
/// Offline, read-only analytics for a draft. Missing Scryfall metadata is
/// accounted for explicitly; unknown cards are never treated as zero-mana.
/// Counts are copies, not unique card names. Lands are excluded from the curve.
/// </summary>
public static class NativeDeckAnalytics
{
    public sealed record Report(int TotalCards, int KnownCards, int UnknownCards,
        int Lands, int Creatures, int Instants, int Sorceries,
        int Artifacts, int Enchantments, int Planeswalkers, int Other,
        IReadOnlyList<int> Curve, IReadOnlyDictionary<string, int> Identity,
        double? AverageNonlandManaValue)
    {
        public string Summary =>
            $"Card types: {Creatures} creatures · {Lands} lands · {Instants} instants · " +
            $"{Sorceries} sorceries · {Artifacts} artifacts · {Enchantments} enchantments · " +
            $"{Planeswalkers} planeswalkers · {Other} other";
        public string CurveSummary =>
            "Mana curve (nonlands): " + string.Join("  ",
                Curve.Select((qty, mv) => $"{(mv == 7 ? "7+" : mv.ToString())}:{qty}")) +
            (AverageNonlandManaValue.HasValue
                ? $"  · average {AverageNonlandManaValue.Value:F2}" : "");
        public string ColorSummary => "Color identity: " +
            string.Join("  ", new[] { "W", "U", "B", "R", "G", "Multicolor", "Colorless" }
                .Select(color => $"{color}:{Identity.GetValueOrDefault(color)}"));
        public string CoverageSummary =>
            $"Catalog matched {KnownCards}/{TotalCards} deck copies" +
            (UnknownCards > 0 ? $" ({UnknownCards} unknown; excluded from charts)" : "");
    }

    public static Report Analyze(IReadOnlyList<NativeDeckEditing.Entry> entries,
        string? commander, string? format, string? catalogPath)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (string.IsNullOrWhiteSpace(catalogPath))
            throw new InvalidDataException("No local Scryfall catalog selected for analytics.");
        bool singleton = string.Equals(format?.Trim(), "Commander", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(format?.Trim(), "Arena Brawl", StringComparison.OrdinalIgnoreCase);
        var quantities = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            if (entry.Quantity < 1) throw new InvalidDataException("Invalid deck quantity.");
            quantities[entry.Name] = checked(quantities.GetValueOrDefault(entry.Name) + entry.Quantity);
        }
        if (singleton && !string.IsNullOrWhiteSpace(commander))
        {
            string name = commander.Trim();
            quantities[name] = checked(quantities.GetValueOrDefault(name) + 1);
        }

        int total = quantities.Values.Sum();
        int known = 0, lands = 0, creatures = 0, instants = 0, sorceries = 0;
        int artifacts = 0, enchantments = 0, planeswalkers = 0, other = 0;
        int[] curve = new int[8];
        var identity = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        double weightedMana = 0;
        int nonlandKnown = 0;

        using var db = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = catalogPath, Mode = SqliteOpenMode.ReadOnly, Pooling = false
        }.ToString());
        db.Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT type,mv,colors FROM cards
            WHERE name=$name COLLATE NOCASE LIMIT 1
            """;
        var nameParam = cmd.Parameters.AddWithValue("$name", "");
        foreach (var entry in quantities)
        {
            nameParam.Value = entry.Key;
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) continue;
            string type = reader.GetString(0);
            double mv = reader.GetDouble(1);
            string colors = reader.GetString(2).ToUpperInvariant();
            int qty = entry.Value;
            if (double.IsNaN(mv) || double.IsInfinity(mv) || mv < 0 ||
                colors.Any(c => !"WUBRG".Contains(c)))
                continue; // malformed records count as unknown
            known = checked(known + qty);
            bool isLand = type.Contains("Land", StringComparison.OrdinalIgnoreCase);
            // Primary type buckets: avoid double counting artifact creatures.
            if (isLand) lands += qty;
            else if (type.Contains("Creature", StringComparison.OrdinalIgnoreCase)) creatures += qty;
            else if (type.Contains("Instant", StringComparison.OrdinalIgnoreCase)) instants += qty;
            else if (type.Contains("Sorcery", StringComparison.OrdinalIgnoreCase)) sorceries += qty;
            else if (type.Contains("Planeswalker", StringComparison.OrdinalIgnoreCase)) planeswalkers += qty;
            else if (type.Contains("Artifact", StringComparison.OrdinalIgnoreCase)) artifacts += qty;
            else if (type.Contains("Enchantment", StringComparison.OrdinalIgnoreCase)) enchantments += qty;
            else other += qty;

            string category = colors.Distinct().Count() switch
            {
                0 => "Colorless", 1 => colors[..1], _ => "Multicolor"
            };
            identity[category] = checked(identity.GetValueOrDefault(category) + qty);
            if (!isLand)
            {
                int bucket = mv >= 7 ? 7 : (int)Math.Floor(mv);
                curve[bucket] += qty;
                weightedMana += mv * qty;
                nonlandKnown += qty;
            }
        }
        return new Report(total, known, total - known,
            lands, creatures, instants, sorceries, artifacts, enchantments,
            planeswalkers, other, curve, identity,
            nonlandKnown > 0 ? weightedMana / nonlandKnown : null);
    }
}
