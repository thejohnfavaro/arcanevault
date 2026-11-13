using ArcaneVault.Next.Collection;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text.Json;

internal static class NativeFreshStartChecks
{
    private static void Must(bool value, string description)
    {
        if (!value)
            throw new InvalidOperationException("Native fresh collection: " + description);
    }

    private static void Reject(Action action)
    {
        try { action(); }
        catch (InvalidDataException) { return; }
        throw new InvalidOperationException("Unsafe fresh-start operation was accepted.");
    }

    private static byte[] PopulatedSource()
    {
        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema = 2,
            exported = "2026-11-10T00:00:00Z",
            cards = new object[]
            {
                new { name = "Arena Favorite", qty = 4, tags = new[] { "owned" } }
            },
            physicalCards = new object[]
            {
                new { name = "Split // Back", qty = 3, set = "AAA", number = "010",
                    finish = "foil", scryfallId = "00000000-0000-4000-8000-000000000100",
                    location = "Binder 1" },
                new { name = "Split // Back", qty = 2, set = "BBB", number = "011",
                    finish = "nonfoil", scryfallId = "", location = "Deck Box" }
            },
            deckLibrary = new object[]
            {
                new { name = "EDH Deck", entries = new [] { new { name = "Test", qty = 1 } },
                    revisions = new [] { new { name = "Old" }, new { name = "New" } } }
            },
            preferredArtwork = new { card = new { image = "art.jpg" } },
            favorites = new [] { "Split // Back" },
            studio = new { name = "Test" },
            wildcards = new { common = 6, rare = 2 },
            legacyStorage = new { futureKey = "preserve-in-rollback" }
        });
    }

    public static void Run(string work)
    {
        var firstPath = Path.Combine(work, "new-blank-from-nothing.sqlite");
        var sentinelCatalog = Path.Combine(work, "native-catalog-never-reset.sqlite");
        File.WriteAllBytes(sentinelCatalog, "independent-catalog-sentinel"u8.ToArray());
        var catalogSha = SHA256.HashData(File.ReadAllBytes(sentinelCatalog));

        // Empty native collection: no old application, Scryfall catalog, export,
        // seed cards, or previously initialized native database required.
        var newBlank = NativeWorkspaceSafety.StartFresh(firstPath);
        Must(newBlank.Created && !newBlank.AlreadyEmpty &&
            newBlank.RecoveryPath is null && File.Exists(firstPath),
            "First launch did not make an independent blank native workspace.");
        var opened = NativeWorkspace.Open(firstPath);
        Must(opened.Collection.PhysicalCards.Count == 0 &&
            opened.Collection.ArenaCards.Count == 0 &&
            opened.Collection.PhysicalCopies == 0 && opened.Collection.ArenaCopies == 0 &&
            opened.Collection.Decks == 0 && opened.Collection.DeckRevisions == 0 &&
            opened.Collection.PreferredArtworks == 0,
            "First-launch collection must have exactly zero physical, Arena, deck, and art records.");
        Must(NativeWorkspaceSafety.RecoveryPaths(firstPath).Count == 0,
            "Starting fresh on a new device must not invent a previous recovery.");
        Reject(() => NativeWorkspaceSafety.StartFresh(
            Path.Combine(work, "no-existing-for-stale-check.sqlite"),
            expectedCurrentBackupSha: "FAKE-SHA"));
        Must(!File.Exists(Path.Combine(work, "no-existing-for-stale-check.sqlite")),
            "Stale expectation created a database.");

        byte[] beforeNoOp = SHA256.HashData(File.ReadAllBytes(firstPath));
        DateTime beforeMtime = File.GetLastWriteTimeUtc(firstPath);
        var blankAgain = NativeWorkspaceSafety.StartFresh(firstPath, opened.BackupSha256);
        Must(!blankAgain.Created && blankAgain.AlreadyEmpty &&
            blankAgain.RecoveryPath is null &&
            NativeWorkspaceSafety.RecoveryPaths(firstPath).Count == 0 &&
            beforeNoOp.SequenceEqual(SHA256.HashData(File.ReadAllBytes(firstPath))) &&
            beforeMtime == File.GetLastWriteTimeUtc(firstPath),
            "Repeating fresh start on a blank collection must be a no-op.");

        string populatedPath = Path.Combine(work, "populated-to-reset.sqlite");
        byte[] originalPayload = PopulatedSource();
        var original = NativeWorkspace.ImportNewBytes(
            originalPayload, "populated-source", populatedPath);
        Must(original.Collection.PhysicalCopies == 5 &&
             original.Collection.ArenaCopies == 4 &&
             original.Collection.Decks == 1 &&
             original.Collection.DeckRevisions == 2 &&
             original.Collection.PreferredArtworks == 1,
             "Synthetic source did not contain all the data to protect.");

        var originalHash = SHA256.HashData(File.ReadAllBytes(populatedPath));
        var originalMtime = File.GetLastWriteTimeUtc(populatedPath);
        Reject(() => NativeWorkspaceSafety.StartFresh(
            populatedPath, expectedCurrentBackupSha: "STALE-SHA"));
        Must(originalHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(populatedPath))) &&
             originalMtime == File.GetLastWriteTimeUtc(populatedPath) &&
             NativeWorkspaceSafety.RecoveryPaths(populatedPath).Count == 0,
             "Rejected reset modified the source or created a rollback.");

        var fresh = NativeWorkspaceSafety.StartFresh(
            populatedPath, expectedCurrentBackupSha: original.BackupSha256);
        Must(!fresh.Created && !fresh.AlreadyEmpty &&
            fresh.RecoveryPath is not null && File.Exists(fresh.RecoveryPath),
            "Reset of a populated collection must retain a uniquely named recovery.");
        var reset = NativeWorkspace.Open(populatedPath);
        var prior = NativeWorkspace.Open(fresh.RecoveryPath!);
        Must(reset.Collection.PhysicalCopies == 0 &&
             reset.Collection.ArenaCopies == 0 &&
             reset.Collection.PhysicalCards.Count == 0 &&
             reset.Collection.ArenaCards.Count == 0 &&
             reset.Collection.Decks == 0 &&
             reset.Collection.DeckRevisions == 0 &&
             reset.Collection.PreferredArtworks == 0,
             "Reset did not remove all active native inventory, decks and artwork.");
        Must(prior.BackupSha256 == original.BackupSha256 &&
             prior.Collection.PhysicalCards.SequenceEqual(original.Collection.PhysicalCards) &&
             prior.Collection.ArenaCards.SequenceEqual(original.Collection.ArenaCards) &&
             prior.Collection.DeckRevisions == 2 &&
             prior.Collection.PreferredArtworks == 1,
             "The recovery copy did not retain all previous collection data.");
        using (var sqlite = new SqliteConnection(new SqliteConnectionStringBuilder
               { DataSource = populatedPath, Mode = SqliteOpenMode.ReadOnly,
                 Pooling = false }.ToString()))
        {
            sqlite.Open();
            using var query = sqlite.CreateCommand();
            query.CommandText = "SELECT backup FROM manifest WHERE id=1";
            var blob = (byte[])query.ExecuteScalar()!;
            using var parsed = JsonDocument.Parse(blob);
            var root = parsed.RootElement;
            Must(root.GetProperty("wildcards").GetProperty("rare").GetInt32() == 0 &&
                 root.GetProperty("favorites").GetArrayLength() == 0 &&
                 root.GetProperty("collectionHistory").GetArrayLength() == 0 &&
                 !root.TryGetProperty("legacyStorage", out _),
                 "Fresh workspace still contains old preferences or legacy profile state.");
        }
        Must(catalogSha.SequenceEqual(SHA256.HashData(File.ReadAllBytes(sentinelCatalog))),
            "Reset modified the independently preserved Scryfall catalog.");

        Reject(() => NativeWorkspaceSafety.StartFresh(
            populatedPath, expectedCurrentBackupSha: original.BackupSha256));
        Must(NativeWorkspaceSafety.RecoveryPaths(populatedPath).Count == 1,
            "Stale duplicate reset created an extra rollback.");
        var noOp = NativeWorkspaceSafety.StartFresh(populatedPath, reset.BackupSha256);
        Must(noOp.AlreadyEmpty && noOp.RecoveryPath is null &&
             NativeWorkspaceSafety.RecoveryPaths(populatedPath).Count == 1,
             "Reset of an already-empty workspace must not create a second rollback.");

        var recovered = NativeWorkspaceSafety.Restore(
            populatedPath, fresh.RecoveryPath!, reset.BackupSha256);
        Must(recovered.Current.BackupSha256 == original.BackupSha256 &&
             recovered.Current.Collection.PhysicalCopies == 5 &&
             recovered.Current.Collection.ArenaCopies == 4 &&
             recovered.Current.Collection.Decks == 1 &&
             recovered.Current.Collection.DeckRevisions == 2 &&
             recovered.Current.Collection.PreferredArtworks == 1 &&
             NativeWorkspaceSafety.RecoveryPaths(populatedPath).Count == 2,
             "Restoring a previous native collection must bring all the data back and preserve blank state.");
        Must(File.Exists(fresh.RecoveryPath) &&
             File.Exists(recovered.RecoveryPath) &&
             NativeWorkspace.Open(recovered.RecoveryPath).Collection.PhysicalCopies == 0,
             "Restoring prior data deleted the blank version or its original recovery.");

        // v0.9.55: selected collection clearing is NOT full Start Fresh.
        // Keep all decks/revisions, the other inventory, artwork, favorites,
        // wildcards, and unknown original fields, including in a real rollback.
        string selectivePath = Path.Combine(work, "selective-inventory-reset.sqlite");
        var selectiveOriginal = NativeWorkspace.ImportNewBytes(
            PopulatedSource(), "selective-source", selectivePath);
        var physicalOnly = NativeWorkspaceSafety.ClearInventory(selectivePath,
            NativeWorkspaceSafety.InventoryScope.Physical,
            selectiveOriginal.BackupSha256);
        Must(physicalOnly.RecoveryPath is not null &&
             File.Exists(physicalOnly.RecoveryPath) &&
             !physicalOnly.AlreadyEmpty &&
             physicalOnly.Current.Collection.PhysicalCopies == 0 &&
             physicalOnly.Current.Collection.ArenaCopies == 4 &&
             physicalOnly.Current.Collection.Decks == 1 &&
             physicalOnly.Current.Collection.DeckRevisions == 2 &&
             physicalOnly.Current.Collection.PreferredArtworks == 1 &&
             NativeWorkspace.Open(physicalOnly.RecoveryPath).BackupSha256 ==
                selectiveOriginal.BackupSha256,
             "Physical-only reset must retain Arena, decks/art and complete source recovery.");
        using (var sqlite = new SqliteConnection(new SqliteConnectionStringBuilder
               { DataSource = selectivePath, Mode = SqliteOpenMode.ReadOnly,
                 Pooling = false }.ToString()))
        {
            sqlite.Open();
            using var cmd = sqlite.CreateCommand();
            cmd.CommandText = "SELECT backup FROM manifest WHERE id=1";
            using var doc = JsonDocument.Parse((byte[])cmd.ExecuteScalar()!);
            var root = doc.RootElement;
            Must(root.GetProperty("physicalCards").GetArrayLength() == 0 &&
                 root.GetProperty("cards").GetArrayLength() == 1 &&
                 root.GetProperty("deckLibrary")[0].GetProperty("revisions").GetArrayLength() == 2 &&
                 root.GetProperty("favorites").GetArrayLength() == 1 &&
                 root.GetProperty("wildcards").GetProperty("rare").GetInt32() == 2 &&
                 root.GetProperty("legacyStorage").GetProperty("futureKey")
                    .GetString() == "preserve-in-rollback",
                 "Selective reset must preserve unknown schema fields and deck/shortlist preferences.");
        }

        var postPhysical = NativeWorkspace.Open(selectivePath);
        var afterPhysicalHash = SHA256.HashData(File.ReadAllBytes(selectivePath));
        var afterPhysicalTime = File.GetLastWriteTimeUtc(selectivePath);
        var noOpPhysical = NativeWorkspaceSafety.ClearInventory(selectivePath,
            NativeWorkspaceSafety.InventoryScope.Physical, postPhysical.BackupSha256);
        Must(noOpPhysical.AlreadyEmpty && noOpPhysical.RecoveryPath is null &&
             afterPhysicalHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(selectivePath))) &&
             afterPhysicalTime == File.GetLastWriteTimeUtc(selectivePath) &&
             NativeWorkspaceSafety.RecoveryPaths(selectivePath).Count == 1,
             "Clearing an empty physical scope must not write or make a duplicate rollback.");

        Reject(() => NativeWorkspaceSafety.ClearInventory(selectivePath,
            NativeWorkspaceSafety.InventoryScope.Arena,
            selectiveOriginal.BackupSha256));
        Must(afterPhysicalHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(selectivePath))),
             "Stale scope reset must leave current collection unchanged.");

        var arenaOnly = NativeWorkspaceSafety.ClearInventory(selectivePath,
            NativeWorkspaceSafety.InventoryScope.Arena, postPhysical.BackupSha256);
        Must(arenaOnly.Current.Collection.ArenaCopies == 0 &&
             arenaOnly.Current.Collection.PhysicalCopies == 0 &&
             arenaOnly.Current.Collection.Decks == 1 &&
             arenaOnly.Current.Collection.DeckRevisions == 2 &&
             arenaOnly.Current.Collection.PreferredArtworks == 1 &&
             arenaOnly.RecoveryPath is not null &&
             NativeWorkspace.Open(arenaOnly.RecoveryPath).Collection.ArenaCopies == 4,
             "Arena-only reset must retain decks/art and a separate complete recovery.");

        string bothPath = Path.Combine(work, "both-scopes-to-reset.sqlite");
        var bothOriginal = NativeWorkspace.ImportNewBytes(
            PopulatedSource(), "both-scopes-source", bothPath);
        var clearedBoth = NativeWorkspaceSafety.ClearInventory(bothPath,
            NativeWorkspaceSafety.InventoryScope.Both,
            bothOriginal.BackupSha256);
        Must(clearedBoth.Current.Collection.PhysicalCards.Count == 0 &&
             clearedBoth.Current.Collection.ArenaCards.Count == 0 &&
             clearedBoth.Current.Collection.PhysicalCopies == 0 &&
             clearedBoth.Current.Collection.ArenaCopies == 0 &&
             clearedBoth.Current.Collection.Decks == 1 &&
             clearedBoth.Current.Collection.DeckRevisions == 2 &&
             clearedBoth.Current.Collection.PreferredArtworks == 1 &&
             clearedBoth.RecoveryPath is not null &&
             NativeWorkspace.Open(clearedBoth.RecoveryPath).BackupSha256 ==
                 bothOriginal.BackupSha256,
             "Clear both inventories must preserve deck library and full recoverability.");
        var restoredBoth = NativeWorkspaceSafety.Restore(bothPath,
            clearedBoth.RecoveryPath, clearedBoth.Current.BackupSha256);
        Must(restoredBoth.Current.Collection.PhysicalCopies == 5 &&
             restoredBoth.Current.Collection.ArenaCopies == 4 &&
             restoredBoth.Current.Collection.Decks == 1,
             "Full recovery after selective reset must restore both old ownership scopes.");
        Must(catalogSha.SequenceEqual(SHA256.HashData(File.ReadAllBytes(sentinelCatalog))),
             "Selective inventory clear changed the Scryfall catalog.");
        Console.WriteLine("PASS: create empty native collection without export, preserve catalog, typed reset backend clears all data, idempotent no-op, stale guard, verified rollback, full restore, selective scope resets with preserved decks/preferences");
    }
}
