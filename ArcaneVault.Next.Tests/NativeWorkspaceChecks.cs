using ArcaneVault.Next.Collection;
using ArcaneVault.Next.Desktop;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text.Json;

internal static class NativeWorkspaceChecks
{
    private static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException("Native workspace: " + message);
    }

    private static void Reject(Action action)
    {
        try { action(); }
        catch (InvalidDataException) { return; }
        throw new InvalidOperationException("Native workspace accepted an invalid action.");
    }

    public static void Run(string work)
    {
        string source = Path.Combine(work, "native-import-once.json");
        string output = Path.Combine(work, "persistent-native.sqlite");
        const string uuid = "00000000-0000-4000-8000-000000000121";
        File.WriteAllText(source, JsonSerializer.Serialize(new
        {
            schema = 2,
            cards = new object[] {
                new { name = "Arena Leader", qty = 4, sets = new [] { "AAA" } },
                new { name = "Zero Arena", qty = 0, sets = Array.Empty<string>() }
            },
            physicalCards = new object[] {
                new { name = "Split // Back", qty = 2, set = "ABC",
                    number = "123", scryfallId = uuid, finish = "foil",
                    location = "Binder A" },
                new { name = "Split // Back", qty = 3, set = "XYZ",
                    number = "005", scryfallId = "", finish = "nonfoil",
                    location = "Deck Box" },
                new { name = "Another Card", qty = 1, set = "", number = "",
                    scryfallId = "bad-uuid", finish = "etched" }
            },
            deckLibrary = new object[] {
                new { name = "Commander A", entries = new [] {
                    new { name = "Test Creature", qty = 1 } },
                    revisions = new [] { new { label = "v1" }, new { label = "v2" } } }
            },
            preferredArtwork = new { first = new { art = "test" }, second = "other" },
            extraSetting = new { wildcard = 2, selectedTheme = "dark" }
        }));

        byte[] sourceHash = SHA256.HashData(File.ReadAllBytes(source));
        DateTime sourceModified = File.GetLastWriteTimeUtc(source);
        var first = NativeWorkspace.ImportNew(source, output);
        Check(File.Exists(output) && first.DatabasePath == output &&
              first.Collection.PhysicalCopies == 6 && first.Collection.ArenaCopies == 4,
            "First native import failed to produce a complete persistent database.");
        Check(first.Collection.PhysicalCards.Count == 3 &&
              first.Collection.PhysicalCards[0].SetCode == "ABC" &&
              first.Collection.PhysicalCards[1].SetCode == "XYZ" &&
              first.Collection.DistinctPhysicalNames == 2,
            "Physical printings and duplicate named cards were merged.");
        Check(first.Collection.Decks == 1 && first.Collection.DeckRevisions == 2 &&
              first.Collection.PreferredArtworks == 2,
            "Full deck library, revisions, or preferred artwork not persisted.");

        var installedBefore = SHA256.HashData(File.ReadAllBytes(output));
        var outputModified = File.GetLastWriteTimeUtc(output);
        var reopened = NativeWorkspace.Open(output);
        Check(reopened.BackupSha256 == first.BackupSha256 &&
              reopened.ImportedAtUtc == first.ImportedAtUtc &&
              reopened.Collection.PhysicalCards.SequenceEqual(first.Collection.PhysicalCards) &&
              reopened.Collection.ArenaCards.SequenceEqual(first.Collection.ArenaCards),
            "A fresh native reopen does not match the imported source.");
        Check(installedBefore.SequenceEqual(SHA256.HashData(File.ReadAllBytes(output))) &&
              outputModified == File.GetLastWriteTimeUtc(output),
            "Native reopen unexpectedly wrote its SQLite database.");

        Reject(() => NativeWorkspace.ImportNew(source, output));
        Check(installedBefore.SequenceEqual(SHA256.HashData(File.ReadAllBytes(output))),
            "Repeated import overwrote or damaged the existing native workspace.");

        var invalid = Path.Combine(work, "native-reject.json");
        File.WriteAllText(invalid, "{\"schema\":2,\"cards\":[],\"physicalCards\":[{\"name\":\"bad\",\"qty\":-2}],\"deckLibrary\":[],\"preferredArtwork\":{}}");
        var invalidOutput = Path.Combine(work, "native-invalid.sqlite");
        Reject(() => NativeWorkspace.ImportNew(invalid, invalidOutput));
        Check(!File.Exists(invalidOutput) &&
              Directory.GetFiles(work, "native-invalid.sqlite.*.partial").Length == 0,
            "Invalid backup left a published or temporary workspace.");

        using (var corrupt = new SqliteConnection(new SqliteConnectionStringBuilder
               { DataSource = output, Mode = SqliteOpenMode.ReadWrite, Pooling = false }.ToString()))
        {
            corrupt.Open();
            using var cmd = corrupt.CreateCommand();
            cmd.CommandText = "UPDATE physical_lines SET qty=99 WHERE ordinal=0";
            cmd.ExecuteNonQuery();
        }
        Reject(() => NativeWorkspace.Open(output));
        Check(sourceHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(source))) &&
              sourceModified == File.GetLastWriteTimeUtc(source),
            "Original complete backup was modified during import or corruption testing.");

        string catalogSrc = Path.Combine(work, "native-catalog-source.sqlite");
        string nativeCatalog = Path.Combine(work, "native-catalog-persisted.sqlite");
        using (var src = new SqliteConnection(new SqliteConnectionStringBuilder
               { DataSource = catalogSrc, Mode = SqliteOpenMode.ReadWriteCreate,
                 Pooling = false }.ToString()))
        {
            src.Open();
            using var cmd = src.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE variants(
                    print_id TEXT PRIMARY KEY, name TEXT NOT NULL,
                    setcode TEXT NOT NULL, collector TEXT NOT NULL);
                INSERT INTO variants VALUES('00000000-0000-4000-8000-000000000121',
                    'Split // Back','ABC','123');
                CREATE TABLE cards(name TEXT NOT NULL);
                INSERT INTO cards VALUES('Split // Back');
                CREATE TABLE ownership(name TEXT PRIMARY KEY,qty INTEGER);
                INSERT INTO ownership VALUES('Not for transfer',999);
                """;
            cmd.ExecuteNonQuery();
        }
        var originalCatalogSha = SHA256.HashData(File.ReadAllBytes(catalogSrc));
        var originalCatalogModified = File.GetLastWriteTimeUtc(catalogSrc);
        using (var persistent = CatalogSnapshot.SavePersistentCopy(catalogSrc, nativeCatalog))
        {
            Check(persistent.TotalPrintings == 1 &&
                  persistent.SearchPrefix("Split // Back").Rows.Count == 1,
                "Native persistent catalog search failed.");
        }
        Check(File.Exists(nativeCatalog),
            "Closing a persistent catalog deleted the native source.");
        using (var reopenedCatalog = CatalogSnapshot.OpenPersistent(nativeCatalog))
            Check(reopenedCatalog.SearchPrefix("Split").Rows.Count == 1,
                "Native catalog did not reopen without legacy source.");
        Check(originalCatalogSha.SequenceEqual(SHA256.HashData(File.ReadAllBytes(catalogSrc))) &&
              originalCatalogModified == File.GetLastWriteTimeUtc(catalogSrc),
            "Native catalog import unexpectedly changed the old catalog.");
        Reject(() => CatalogSnapshot.SavePersistentCopy(catalogSrc, nativeCatalog));
        Check(File.Exists(nativeCatalog), "Duplicate native catalog import changed native data.");

        Console.WriteLine("PASS: persistent native SQLite import/reopen, 6 physical + 4 Arena copies, decks, art, no overwrite, tamper rejection, independent catalog reopen, legacy inputs unchanged");
    }
}
