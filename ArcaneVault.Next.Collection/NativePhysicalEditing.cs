using ArcaneVault.Next.Domain;
using Microsoft.Data.Sqlite;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ArcaneVault.Next.Collection;

/// <summary>
/// AVN-024: explicitly selected PAPER printing edits only. Catalog data is read-only;
/// no name-to-printing inference and no changes to the installed legacy profile.
/// Persistence and automatic rollback are delegated to NativeWorkspaceSafety.
/// </summary>
public static class NativePhysicalEditing
{
    public sealed record PrintingCandidate(Guid PrintId, string Name, string SetCode,
        string CollectorNumber);

    public sealed record SearchResult(IReadOnlyList<PrintingCandidate> Rows, bool HasMore);

    private static SqliteConnection OpenCatalog(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new InvalidDataException("Load a complete local Scryfall catalog before editing ownership.");
        var db = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false
        }.ToString());
        db.Open();
        return db;
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    public static SearchResult FindPaperPrintings(string catalogPath, string search,
        int limit = 75)
    {
        if (string.IsNullOrWhiteSpace(search) || search.Trim().Length < 2)
            throw new ArgumentException("Enter at least two characters to find a printing.", nameof(search));
        if (limit is < 1 or > 200) throw new ArgumentOutOfRangeException(nameof(limit));

        using var db = OpenCatalog(catalogPath);
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT print_id,name,setcode,collector FROM variants
            WHERE name LIKE $prefix ESCAPE '\' COLLATE NOCASE
              AND digital=0
              AND instr(',' || lower(replace(games,' ','')) || ',', ',paper,') > 0
            ORDER BY name COLLATE NOCASE, setcode COLLATE NOCASE, collector
            LIMIT $limit
            """;
        cmd.Parameters.AddWithValue("$prefix", EscapeLike(search.Trim()) + "%");
        cmd.Parameters.AddWithValue("$limit", limit + 1);
        var results = new List<PrintingCandidate>();
        using var rows = cmd.ExecuteReader();
        while (rows.Read())
        {
            if (rows.IsDBNull(0) || rows.IsDBNull(1) || rows.IsDBNull(2) || rows.IsDBNull(3) ||
                !Guid.TryParse(rows.GetString(0), out var id) || id == Guid.Empty)
                continue;
            results.Add(new PrintingCandidate(id, rows.GetString(1),
                rows.GetString(2), rows.GetString(3)));
            if (results.Count > limit) break;
        }
        return new SearchResult(results.Take(limit).ToArray(), results.Count > limit);
    }

    /// <summary>Revalidate the ID at write time, independent of the search result.</summary>
    public static PrintingCandidate RequirePaperPrinting(string catalogPath, Guid printId)
    {
        if (printId == Guid.Empty) throw new InvalidDataException("Select a valid Scryfall printing ID.");
        using var db = OpenCatalog(catalogPath);
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            SELECT name,setcode,collector,games,digital FROM variants
            WHERE print_id=$id COLLATE NOCASE LIMIT 2
            """;
        cmd.Parameters.AddWithValue("$id", printId.ToString("D"));
        using var row = cmd.ExecuteReader();
        if (!row.Read() || row.IsDBNull(0) || row.IsDBNull(1) ||
            row.IsDBNull(2) || row.IsDBNull(3) || row.IsDBNull(4))
            throw new InvalidDataException("Selected printing is not in the local Scryfall catalog.");
        var name = row.GetString(0).Trim();
        var set = row.GetString(1).Trim();
        var number = row.GetString(2).Trim();
        var paper = row.GetString(3).Split(',',
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Any(game => game.Equals("paper", StringComparison.OrdinalIgnoreCase));
        if (row.GetInt64(4) != 0 || !paper ||
            string.IsNullOrWhiteSpace(name) || set.Length == 0 || number.Length == 0)
            throw new InvalidDataException("This is not a complete, physical paper printing.");
        if (row.Read())
            throw new InvalidDataException("Ambiguous duplicated printing ID in the local catalog.");
        return new PrintingCandidate(printId, name, set, number);
    }

    /// <summary>
    /// Pure, validated schema-2 transformation. All OTHER backup sections and
    /// unknown future keys are preserved; matches are by exact print UUID and
    /// finish, never by card name alone. Multiple matching legacy lines fail closed.
    /// </summary>
    public static byte[] AddCopies(byte[] completeBackup, PrintingCandidate selected,
        int additionalQuantity, string finish)
    {
        ArgumentNullException.ThrowIfNull(completeBackup);
        ArgumentNullException.ThrowIfNull(selected);
        if (additionalQuantity is < 1 or > 99_999)
            throw new ArgumentOutOfRangeException(nameof(additionalQuantity));
        if (finish is not ("nonfoil" or "foil" or "etched"))
            throw new ArgumentException("Choose nonfoil, foil, or etched.", nameof(finish));
        if (selected.PrintId == Guid.Empty || string.IsNullOrWhiteSpace(selected.Name) ||
            string.IsNullOrWhiteSpace(selected.SetCode) ||
            string.IsNullOrWhiteSpace(selected.CollectorNumber))
            throw new InvalidDataException("Invalid verified catalog printing.");

        var before = NativeBackupReader.Read(completeBackup);
        var json = JsonNode.Parse(completeBackup) as JsonObject ??
            throw new InvalidDataException("Invalid full backup root.");
        var physical = json["physicalCards"] as JsonArray ??
            throw new InvalidDataException("Full backup has no physical collection.");
        var matches = new List<JsonObject>();
        foreach (var item in physical)
        {
            if (item is not JsonObject row)
                throw new InvalidDataException("Invalid physical ownership row.");
            if (!Guid.TryParse(row["scryfallId"]?.GetValue<string>(), out var id) ||
                id != selected.PrintId ||
                !string.Equals(row["finish"]?.GetValue<string>() ?? "",
                    finish, StringComparison.OrdinalIgnoreCase))
                continue;

            // Never merge conflicting metadata or silently 'fix' a legacy row.
            string name = row["name"]?.GetValue<string>() ?? "";
            string set = row["set"]?.GetValue<string>() ?? "";
            string number = row["number"]?.GetValue<string>() ?? "";
            if (!string.Equals(name.Trim(), selected.Name, StringComparison.OrdinalIgnoreCase) ||
                (set.Length > 0 && !string.Equals(set.Trim(),
                    selected.SetCode, StringComparison.OrdinalIgnoreCase)) ||
                (number.Length > 0 && !string.Equals(number.Trim(),
                    selected.CollectorNumber, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException(
                    "An existing line with this printing ID conflicts with catalog metadata. No edit applied.");
            matches.Add(row);
        }

        if (matches.Count > 1)
            throw new InvalidDataException(
                "More than one existing line has the same printing and finish. Resolve duplicates before editing.");
        if (matches.Count == 1)
        {
            var existing = matches[0];
            int quantity = existing["qty"]!.GetValue<int>();
            if ((long)quantity + additionalQuantity > 99_999)
                throw new InvalidDataException("Adding copies exceeds the supported 99,999 per-line limit.");
            existing["qty"] = quantity + additionalQuantity;
        }
        else
        {
            if (physical.Count >= 150_000)
                throw new InvalidDataException("Physical collection row limit reached.");
            physical.Add(new JsonObject
            {
                ["name"] = selected.Name,
                ["qty"] = additionalQuantity,
                ["set"] = selected.SetCode,
                ["number"] = selected.CollectorNumber,
                ["finish"] = finish,
                ["scryfallId"] = selected.PrintId.ToString("D")
            });
        }

        byte[] result = JsonSerializer.SerializeToUtf8Bytes(json);
        var after = NativeBackupReader.Read(result);
        if (after.PhysicalCopies != checked(before.PhysicalCopies + additionalQuantity) ||
            after.ArenaCopies != before.ArenaCopies ||
            after.Decks != before.Decks ||
            after.DeckRevisions != before.DeckRevisions ||
            after.PreferredArtworks != before.PreferredArtworks ||
            after.PhysicalCards.Count != before.PhysicalCards.Count + (matches.Count == 0 ? 1 : 0))
            throw new InvalidDataException("Native edit failed collection conservation checks.");
        return result;
    }
    /// <summary>
    /// Modify ONLY the specifically selected native physical row. Unlike adding
    /// copies, edits may target name-only/unresolved legacy rows:
    /// deleting one user's selected line needs no guessed printing identity.
    /// The ordinal AND complete original row AND workspace SHA must match.
    /// Zero removes that ONE line rather than storing a zero-quantity tombstone.
    /// </summary>
    public static byte[] SetCopies(byte[] completeBackup, int physicalOrdinal,
        NativePhysicalRow expectedRow, int newQuantity)
    {
        ArgumentNullException.ThrowIfNull(completeBackup);
        ArgumentNullException.ThrowIfNull(expectedRow);
        if (newQuantity is < 0 or > 99_999)
            throw new ArgumentOutOfRangeException(nameof(newQuantity));

        var before = NativeBackupReader.Read(completeBackup);
        if (physicalOrdinal < 0 || physicalOrdinal >= before.PhysicalCards.Count)
            throw new InvalidDataException("Selected physical row no longer exists.");
        var oldRow = before.PhysicalCards[physicalOrdinal];
        if (oldRow != expectedRow)
            throw new InvalidDataException("Selected physical row changed since it was displayed.");
        if (newQuantity == oldRow.Quantity)
            throw new InvalidDataException("Quantity is unchanged; no recovery point was created.");

        var json = JsonNode.Parse(completeBackup) as JsonObject ??
            throw new InvalidDataException("Invalid full backup root.");
        var physical = json["physicalCards"] as JsonArray ??
            throw new InvalidDataException("Full backup has no physical collection.");
        if (physical.Count != before.PhysicalCards.Count ||
            physical[physicalOrdinal] is not JsonObject line)
            throw new InvalidDataException("Physical row count or payload does not match the source.");
        if (newQuantity == 0)
            physical.RemoveAt(physicalOrdinal);
        else
            line["qty"] = newQuantity;

        byte[] result = JsonSerializer.SerializeToUtf8Bytes(json);
        var after = NativeBackupReader.Read(result);
        var expectedCopies = checked(before.PhysicalCopies -
            oldRow.Quantity + newQuantity);
        if (after.PhysicalCopies != expectedCopies ||
            after.ArenaCopies != before.ArenaCopies ||
            after.ArenaCards.Count != before.ArenaCards.Count ||
            after.Decks != before.Decks ||
            after.DeckRevisions != before.DeckRevisions ||
            after.PreferredArtworks != before.PreferredArtworks ||
            after.PhysicalCards.Count != before.PhysicalCards.Count -
                (newQuantity == 0 ? 1 : 0))
            throw new InvalidDataException("Physical edit failed collection conservation checks.");

        // Immutable rows on either side of the exact ordinal must survive.
        for (int i = 0; i < before.PhysicalCards.Count; i++)
        {
            if (i == physicalOrdinal) continue;
            int target = i > physicalOrdinal && newQuantity == 0 ? i - 1 : i;
            if (before.PhysicalCards[i] != after.PhysicalCards[target])
                throw new InvalidDataException("Physical edit affected an unrelated ownership row.");
        }
        return result;
    }

}
