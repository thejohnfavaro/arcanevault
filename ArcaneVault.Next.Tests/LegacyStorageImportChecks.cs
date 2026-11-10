using ArcaneVault.Next.Collection;
using ArcaneVault.Next.Domain;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text.Json;

internal static class LegacyStorageImportChecks
{
    private static void Must(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException("Direct legacy migration: " + description);
    }

    private static void Reject(Action test)
    {
        try { test(); }
        catch (InvalidDataException) { return; }
        throw new InvalidOperationException("Direct legacy migration accepted invalid profile data.");
    }

    public static void Run(string directory)
    {
        const string printId = "00000000-0000-4000-8000-000000000111";
        var storage = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["vault_cards"] = JsonSerializer.Serialize(new object[]
            {
                new { name = "Arena Alpha", qty = 3, sets = new[] { "AAA" } },
                new { name = "Zero Wildcard Test", qty = 0, sets = Array.Empty<string>() }
            }),
            ["vault_physicalCollectionV1"] = JsonSerializer.Serialize(new object[]
            {
                new { name = "Split // Back", qty = 2, set = "AAA", number = "17",
                    finish = "foil", scryfallId = printId, location = "Binder" },
                new { name = "Split // Back", qty = 4, set = "BBB", number = "5",
                    finish = "nonfoil", scryfallId = "", location = "Deck Box" }
            }),
            ["vault_libraryV3"] = JsonSerializer.Serialize(new object[] {
                new { name = "Legacy Deck", entries = new object[] {
                    new { name = "Split // Back", qty = 1 }},
                    revisions = new[] { new { label = "older revision" } } }
            }),
            ["vault_preferredPrintingArtworkV1"] = "{\"split // back\":{\"image\":\"https://cards.scryfall.io/test.jpg\"}}",
            ["vault_favorites"] = "[\"Split // Back\"]",
            ["vault_deck"] = "null",
            ["vault_studio"] = "{\"name\":\"Legacy draft\"}",
            ["vault_wildcardsV3"] = "{\"common\":25,\"rare\":2}",
            ["arcaneVault.collectionHistory.v1"] = "[{\"action\":\"import\"}]",
            ["arcaneVault.theme"] = "noir",
            ["vault_preferenceV99"] = "{\"unknownFuture\":7}"
        };
        var bytes = LegacyStorageImport.BuildFullBackup(storage);
        var snapshot = NativeBackupReader.Read(bytes);
        Must(snapshot.ArenaCopies == 3 && snapshot.PhysicalCopies == 6 &&
             snapshot.PhysicalCards.Count == 2 && snapshot.Decks == 1 &&
             snapshot.DeckRevisions == 1 && snapshot.PreferredArtworks == 1 &&
             snapshot.CopiesWithMissingOrInvalidIds == 4,
             "Lost collection counts/physical variants while converting browser storage.");

        using (var json = JsonDocument.Parse(bytes))
        {
            var root = json.RootElement;
            Must(root.GetProperty("schema").GetInt32() == 2 &&
                 root.GetProperty("source").GetString() == "installed-webview2-offline-snapshot" &&
                 root.GetProperty("legacyStorage").GetProperty("vault_preferenceV99")
                     .GetString() == "{\"unknownFuture\":7}" &&
                 root.GetProperty("legacyStorage").GetProperty("arcaneVault.theme")
                     .GetString() == "noir" &&
                 root.GetProperty("collectionHistory").GetArrayLength() == 1 &&
                 root.GetProperty("wildcards").GetProperty("rare").GetInt32() == 2 &&
                 root.GetProperty("deck").ValueKind == JsonValueKind.Null,
                 "Ancillary browser state or unknown future fields were lost.");
        }

        var native = Path.Combine(directory, "legacy-direct-native.sqlite");
        var imported = NativeWorkspace.ImportNewBytes(
            bytes, "installed-WebView2-origin", native);
        var reopened = NativeWorkspace.Open(native);
        Must(imported.BackupSha256 == reopened.BackupSha256 &&
             imported.Collection.PhysicalCopies == reopened.Collection.PhysicalCopies &&
             reopened.Collection.DeckRevisions == 1 &&
             reopened.Collection.PhysicalCards[0].ScryfallId == printId,
             "Native workspace could not reopen directly migrated records.");
        Reject(() => NativeWorkspace.ImportNewBytes(bytes, "installed-WebView2-origin", native));
        Must(File.Exists(native), "Repeated migration should not delete native workspace.");

        var missingPhysical = new Dictionary<string, string?>(storage);
        missingPhysical.Remove("vault_physicalCollectionV1");
        Reject(() => LegacyStorageImport.BuildFullBackup(missingPhysical));
        var missingArena = new Dictionary<string, string?>(storage);
        missingArena.Remove("vault_cards");
        Reject(() => LegacyStorageImport.BuildFullBackup(missingArena));
        var malformedPhysical = new Dictionary<string, string?>(storage)
        { ["vault_physicalCollectionV1"] = "{not-json" };
        Reject(() => LegacyStorageImport.BuildFullBackup(malformedPhysical));

        // Scratch profile copy of localStorage must be distinct and immutable
        // relative to the original installed profile. Never traverse symlinks
        // or reuse a populated migration scratch.
        string legacy = Path.Combine(directory, "fake-old-webview");
        string leveldb = Path.Combine(legacy, "Default", "Local Storage", "leveldb");
        Directory.CreateDirectory(leveldb);
        File.WriteAllText(Path.Combine(leveldb, "CURRENT"), "MANIFEST-000001\n");
        File.WriteAllText(Path.Combine(leveldb, "MANIFEST-000001"), "manifest data");
        File.WriteAllText(Path.Combine(leveldb, "000001.ldb"), "example local storage bytes");
        File.WriteAllText(Path.Combine(leveldb, "LOCK"), "must not be copied");
        var before = Directory.GetFiles(leveldb).ToDictionary(
            p => Path.GetFileName(p), p => SHA256.HashData(File.ReadAllBytes(p)));
        var scratch = Path.Combine(directory, "isolated-browser-clone");
        LegacyProfileSnapshot.CopyToTemporaryProfile(legacy, scratch);
        string isolated = Path.Combine(scratch, "Default", "Local Storage", "leveldb");
        Must(!File.Exists(Path.Combine(isolated, "LOCK")) &&
             File.Exists(Path.Combine(isolated, "CURRENT")) &&
             File.Exists(Path.Combine(isolated, "000001.ldb")),
             "Cloned profile is not independently usable or contains live LOCK.");
        foreach (var item in before)
            Must(item.Value.SequenceEqual(SHA256.HashData(
                    File.ReadAllBytes(Path.Combine(leveldb, item.Key)))),
                 "Snapshot copied data by modifying the old profile.");
        Reject(() => LegacyProfileSnapshot.CopyToTemporaryProfile(legacy, scratch));
        Reject(() => LegacyProfileSnapshot.CopyToTemporaryProfile(
            Path.Combine(directory, "unknown-profile"), Path.Combine(directory, "nowhere")));

        Console.WriteLine("PASS: zero-export legacy profile conversion, preserved collections/decks/art/unknown settings, direct atomic SQLite, no overwrite, fail-closed keys, isolated LevelDB snapshot source unchanged");
    }
}
