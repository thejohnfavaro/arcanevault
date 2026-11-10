using Microsoft.Data.Sqlite;

namespace ArcaneVault.Next.Collection;

/// <summary>
/// Read-only set checklist over catalog-backed paper collector-number slots.
/// This is not a claim about a standard booster checklist: promo, token and
/// other numbered paper printings remain included if present in the catalog.
/// </summary>
public static class NativeSetProgress
{
    public sealed record SetRow(
        string SetCode,
        string SetName,
        string Released,
        int TotalSlots,
        int OwnedSlots,
        long VerifiedCopies)
    {
        public double Percent => TotalSlots == 0 ? 0 : 100.0 * OwnedSlots / TotalSlots;
        public string PercentLabel => $"{Percent:0.0}%";
        public string SlotsLabel => $"{OwnedSlots:N0} / {TotalSlots:N0}";
    }

    public sealed record SetSlot(
        string CollectorNumber,
        string CardName,
        long VerifiedCopies)
    {
        public string Ownership => VerifiedCopies > 0 ? $"Owned ({VerifiedCopies:N0})" : "Missing";
    }

    public sealed record Result(
        IReadOnlyList<SetRow> Sets,
        int CatalogPaperSlots,
        int ConfirmedOwnedSlots,
        long ConfirmedOwnedCopies);

    private static SqliteConnection OpenReadOnly(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new FileNotFoundException("Select a complete Scryfall catalog before Set Progress.");
        var db = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false
        }.ToString());
        db.Open();
        return db;
    }

    private static Dictionary<string, Dictionary<string, long>> OwnedBySet(
        CollectionCatalogReview.Result verification)
    {
        var owned = new Dictionary<string, Dictionary<string, long>>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var line in verification.Rows)
        {
            if (!line.Confirmed) continue;
            if (string.IsNullOrWhiteSpace(line.ConfirmedSet) ||
                string.IsNullOrWhiteSpace(line.CatalogCollector))
                throw new InvalidDataException("Catalog-verified printing lacks a set or collector number.");
            var set = line.ConfirmedSet.Trim();
            var collector = line.CatalogCollector.Trim();
            if (!owned.TryGetValue(set, out var slots))
            {
                slots = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                owned.Add(set, slots);
            }
            slots[collector] = checked(slots.GetValueOrDefault(collector) + line.Quantity);
        }
        return owned;
    }

    /// <summary>
    /// A single indexed SQLite aggregation for all set denominators. Print
    /// counts are distinct catalog collector-number slots, not copies or
    /// distinct Scryfall print UUIDs. No user data or catalog is written.
    /// </summary>
    public static Result Build(CollectionCatalogReview.Result verification,
        string catalogPath, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(verification);
        if (!verification.CatalogVerified)
            throw new InvalidDataException("Set Progress requires an independently verified catalog.");

        var owned = OwnedBySet(verification);
        var sets = new List<SetRow>();
        int slotsTotal = 0, ownedSlotsTotal = 0;
        long matchedCopies = 0;
        using var db = OpenReadOnly(catalogPath);
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT setcode,
                   MAX(NULLIF(setname,'')),
                   MIN(CASE WHEN length(release)=10 THEN release END),
                   COUNT(DISTINCT collector COLLATE NOCASE)
            FROM variants
            WHERE digital=0
              AND instr(',' || lower(replace(games,' ','')) || ',', ',paper,') > 0
              AND length(trim(setcode)) > 0 AND length(trim(collector)) > 0
            GROUP BY setcode COLLATE NOCASE
            """;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            cancellation.ThrowIfCancellationRequested();
            string set = reader.GetString(0).Trim();
            string name = reader.IsDBNull(1) ? set : reader.GetString(1);
            string release = reader.IsDBNull(2) ? "" : reader.GetString(2);
            int slots = reader.GetInt32(3);
            if (slots <= 0) continue;
            owned.TryGetValue(set, out var verifiedSlots);
            int confirmedCount = verifiedSlots?.Count ?? 0;
            if (confirmedCount > slots)
                throw new InvalidDataException("Confirmed ownership exceeds catalog set slots.");
            long copies = verifiedSlots?.Values.Sum() ?? 0L;
            matchedCopies = checked(matchedCopies + copies);
            slotsTotal = checked(slotsTotal + slots);
            ownedSlotsTotal = checked(ownedSlotsTotal + confirmedCount);
            sets.Add(new SetRow(set, name, release, slots, confirmedCount, copies));
        }
        if (matchedCopies != verification.ConfirmedCopies)
            throw new InvalidDataException("Set Progress did not conserve all verified owned copies.");

        return new Result(sets
            .OrderByDescending(s => s.Released, StringComparer.Ordinal)
            .ThenBy(s => s.SetName, StringComparer.OrdinalIgnoreCase)
            .ToArray(), slotsTotal, ownedSlotsTotal, matchedCopies);
    }

    /// <summary>
    /// Load only the selected set, sorted by numeric collector number. Multiple
    /// verified copies/finishes of one numbered catalog slot are consolidated.
    /// </summary>
    public static IReadOnlyList<SetSlot> LoadSetSlots(
        CollectionCatalogReview.Result verification,
        string catalogPath,
        string setCode,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(verification);
        if (!verification.CatalogVerified || string.IsNullOrWhiteSpace(setCode))
            throw new InvalidDataException("Choose a catalog-backed set.");
        var ownership = OwnedBySet(verification);
        ownership.TryGetValue(setCode.Trim(), out var ownedSlots);
        var result = new List<SetSlot>();
        using var db = OpenReadOnly(catalogPath);
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT collector,MIN(name)
            FROM variants
            WHERE setcode=$set COLLATE NOCASE
              AND digital=0
              AND instr(',' || lower(replace(games,' ','')) || ',', ',paper,') > 0
              AND length(trim(collector)) > 0
            GROUP BY collector COLLATE NOCASE
            ORDER BY
              CASE WHEN collector GLOB '[0-9]*' THEN 0 ELSE 1 END,
              CAST(collector AS INTEGER), collector COLLATE NOCASE
            """;
        cmd.Parameters.AddWithValue("$set", setCode.Trim());
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            cancellation.ThrowIfCancellationRequested();
            string collector = reader.GetString(0).Trim();
            string name = reader.IsDBNull(1) ? "" : reader.GetString(1);
            long copies = ownedSlots?.GetValueOrDefault(collector) ?? 0;
            result.Add(new SetSlot(collector, name, copies));
        }
        if (ownedSlots is not null && ownedSlots.Count > result.Count)
            throw new InvalidDataException("Catalog slots do not contain every verified owned collector number.");
        return result.AsReadOnly();
    }
}
