using ArcaneVault.Next.Domain;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ArcaneVault.Next.Migration;

/// <summary>
/// AVN-005 read-only extraction preview from explicitly selected exported JSON.
/// Never opens a live WebView profile or changes, stages, restores or migrates data.
/// If supplied, Scryfall printing identities are validated against an existing
/// read-only SQLite catalog (variants.print_id, setcode, collector).
/// </summary>
public static class MigrationPreview
{
    private const long MaximumBackupBytes = 100 * 1024 * 1024;
    private const int MaximumRows = 150_000;

    public sealed record Report(
        string Format,
        bool ReadOnly,
        bool Migrated,
        bool CatalogVerified,
        int? ArenaRows,
        long? ArenaCopies,
        int? PhysicalRows,
        int? PhysicalNames,
        long? PhysicalCopies,
        int? RowsWithValidPrintId,
        long? CopiesWithValidPrintId,
        long? ConfirmedPrintingCopies,
        int? ConfirmedDistinctPrintings,
        int? ConfirmedSets,
        long? UnresolvedPrintingCopies,
        int? DuplicatePhysicalIdentities,
        int? Decks,
        int? DeckRevisions,
        int? PreferredArtwork,
        IReadOnlyDictionary<string, long> UnresolvedCopiesByReason,
        IReadOnlyList<string> Warnings);

