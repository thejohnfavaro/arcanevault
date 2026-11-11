using Microsoft.Data.Sqlite;
using ArcaneVault.Next.Domain;

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
        long VerifiedCopies, bool NameCoverage = false, string ArtworkUrl = "")
    {
        public string DisplaySetCode => SetCode.ToUpperInvariant();
        public double Percent => TotalSlots == 0 ? 0 : 100.0 * OwnedSlots / TotalSlots;
        public string PercentLabel => $"{Percent:0.0}%";
        public string SlotsLabel => NameCoverage
            ? $"{OwnedSlots:N0} / {TotalSlots:N0} Arena card names"
            : $"{OwnedSlots:N0} / {TotalSlots:N0}";
        public string CopiesLabel => NameCoverage
            ? $"{OwnedSlots:N0} names covered"
            : $"{VerifiedCopies:N0} copies";
    }

    public sealed record SetSlot(
        string CollectorNumber,
        string CardName,
        long VerifiedCopies, bool NameCoverage = false, string ArtworkUrl = "")
    {
        public string Ownership => NameCoverage
            ? (VerifiedCopies > 0 ? $"Name-owned ({VerifiedCopies:N0})" : "Missing name")
            : (VerifiedCopies > 0 ? $"Owned ({VerifiedCopies:N0})" : "Missing");
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

    private static bool HasCatalogArtwork(SqliteConnection db)
    {
        using var columns = db.CreateCommand();
        columns.CommandText = "PRAGMA table_info(variants)";
        using var rows = columns.ExecuteReader();
        while (rows.Read())
            if (rows.GetString(1).Equals("image", StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    /// <summary>
    /// Use representative, official CARD artwork as a set tile preview.
    /// This must never be described as a promotional set banner or set symbol.
    /// Older local catalogs without image URLs retain text-only tiles.
    /// Requests for thumbnails happen only when the UI virtualizes them.
    /// </summary>
    private static IReadOnlyList<SetRow> AddRepresentativeArt(
        SqliteConnection db, IReadOnlyList<SetRow> sets, bool arena,
        CancellationToken cancellation)
    {
        if (!HasCatalogArtwork(db)) return sets;
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT image FROM variants
            WHERE setcode=$set COLLATE NOCASE AND length(trim(image)) > 0
            """ + (arena
                ? " AND arena=1 AND instr(',' || lower(replace(games,' ','')) || ',', ',arena,') > 0"
                : " AND digital=0 AND instr(',' || lower(replace(games,' ','')) || ',', ',paper,') > 0") +
            """
            
            ORDER BY CASE WHEN collector GLOB '[0-9]*' THEN 0 ELSE 1 END,
                     CAST(collector AS INTEGER),collector COLLATE NOCASE
            LIMIT 1
            """;
        var parameter = cmd.Parameters.Add("$set", SqliteType.Text);
        var items = new List<SetRow>(sets.Count);
        foreach (var set in sets)
        {
            cancellation.ThrowIfCancellationRequested();
            parameter.Value = set.SetCode;
            string url = cmd.ExecuteScalar()?.ToString() ?? "";
            items.Add(set with { ArtworkUrl = url });
        }
        return items;
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
            int slots = checked((int)reader.GetInt64(3));
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
        reader.Close();
        var artSets = AddRepresentativeArt(db, sets, arena: false, cancellation);

        return new Result(artSets
            .OrderByDescending(s => s.Released, StringComparer.Ordinal)
            .ThenBy(s => s.SetName, StringComparer.OrdinalIgnoreCase)
            .ToArray(), slotsTotal, ownedSlotsTotal, matchedCopies);
    }

    /// <summary>
    /// Arena set-name COVERAGE, not set printing ownership. Arena inventory
    /// contains names and quantities only, so a matching card name is credited
    /// once within each set of Arena-available card names. The same reprinted
    /// name may cover several sets, but never establishes owning those printings.
    /// Physical verified slot accounting remains completely separate.
    /// </summary>
    public static Result BuildArena(IReadOnlyList<NativeArenaRow> arenaCards,
        string catalogPath, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(arenaCards);
        var owned = ArenaNames(arenaCards);
        var sets = new List<SetRow>();
        int total = 0, matched = 0;
        using var db = OpenReadOnly(catalogPath);
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT setcode,MAX(NULLIF(setname,'')),
                   MIN(CASE WHEN length(release)=10 THEN release END),
                   name
            FROM variants
            WHERE arena=1
              AND instr(',' || lower(replace(games,' ','')) || ',', ',arena,') > 0
              AND length(trim(setcode))>0 AND length(trim(name))>0
            GROUP BY setcode COLLATE NOCASE,name COLLATE NOCASE
            ORDER BY setcode COLLATE NOCASE,name COLLATE NOCASE
            """;
        using var reader = cmd.ExecuteReader();
        string? currentSet = null, currentName = null, currentRelease = null;
        int names = 0, covered = 0;
        void Finish()
        {
            if (string.IsNullOrWhiteSpace(currentSet) || names == 0) return;
            total = checked(total + names);
            matched = checked(matched + covered);
            sets.Add(new SetRow(currentSet, currentName ?? currentSet,
                currentRelease ?? "", names, covered, 0, NameCoverage: true));
        }
        while (reader.Read())
        {
            cancellation.ThrowIfCancellationRequested();
            var set = reader.GetString(0).Trim();
            var title = reader.IsDBNull(1) ? set : reader.GetString(1);
            var release = reader.IsDBNull(2) ? "" : reader.GetString(2);
            var name = reader.GetString(3).Trim();
            if (!set.Equals(currentSet, StringComparison.OrdinalIgnoreCase))
            {
                Finish();
                currentSet = set;
                currentName = title;
                currentRelease = release;
                names = 0;
                covered = 0;
            }
            names++;
            if (owned.ContainsKey(name)) covered++;
        }
        Finish();
        reader.Close();
        var artSets = AddRepresentativeArt(db, sets, arena: true, cancellation);
        return new Result(artSets.OrderByDescending(x => x.Released, StringComparer.Ordinal)
            .ThenBy(x => x.SetName, StringComparer.OrdinalIgnoreCase)
            .ToArray(), total, matched, 0);
    }

    private static Dictionary<string, long> ArenaNames(IReadOnlyList<NativeArenaRow> cards)
    {
        var result = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in cards)
        {
            if (row.Quantity <= 0 || string.IsNullOrWhiteSpace(row.Name)) continue;
            string name = row.Name.Trim();
            result[name] = checked(result.GetValueOrDefault(name) + row.Quantity);
        }
        return result;
    }

    /// <summary>
    /// A name-based Arena availability checklist. The collector number is
    /// a catalog reference only and is NEVER evidence of an owned printing.
    /// </summary>
    public static IReadOnlyList<SetSlot> LoadArenaSetNames(
        IReadOnlyList<NativeArenaRow> arenaCards, string catalogPath,
        string setCode, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(arenaCards);
        if (string.IsNullOrWhiteSpace(setCode))
            throw new InvalidDataException("Select an Arena-available set.");
        var owned = ArenaNames(arenaCards);
        var result = new List<SetSlot>();
        using var db = OpenReadOnly(catalogPath);
        using var cmd = db.CreateCommand();
        string artColumn = HasCatalogArtwork(db)
            ? "MIN(NULLIF(image,''))" : "''";
        cmd.CommandText = $"""
            SELECT MIN(collector),name,{artColumn} FROM variants
            WHERE setcode=$set COLLATE NOCASE AND arena=1
              AND instr(',' || lower(replace(games,' ','')) || ',', ',arena,') > 0
              AND length(trim(name))>0
            GROUP BY name COLLATE NOCASE
            ORDER BY name COLLATE NOCASE
            """;
        cmd.Parameters.AddWithValue("$set", setCode.Trim());
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            cancellation.ThrowIfCancellationRequested();
            string name = reader.GetString(1).Trim();
            result.Add(new SetSlot(
                reader.IsDBNull(0) ? "" : reader.GetString(0),
                name, owned.GetValueOrDefault(name), NameCoverage: true,
                ArtworkUrl: reader.IsDBNull(2) ? "" : reader.GetString(2)));
        }
        return result.AsReadOnly();
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
        // One collector number can appear on multiple catalog records. Show a
        // verified owned card's name when present; never imply an unowned
        // catalog variant is the exact card the user owns.
        var verifiedNames = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in verification.Rows)
        {
            if (!line.Confirmed ||
                !string.Equals(line.ConfirmedSet.Trim(), setCode.Trim(), StringComparison.OrdinalIgnoreCase))
                continue;
            if (!verifiedNames.TryGetValue(line.CatalogCollector.Trim(), out var names))
            {
                names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                verifiedNames.Add(line.CatalogCollector.Trim(), names);
            }
            names.Add(line.CatalogName);
        }
        var result = new List<SetSlot>();
        using var db = OpenReadOnly(catalogPath);
        using var cmd = db.CreateCommand();
        string artColumn = HasCatalogArtwork(db)
            ? "MIN(NULLIF(image,''))" : "''";
        cmd.CommandText = $"""
            SELECT collector,MIN(name),COUNT(DISTINCT name COLLATE NOCASE),{artColumn}
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
            if (copies > 0 && verifiedNames.TryGetValue(collector, out var names))
            {
                name = string.Join(" / ", names.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).Take(3));
                if (names.Count > 3) name += " / …";
            }
            else if (reader.GetInt64(2) > 1)
                name += " (multiple catalog names at this number)";
            result.Add(new SetSlot(collector, name, copies,
                ArtworkUrl: reader.IsDBNull(3) ? "" : reader.GetString(3)));
        }
        if (ownedSlots is not null && ownedSlots.Count > result.Count)
            throw new InvalidDataException("Catalog slots do not contain every verified owned collector number.");
        return result.AsReadOnly();
    }
}
