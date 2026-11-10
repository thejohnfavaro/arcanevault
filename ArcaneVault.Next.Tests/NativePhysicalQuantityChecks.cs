using ArcaneVault.Next.Collection;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text.Json;

internal static class NativePhysicalQuantityChecks
{
    private static void Must(bool valid, string message)
    {
        if (!valid) throw new InvalidOperationException("Native physical quantity: " + message);
    }

    private static void Reject(Action action)
    {
        try { action(); }
        catch (InvalidDataException) { return; }
        catch (ArgumentException) { return; }
        throw new InvalidOperationException("Unsafe native physical quantity operation was accepted.");
    }

    public static void Run(string work)
    {
        const string printId = "00000000-0000-4000-8000-000000000651";
        string path = Path.Combine(work, "native-physical-quantity.sqlite");
        string exported = Path.Combine(work, "native-physical-original.json");
        byte[] originalBackup = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema = 2,
            cards = new[] { new { name = "Arena Card", qty = 4 } },
            physicalCards = new object[]
            {
                new { name = "One Card", qty = 5, set = "AAA", number = "010",
                      finish = "nonfoil", scryfallId = printId, location = "Binder A" },
                new { name = "One Card", qty = 3, set = "AAA", number = "010",
                      finish = "foil", scryfallId = printId, location = "Binder B" },
                new { name = "Unknown Card", qty = 7, set = "", number = "",
                      finish = "nonfoil", scryfallId = "", location = "Box C" },
                new { name = "Unknown Card", qty = 2, set = "", number = "",
                      finish = "nonfoil", scryfallId = "", location = "Box D" }
            },
            deckLibrary = new[] { new { name = "Commander",
                entries = new[] { new { name = "One Card", qty = 1 } },
                revisions = new[] { new { note = "v1" } } } },
            preferredArtwork = new { card = new { art = "chosen.png" } },
            favorites = new[] { "One Card" },
            extraFutureSetting = new { unknownKey = "preserve" }
        });
        File.WriteAllBytes(exported, originalBackup);
        var sourceHash = SHA256.HashData(File.ReadAllBytes(exported));
        var original = NativeWorkspace.ImportNewBytes(originalBackup, "fixture", path);
        Must(original.Collection.PhysicalCopies == 17 &&
             original.Collection.PhysicalCards.Count == 4 &&
             original.Collection.CopiesWithMissingOrInvalidIds == 9,
             "Fixture did not keep distinct paper finishes and unresolved rows.");

        var first = NativeWorkspaceSafety.SetPhysicalQuantity(
            path, 1, original.Collection.PhysicalCards[1], 1, original.BackupSha256);
        Must(first.Current.Collection.PhysicalCopies == 15 &&
             first.Current.Collection.PhysicalCards[0].Quantity == 5 &&
             first.Current.Collection.PhysicalCards[1].Quantity == 1 &&
             first.Current.Collection.PhysicalCards[1].Finish == "foil" &&
             first.Current.Collection.PhysicalCards[2].Quantity == 7 &&
             first.Current.Collection.PhysicalCards[3].Quantity == 2,
             "Changing the foil line also changed a different finish or unresolved row.");
        Must(NativeWorkspace.Open(first.RecoveryPath).BackupSha256 == original.BackupSha256,
             "The complete original collection was not kept for rollback.");

        var unchanged = SHA256.HashData(File.ReadAllBytes(path));
        var rollbacks = NativeWorkspaceSafety.RecoveryPaths(path).Count;
        Reject(() => NativeWorkspaceSafety.SetPhysicalQuantity(
            path, 1, first.Current.Collection.PhysicalCards[1], 0, original.BackupSha256));
        Reject(() => NativeWorkspaceSafety.SetPhysicalQuantity(
            path, 2, first.Current.Collection.PhysicalCards[1], 0, first.Current.BackupSha256));
        Reject(() => NativeWorkspaceSafety.SetPhysicalQuantity(
            path, 1, first.Current.Collection.PhysicalCards[1], 1, first.Current.BackupSha256));
        Reject(() => NativeWorkspaceSafety.SetPhysicalQuantity(
            path, 1, first.Current.Collection.PhysicalCards[1], 2, first.Current.BackupSha256));
        Reject(() => NativeWorkspaceSafety.SetPhysicalQuantity(
            path, 1, first.Current.Collection.PhysicalCards[1], -1, first.Current.BackupSha256));
        Reject(() => NativeWorkspaceSafety.SetPhysicalQuantity(
            path, 99, first.Current.Collection.PhysicalCards[1], 0, first.Current.BackupSha256));
        Must(unchanged.SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))) &&
             NativeWorkspaceSafety.RecoveryPaths(path).Count == rollbacks,
             "Rejected stale/mismatched/no-op/increase/invalid requests changed data or made a recovery.");

        // Distinct, unresolved name-only rows can be removed individually,
        // even without a catalog. Never guess a print ID or aggregate by name.
        var second = NativeWorkspaceSafety.SetPhysicalQuantity(
            path, 2, first.Current.Collection.PhysicalCards[2], 0,
            first.Current.BackupSha256);
        Must(second.Current.Collection.PhysicalCopies == 8 &&
             second.Current.Collection.PhysicalCards.Count == 3 &&
             second.Current.Collection.PhysicalCards[2].Quantity == 2 &&
             second.Current.Collection.PhysicalCards[2].ScryfallId == "" &&
             second.Current.Collection.CopiesWithMissingOrInvalidIds == 2 &&
             second.Current.Collection.ArenaCopies == 4 &&
             second.Current.Collection.Decks == 1 &&
             second.Current.Collection.DeckRevisions == 1 &&
             second.Current.Collection.PreferredArtworks == 1,
             "Removing one unresolved row affected another or the deck/Arena/art data.");
        var third = NativeWorkspaceSafety.SetPhysicalQuantity(
            path, 2, second.Current.Collection.PhysicalCards[2], 0,
            second.Current.BackupSha256);
        Must(third.Current.Collection.PhysicalCopies == 6 &&
             third.Current.Collection.PhysicalCards.Count == 2 &&
             third.Current.Collection.CopiesWithMissingOrInvalidIds == 0,
             "Last unresolved copy was not cleanly removed without a tombstone.");

        using (var db = new SqliteConnection(new SqliteConnectionStringBuilder
               { DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString()))
        {
            db.Open();
            using var query = db.CreateCommand();
            query.CommandText = "SELECT backup FROM manifest WHERE id=1";
            using var json = JsonDocument.Parse((byte[])query.ExecuteScalar()!);
            var root = json.RootElement;
            Must(root.GetProperty("extraFutureSetting").GetProperty("unknownKey").GetString() == "preserve" &&
                 root.GetProperty("physicalCards")[0].GetProperty("location").GetString() == "Binder A" &&
                 root.GetProperty("physicalCards")[1].GetProperty("location").GetString() == "Binder B" &&
                 root.GetProperty("physicalCards")[1].GetProperty("qty").GetInt32() == 1 &&
                 root.GetProperty("favorites")[0].GetString() == "One Card",
                 "Edit discarded card-location, favorite, or unknown future fields.");
        }

        Must(NativeWorkspaceSafety.RecoveryPaths(path).Count == 3,
             "Each successful quantity edit should keep one complete rollback.");
        var restored = NativeWorkspaceSafety.Restore(path, first.RecoveryPath,
            third.Current.BackupSha256);
        Must(restored.Current.BackupSha256 == original.BackupSha256 &&
             restored.Current.Collection.PhysicalCopies == 17 &&
             restored.Current.Collection.PhysicalCards.Count == 4 &&
             restored.Current.Collection.CopiesWithMissingOrInvalidIds == 9 &&
             NativeWorkspace.Open(restored.RecoveryPath).Collection.PhysicalCopies == 6,
             "Rollback must restore original exact rows and preserve newer collection.");
        Must(sourceHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(exported))),
             "Native quantity edits modified the separately exported original backup.");
        Console.WriteLine("PASS: exact physical line reduction, zero removes one line, no name-based guesses, typed rows & unknown fields retained, stale guards and full rollback");
    }
}
