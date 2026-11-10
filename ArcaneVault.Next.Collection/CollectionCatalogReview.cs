using ArcaneVault.Next.Domain;
using Microsoft.Data.Sqlite;

namespace ArcaneVault.Next.Collection;

/// <summary>
/// Read-only comparison of an exported physical inventory with a catalog snapshot.
/// Identities and copies are never inferred from card names or set hints.
/// </summary>
public static class CollectionCatalogReview
{
    public sealed record PrintingRow(
        NativePhysicalRow Original,
        bool Confirmed,
        string Status,
        string ConfirmedSet,
        string CatalogName,
        string CatalogCollector)
    {
        public string Name => Original.Name;
        public int Quantity => Original.Quantity;
        public string SetAndNumber => Original.SetAndNumber;
        public string Finish => Original.Finish;
        public string PrintId => Original.ScryfallId;
        public string SetCode => Confirmed ? ConfirmedSet : "";
    }

    public sealed record SetSummary(string SetCode, int DistinctPrintings, long Copies);

    public sealed record Result(
        bool CatalogVerified,
        IReadOnlyList<PrintingRow> Rows,
        long TotalCopies,
        long ConfirmedCopies,
        long UnresolvedCopies,
        int ConfirmedDistinctPrintings,
        IReadOnlyList<SetSummary> ConfirmedSets)
    {
        public bool QuantitiesConserved => ConfirmedCopies + UnresolvedCopies == TotalCopies;
    }

    private sealed record Printing(Guid Id, string Name, string Set, string Collector, bool IsPaper);

    private static SqliteConnection Open(string path)
    {
        var db = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false
        }.ToString());
        db.Open();
        return db;
    }

    /// <summary>
    /// Null catalog = no verification. Otherwise scan trusted printing IDs using
    /// read-only SQLite and compare both ID and available source metadata.
    /// </summary>
    public static Result Examine(NativeBackupView backup, string? catalogPath,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(backup);
        bool verified = !string.IsNullOrWhiteSpace(catalogPath);
        var presentIds = new HashSet<Guid>();
        foreach (var row in backup.PhysicalCards)
            if (Guid.TryParse(row.ScryfallId, out var id) && id != Guid.Empty)
                presentIds.Add(id);

        var matches = new Dictionary<Guid, Printing>();
        if (verified)
        {
            using var db = Open(catalogPath!);
            using var command = db.CreateCommand();
            command.CommandText = "SELECT print_id,name,setcode,collector,games,digital FROM variants";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                cancellation.ThrowIfCancellationRequested();
                if (reader.IsDBNull(0) || !Guid.TryParse(reader.GetString(0), out var id) ||
                    id == Guid.Empty || !presentIds.Contains(id))
                    continue;
                if (reader.IsDBNull(1) || reader.IsDBNull(2) || reader.IsDBNull(3) ||
                    reader.IsDBNull(4) || reader.IsDBNull(5))
                    continue;
                bool hasPaper = reader.GetString(4).Split(',',
                    StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .Any(g => g.Equals("paper", StringComparison.OrdinalIgnoreCase));
                var record = new Printing(id, reader.GetString(1), reader.GetString(2),
                    reader.GetString(3), hasPaper && reader.GetInt64(5) == 0);
                if (!matches.TryAdd(id, record))
                    throw new InvalidDataException("Catalog contains a duplicated Scryfall printing ID.");
            }
        }

        var rows = new List<PrintingRow>(backup.PhysicalCards.Count);
        var sets = new Dictionary<string, (HashSet<Guid> Ids, long Copies)>(
            StringComparer.OrdinalIgnoreCase);
        long confirmed = 0, unresolved = 0;
        var allConfirmed = new HashSet<Guid>();
        foreach (var row in backup.PhysicalCards)
        {
            cancellation.ThrowIfCancellationRequested();
            string status;
            Printing? match = null;
            bool ok = false;
            if (string.IsNullOrWhiteSpace(row.ScryfallId))
                status = "Missing printing ID";
            else if (!Guid.TryParse(row.ScryfallId, out var id) || id == Guid.Empty)
                status = "Invalid printing ID";
            else if (!verified)
                status = "ID supplied — load catalog to verify";
            else if (!matches.TryGetValue(id, out match))
                status = "Printing ID absent from catalog";
            else if (!match.IsPaper)
                status = "Not a physical paper printing";
            else if (string.IsNullOrWhiteSpace(match.Set) ||
                     string.IsNullOrWhiteSpace(match.Collector))
                status = "Catalog printing is missing a set or collector number";
            else if (!string.Equals(row.Name.Trim(), match.Name.Trim(),
                         StringComparison.OrdinalIgnoreCase))
                status = "Card name conflicts with printing ID";
            else if (!string.IsNullOrWhiteSpace(row.SetCode) &&
                     !string.Equals(row.SetCode.Trim(), match.Set.Trim(),
                         StringComparison.OrdinalIgnoreCase))
                status = "Set conflicts with printing ID";
            else if (!string.IsNullOrWhiteSpace(row.CollectorNumber) &&
                     !string.Equals(row.CollectorNumber.Trim(), match.Collector.Trim(),
                         StringComparison.OrdinalIgnoreCase))
                status = "Collector number conflicts with printing ID";
            else
            {
                status = "Verified printing";
                ok = true;
                confirmed = checked(confirmed + row.Quantity);
                allConfirmed.Add(id);
                if (!sets.TryGetValue(match.Set, out var stats))
                    stats = (new HashSet<Guid>(), 0);
                stats.Ids.Add(id);
                stats.Copies = checked(stats.Copies + row.Quantity);
                sets[match.Set] = stats;
            }

            if (!ok)
                unresolved = checked(unresolved + row.Quantity);
            rows.Add(new PrintingRow(row, ok, status,
                ok ? match!.Set : "", ok ? match!.Name : "",
                ok ? match!.Collector : ""));
        }
        if (backup.PhysicalCopies != confirmed + unresolved)
            throw new InvalidDataException("Collection verification failed copy conservation.");

        var perSet = sets.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => new SetSummary(pair.Key, pair.Value.Ids.Count, pair.Value.Copies))
            .ToArray();
        return new Result(verified, rows.AsReadOnly(), backup.PhysicalCopies,
            confirmed, unresolved, allConfirmed.Count, perSet);
    }

    public sealed record CardDetails(
        string Name,
        string TypeLine,
        string OracleText,
        string Rarity,
        double ManaValue,
        string ColorIdentity,
        string Legalities,
        string SetCode);

    /// <summary>Offline card identity details; not a claim about the owned printing.</summary>
    public static CardDetails? ReadDetails(string catalogPath, string name)
    {
        if (string.IsNullOrWhiteSpace(catalogPath) || string.IsNullOrWhiteSpace(name))
            return null;
        using var db = Open(catalogPath);
        using var command = db.CreateCommand();
        command.CommandText = """
            SELECT name,type,oracle,rarity,mv,colors,legalities,setcode
            FROM cards WHERE name=$name COLLATE NOCASE LIMIT 1
            """;
        command.Parameters.AddWithValue("$name", name);
        using var reader = command.ExecuteReader();
        if (!reader.Read()) return null;
        return new CardDetails(reader.GetString(0), reader.GetString(1),
            reader.GetString(2), reader.GetString(3),
            reader.GetDouble(4), reader.GetString(5),
            reader.GetString(6), reader.GetString(7));
    }
}