    private static JsonElement ArrayOrMissing(JsonElement root, string field)
    {
        if (!root.TryGetProperty(field, out var value))
            return default;
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() > MaximumRows)
            throw new InvalidDataException("A backup section has an invalid type or too many rows.");
        return value;
    }

    private static string Text(JsonElement obj, string field, bool required = false)
    {
        if (!obj.TryGetProperty(field, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            if (required) throw new InvalidDataException("Backup is missing a required text field.");
            return "";
        }
        if (value.ValueKind != JsonValueKind.String)
            throw new InvalidDataException("Backup has a malformed text field.");
        var text = value.GetString() ?? "";
        if (required && string.IsNullOrWhiteSpace(text))
            throw new InvalidDataException("Backup has an empty required text field.");
        return text;
    }

    private static int Quantity(JsonElement row, bool allowZero)
    {
        if (!row.TryGetProperty("qty", out var value) ||
            value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var qty) ||
            qty > 99_999 || (allowZero ? qty < 0 : qty <= 0))
            throw new InvalidDataException("Backup has an invalid card quantity.");
        return qty;
    }

    private static JsonElement ObjectRow(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Backup contains a non-object row.");
        return value;
    }

    private static Guid PreviewLineId(int ordinal)
    {
        // Ephemeral read-model identifiers ONLY; not identifiers for import or sync.
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes("ArcaneVault AVN-005 preview row " + ordinal));
        return new Guid(bytes.AsSpan(0, 16));
    }

    private static IReadOnlyList<CatalogPrinting> ReadCatalog(string path)
    {
        if (!File.Exists(path))
            throw new InvalidDataException("The selected SQLite catalog is missing.");
        // Read-only SQLite mode allows the installed app to keep its WAL and
        // never opens the source in a writable mode.
        var result = new List<CatalogPrinting>();
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false
        }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT print_id,name,setcode,collector FROM variants";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (reader.IsDBNull(0) || reader.IsDBNull(1) || reader.IsDBNull(2) || reader.IsDBNull(3))
                continue;
            if (!Guid.TryParse(reader.GetString(0), out var printId))
                continue; // Non-Scryfall/non-UUID catalog keys cannot confirm ownership.
            result.Add(new CatalogPrinting(printId,
                reader.GetString(1), reader.GetString(2), reader.GetString(3)));
        }
        return result;
    }

    private static void Add(Dictionary<string, long> reasons, string reason, long copies)
    {
        reasons[reason] = checked(reasons.GetValueOrDefault(reason) + copies);
    }

    public static Report Analyze(string backupPath, string? catalogPath = null)
    {
        if (string.IsNullOrWhiteSpace(backupPath))
            throw new ArgumentException("Select an exported backup JSON.", nameof(backupPath));
        var stat = new FileInfo(backupPath);
        if (!stat.Exists || stat.Length > MaximumBackupBytes)
            throw new InvalidDataException("Backup must exist and be no larger than 100 MiB.");
        // The source is read ONLY; no profile paths or database file are ever created.
        using var input = new FileStream(backupPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var document = JsonDocument.Parse(input);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Expected an Arcane Vault backup object.");

        bool sync = Text(root, "format") == "arcane-vault-collection-sync";
        string format;
        if (sync)
        {
            if (!root.TryGetProperty("schema", out var schema) || !schema.TryGetInt32(out var version) || version != 1)
                throw new InvalidDataException("Unsupported collection transfer schema.");
            format = "collection-sync-v1";
        }
        else if (root.TryGetProperty("cards", out var cards) && cards.ValueKind == JsonValueKind.Array)
        {
            if (root.TryGetProperty("schema", out var schema) &&
                (!schema.TryGetInt32(out var version) || version != 2))
                throw new InvalidDataException("Unsupported full backup schema.");
            format = "full-backup-v2";
        }
        else
            throw new InvalidDataException("Unrecognized exported backup or collection transfer.");

        var warnings = new SortedSet<string>(StringComparer.Ordinal);
        var arena = ArrayOrMissing(root, sync ? "arenaCards" : "cards");
        var physical = ArrayOrMissing(root, "physicalCards");
        var decks = ArrayOrMissing(root, "deckLibrary");
        int? arenaRows = null, physicalRows = null, physicalNames = null;
        long? arenaCopies = null, physicalCopies = null;
        int? deckCount = null, deckRevisions = null, preferredArtwork = null;
        int? validIdRows = null, duplicateRows = null;
        long? validIdCopies = null, confirmed = null, unresolved = null;
        int? distinct = null, confirmedSets = null;
        var breakdown = new Dictionary<string, long>(StringComparer.Ordinal);

        if (arena.ValueKind == JsonValueKind.Array)
        {
            long copies = 0;
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var element in arena.EnumerateArray())
            {
                var row = ObjectRow(element);
                var name = Text(row, "name", required: true).Trim();
                if (!names.Add(name))
                    throw new InvalidDataException("Backup contains duplicate Arena names.");
                copies = checked(copies + Quantity(row, allowZero: true));
            }
            arenaRows = arena.GetArrayLength();
            arenaCopies = copies;
        }

        if (physical.ValueKind == JsonValueKind.Array)
        {
            var rows = new List<PhysicalInventoryLine>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var identities = new HashSet<string>(StringComparer.Ordinal);
            var invalidIds = new HashSet<Guid>();
            int withIds = 0, duplicates = 0;
            long copies = 0, withIdCopies = 0;
            int ordinal = 0;
            foreach (var element in physical.EnumerateArray())
            {
                var row = ObjectRow(element);
                var name = Text(row, "name", required: true);
                int qty = Quantity(row, allowZero: false);
                string set = Text(row, "set");
                string collector = Text(row, "number");
                string finish = Text(row, "finish");
                string print = Text(row, "scryfallId").Trim();
                var previewId = PreviewLineId(ordinal++);
                Guid? printId = null;
                if (print.Length > 0)
                {
                    if (Guid.TryParse(print, out var parsed) && parsed != Guid.Empty)
                    {
                        printId = parsed;
                        withIds++;
                        withIdCopies = checked(withIdCopies + qty);
                    }
                    else invalidIds.Add(previewId);
                }
                rows.Add(new PhysicalInventoryLine(previewId, name, qty, printId,
                    set, collector, string.IsNullOrWhiteSpace(finish) ? "unknown" : finish));
                names.Add(name.Trim());
                copies = checked(copies + qty);
                // Serialize tuple rather than joining with a delimiter to avoid ambiguous keys.
                string identity = JsonSerializer.Serialize(new[] { name.Trim().ToLowerInvariant(),
                    set.Trim().ToLowerInvariant(), collector.Trim().ToLowerInvariant(),
                    finish.Trim().ToLowerInvariant(), Text(row, "language").Trim().ToLowerInvariant(),
                    Text(row, "condition").Trim().ToLowerInvariant(), Text(row, "location").Trim().ToLowerInvariant(),
                    print.ToLowerInvariant() });
                if (!identities.Add(identity)) duplicates++;
            }
            if (duplicates > 0) warnings.Add("DUPLICATE_PHYSICAL_IDENTITY_ROWS");
            if (invalidIds.Count > 0) warnings.Add("MALFORMED_PRINT_IDS");
            physicalRows = rows.Count;
            physicalNames = names.Count;
            physicalCopies = copies;
            validIdRows = withIds;
            validIdCopies = withIdCopies;
            duplicateRows = duplicates;

            if (string.IsNullOrWhiteSpace(catalogPath))
            {
                // A syntactically valid Scryfall ID alone is NOT catalog verified.
                foreach (var row in rows)
                {
                    var reason = invalidIds.Contains(row.LineId) ? "MalformedPrintId" :
                        row.PrintId is null ? "MissingPrintId" : "CatalogNotProvided";
                    Add(breakdown, reason, row.Quantity);
                }
                confirmed = 0;
                unresolved = copies;
                distinct = 0;
                confirmedSets = 0;
                warnings.Add("CATALOG_NOT_VERIFIED");
            }
            else
            {
                var result = PrintingReconciliation.Reconcile(rows, ReadCatalog(catalogPath));
                confirmed = result.ConfirmedCopies;
                unresolved = result.UnresolvedCopies;
                distinct = result.ConfirmedDistinctPrintings;
                confirmedSets = result.ConfirmedBySet.Count;
                foreach (var item in result.UnresolvedLines)
                {
                    var reason = invalidIds.Contains(item.LineId) ? "MalformedPrintId" :
                        item.Reason switch
                        {
                            UnresolvedReason.NoPrintId => "MissingPrintId",
                            UnresolvedReason.PrintIdAbsentFromCatalog => "PrintIdAbsentFromCatalog",
                            UnresolvedReason.SourceMetadataConflict => "SourceMetadataConflict",
                            _ => throw new InvalidDataException("Unknown unresolved printing reason.")
                        };
                    Add(breakdown, reason, item.Copies);
                }
            }
            if (unresolved > 0) warnings.Add("UNRESOLVED_PRINTING_COPIES");
            if (confirmed + unresolved != physicalCopies ||
                breakdown.Values.Aggregate(0L, (sum, count) => checked(sum + count)) != unresolved)
                throw new InvalidDataException("Reconciliation failed physical quantity conservation.");
        }
        else
        {
            warnings.Add("PHYSICAL_INVENTORY_ABSENT");
        }

        if (arena.ValueKind != JsonValueKind.Array) warnings.Add("ARENA_INVENTORY_ABSENT");
        if (decks.ValueKind == JsonValueKind.Array)
        {
            int revisions = 0;
            foreach (var element in decks.EnumerateArray())
            {
                var row = ObjectRow(element);
                Text(row, "name", required: true);
                if (ArrayOrMissing(row, "entries").ValueKind != JsonValueKind.Array)
                    throw new InvalidDataException("Deck has no entries array.");
                var revisionList = ArrayOrMissing(row, "revisions");
                if (revisionList.ValueKind == JsonValueKind.Array)
                    revisions = checked(revisions + revisionList.GetArrayLength());
            }
            deckCount = decks.GetArrayLength();
            deckRevisions = revisions;
        }
        else if (!sync)
            warnings.Add("DECK_LIBRARY_ABSENT");

        if (root.TryGetProperty("preferredArtwork", out var art))
        {
            if (art.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("Preferred artwork section is invalid.");
            preferredArtwork = art.EnumerateObject().Count();
        }
        else if (!sync)
            warnings.Add("PREFERRED_ARTWORK_ABSENT");
        if (sync) warnings.Add("SYNC_SNAPSHOT_NOT_A_FULL_BACKUP");
        if (sync && physical.ValueKind != JsonValueKind.Array && arena.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Collection transfer contains no inventory.");

        return new Report(format, true, false, !string.IsNullOrWhiteSpace(catalogPath),
            arenaRows, arenaCopies, physicalRows, physicalNames, physicalCopies,
            validIdRows, validIdCopies, confirmed, distinct, confirmedSets, unresolved,
            duplicateRows, deckCount, deckRevisions, preferredArtwork,
            new SortedDictionary<string, long>(breakdown, StringComparer.Ordinal),
            warnings.ToArray());
    }
}
