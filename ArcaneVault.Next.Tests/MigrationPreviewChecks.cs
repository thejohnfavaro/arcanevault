using ArcaneVault.Next.Migration;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text.Json;

internal static class MigrationPreviewChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Migration preview: " + message);
    }

    private static bool Rejects(Action action)
    {
        try { action(); return false; }
        catch (InvalidDataException) { return true; }
    }

    public static void Run(string work)
    {
        var original = Path.Combine(work, "export-v2.json");
        var sqlite = Path.Combine(work, "scryfall.sqlite");
        var firstId = "00000000-0000-0000-0000-000000000101";
        var secondId = "00000000-0000-0000-0000-000000000102";
        var backup = new
        {
            schema = 2,
            cards = new[] { new { name = "Test Arena Card", qty = 2 } },
            physicalCards = new object[]
            {
                new { name = "Split // Face", qty = 2, set = "AAA", number = "1", finish = "foil", scryfallId = firstId },
                new { name = "Split // Face", qty = 1, set = "BBB", number = "2", finish = "nonfoil", scryfallId = secondId },
                new { name = "Split // Face", qty = 3, set = "AAA", number = "1", finish = "etched", scryfallId = firstId },
                new { name = "Name-only", qty = 4, set = "AAA", number = "1", finish = "nonfoil", scryfallId = "" },
                new { name = "Unknown Id", qty = 2, set = "ZZZ", number = "4", finish = "nonfoil", scryfallId = "00000000-0000-0000-0000-000000000999" },
                new { name = "Corrupt Id", qty = 1, set = "AAA", number = "3", finish = "nonfoil", scryfallId = "not-uuid" },
                new { name = "Conflicted Hint", qty = 1, set = "WRONG", number = "1", finish = "nonfoil", scryfallId = firstId },
                new { name = "Split // Face", qty = 2, set = "AAA", number = "1", finish = "foil", scryfallId = firstId }
            },
            deckLibrary = new[] { new { name = "Synthetic deck", entries = Array.Empty<object>(),
                revisions = new[] { new { name = "Old", entries = Array.Empty<object>() } } } },
            preferredArtwork = new Dictionary<string, string> { ["sample"] = "https://example.invalid/art" }
        };
        File.WriteAllText(original, JsonSerializer.Serialize(backup));
        using (var db = new SqliteConnection(new SqliteConnectionStringBuilder
               { DataSource = sqlite, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false }.ToString()))
        {
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE variants(print_id TEXT PRIMARY KEY, name TEXT, setcode TEXT, collector TEXT);
                INSERT INTO variants VALUES ('00000000-0000-0000-0000-000000000101','Split // Face','AAA','1');
                INSERT INTO variants VALUES ('00000000-0000-0000-0000-000000000102','Split // Face','BBB','2');
                CREATE TABLE ownership(name TEXT, qty INT);
                INSERT INTO ownership VALUES('Private synthetic ownership mirror',42);
                """;
            cmd.ExecuteNonQuery();
        }
        var backupHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(original)));
        var sqliteHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sqlite)));
        var backupMtime = File.GetLastWriteTimeUtc(original);
        var catalogMtime = File.GetLastWriteTimeUtc(sqlite);

        var noCatalog = MigrationPreview.Analyze(original);
        Check(!noCatalog.CatalogVerified && !noCatalog.Migrated && noCatalog.ReadOnly, "No catalog means never confirmed.");
        Check(noCatalog.ConfirmedPrintingCopies == 0 && noCatalog.UnresolvedPrintingCopies == 16,
            "Name/set hints or UUID alone must not count as confirmed.");
        Check(noCatalog.UnresolvedCopiesByReason["CatalogNotProvided"] == 11,
            "Valid-but-unverified printing ID copies were lost.");
        var result = MigrationPreview.Analyze(original, sqlite);
        Check(result.CatalogVerified && result.ReadOnly && !result.Migrated, "Preview safety flags.");
        Check(result.Format == "full-backup-v2" && result.ArenaCopies == 2, "Backup format or Arena count.");
        Check(result.PhysicalRows == 8 && result.PhysicalCopies == 16 && result.PhysicalNames == 5,
            "Physical rows, names or total copies changed.");
        Check(result.RowsWithValidPrintId == 6 && result.CopiesWithValidPrintId == 11,
            "Print ID counts incorrect.");
        Check(result.ConfirmedPrintingCopies == 8 && result.UnresolvedPrintingCopies == 8,
            "Confirmed/unresolved totals incorrect.");
        Check(result.ConfirmedDistinctPrintings == 2 && result.ConfirmedSets == 2,
            "Same printing should only contribute one distinct printing to set.");
        Check(result.UnresolvedCopiesByReason["MissingPrintId"] == 4 &&
              result.UnresolvedCopiesByReason["MalformedPrintId"] == 1 &&
              result.UnresolvedCopiesByReason["PrintIdAbsentFromCatalog"] == 2 &&
              result.UnresolvedCopiesByReason["SourceMetadataConflict"] == 1,
            "Unresolved source categories are incorrect.");
        Check(result.DuplicatePhysicalIdentities == 1 && result.Decks == 1 &&
              result.DeckRevisions == 1 && result.PreferredArtwork == 1, "Other preserved section counts.");
        Check(JsonSerializer.Serialize(result).Contains("Split // Face") == false,
            "Aggregate-only report must not contain card names.");
        Check(backupHash == Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(original))) &&
              sqliteHash == Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sqlite))) &&
              File.GetLastWriteTimeUtc(original) == backupMtime &&
              File.GetLastWriteTimeUtc(sqlite) == catalogMtime,
            "Preview wrote to existing files.");

        var sync = Path.Combine(work, "sync-v1.json");
        File.WriteAllText(sync, JsonSerializer.Serialize(new
        {
            format = "arcane-vault-collection-sync", schema = 1,
            physicalCards = new[] { new { name = "Unmatched", qty = 1, set = "AAA", number = "1" } },
            arenaCards = Array.Empty<object>()
        }));
        var syncReport = MigrationPreview.Analyze(sync, sqlite);
        Check(syncReport.Decks is null && syncReport.PreferredArtwork is null &&
              syncReport.Warnings.Contains("SYNC_SNAPSHOT_NOT_A_FULL_BACKUP"),
            "Portable sync must never masquerade as a full backup.");
        var invalid = Path.Combine(work, "invalid-preview.json");
        File.WriteAllText(invalid, "{\"cards\":[{\"name\":\"Card\",\"qty\":-1}]}");
        Check(Rejects(() => MigrationPreview.Analyze(invalid)), "Negative quantity accepted.");
        File.WriteAllText(invalid, "{\"schema\":99,\"cards\":[]}");
        Check(Rejects(() => MigrationPreview.Analyze(invalid)), "Unknown schema accepted.");
        File.WriteAllText(invalid, "{\"schema\":2,\"cards\":[],\"physicalCards\":[{\"name\":\"Card\",\"qty\":1.5}]}");
        Check(Rejects(() => MigrationPreview.Analyze(invalid)), "Fractional physical quantity accepted.");

        Console.WriteLine("PASS: AVN-005 read-only migration preview, strict catalog-verified printings, 16-copy parity, non-destructive input hashes and legacy snapshot warnings");
    }
}
