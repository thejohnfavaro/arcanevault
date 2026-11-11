using ArcaneVault.Next.Collection;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text.Json;

internal static class NativeArenaEditingChecks
{
    private static void Must(bool yes, string why)
    {
        if (!yes) throw new InvalidOperationException("Native Arena editing: " + why);
    }

    private static void Reject(Action action)
    {
        try { action(); }
        catch (InvalidDataException) { return; }
        catch (ArgumentException) { return; }
        throw new InvalidOperationException("An invalid Arena edit was accepted.");
    }

    private static byte[] Source() => JsonSerializer.SerializeToUtf8Bytes(new
    {
        schema = 2,
        cards = new object[]
        {
            new { name = "Arena Card", qty = 2, note = "Keep favorite annotation" },
            new { name = "Another Spell", qty = 3, note = "Never touch" }
        },
        physicalCards = new object[]
        {
            new { name = "Split // Back", qty = 5, set = "TST",
                number = "123", finish = "foil",
                scryfallId = "00000000-0000-4000-8000-000000000711", location = "Binder" },
            new { name = "Unknown Paper", qty = 2, set = "", number = "",
                finish = "nonfoil", scryfallId = "", location = "Box" }
        },
        deckLibrary = new object[] {
            new { name = "EDH", entries = new[] { new { name = "Arena Card", qty = 1 }},
                revisions = new[] { new { id = "v1" } } }
        },
        preferredArtwork = new { selected = new { image = "choice.png" } },
        favorites = new[] { "Arena Card" },
        extraFutureField = new { future = "preserve" }
    });

    public static void Run(string work)
    {
        string path = Path.Combine(work, "native-arena-edit.sqlite");
        string originalJsonPath = Path.Combine(work, "native-arena-original.json");
        File.WriteAllBytes(originalJsonPath, Source());
        byte[] sourceHash = SHA256.HashData(File.ReadAllBytes(originalJsonPath));
        var first = NativeWorkspace.ImportNewBytes(Source(), "arena-fixture", path);
        Must(first.Collection.PhysicalCopies == 7 && first.Collection.ArenaCopies == 5 &&
             first.Collection.Decks == 1 && first.Collection.DeckRevisions == 1 &&
             first.Collection.PreferredArtworks == 1,
             "Baseline physical/Arena/deck/art totals were lost.");

        var second = NativeWorkspaceSafety.AddArenaCopies(path, "aReNa CaRd", 2,
            first.BackupSha256);
        Must(second.Current.Collection.ArenaCopies == 7 &&
             second.Current.Collection.ArenaCards.Count == 2 &&
             second.Current.Collection.ArenaCards[0].Quantity == 4 &&
             second.Current.Collection.ArenaCards[0].Name == "Arena Card",
             "Case-insensitive add did not increment the existing exact Arena name.");
        Must(NativeWorkspace.Open(second.RecoveryPath).BackupSha256 == first.BackupSha256,
            "Native Arena addition failed to retain original state.");

        var third = NativeWorkspaceSafety.AddArenaCopies(path, "New Split // Card", 1,
            second.Current.BackupSha256);
        Must(third.Current.Collection.ArenaCards.Count == 3 &&
             third.Current.Collection.ArenaCopies == 8 &&
             third.Current.Collection.ArenaCards[2].Name == "New Split // Card" &&
             third.Current.Collection.PhysicalCopies == 7,
             "Name-only Arena addition cannot affect physical printings.");

        var fourth = NativeWorkspaceSafety.EditArenaRow(path, 2,
            third.Current.Collection.ArenaCards[2], "Renamed Card", 4,
            third.Current.BackupSha256);
        Must(fourth.Current.Collection.ArenaCards[2].Name == "Renamed Card" &&
             fourth.Current.Collection.ArenaCards[2].Quantity == 4 &&
             fourth.Current.Collection.ArenaCopies == 11 &&
             fourth.Current.Collection.ArenaCards[0].Quantity == 4 &&
             fourth.Current.Collection.ArenaCards[1].Quantity == 3,
             "Selected Arena edit changed other rows or failed to rename/count copies.");

        byte[] currentHash = SHA256.HashData(File.ReadAllBytes(path));
        int rollbackCount = NativeWorkspaceSafety.RecoveryPaths(path).Count;
        Reject(() => NativeWorkspaceSafety.AddArenaCopies(path, "A", 1,
            "stale-sha"));
        Reject(() => NativeWorkspaceSafety.AddArenaCopies(path, "    ", 1,
            fourth.Current.BackupSha256));
        Reject(() => NativeWorkspaceSafety.AddArenaCopies(path, "Arena Card", 0,
            fourth.Current.BackupSha256));
        Reject(() => NativeWorkspaceSafety.AddArenaCopies(path, "Arena Card", 99_999,
            fourth.Current.BackupSha256));
        Reject(() => NativeWorkspaceSafety.EditArenaRow(path, 2,
            third.Current.Collection.ArenaCards[2], "Another Name", 3,
            fourth.Current.BackupSha256));
        Reject(() => NativeWorkspaceSafety.EditArenaRow(path, 2,
            fourth.Current.Collection.ArenaCards[2], "Arena Card", 3,
            fourth.Current.BackupSha256));
        Reject(() => NativeWorkspaceSafety.EditArenaRow(path, 2,
            fourth.Current.Collection.ArenaCards[2], "Renamed Card", 4,
            fourth.Current.BackupSha256));
        Reject(() => NativeWorkspaceSafety.EditArenaRow(path, 99,
            fourth.Current.Collection.ArenaCards[2], "Oops", 2,
            fourth.Current.BackupSha256));
        Must(currentHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))) &&
             NativeWorkspaceSafety.RecoveryPaths(path).Count == rollbackCount,
             "Rejected Arena changes wrote to live SQLite or created rollback copies.");

        var removed = NativeWorkspaceSafety.EditArenaRow(path, 2,
            fourth.Current.Collection.ArenaCards[2], "Renamed Card", 0,
            fourth.Current.BackupSha256);
        Must(removed.Current.Collection.ArenaCopies == 7 &&
             removed.Current.Collection.ArenaCards.Count == 2 &&
             removed.Current.Collection.ArenaCards[0].Quantity == 4 &&
             removed.Current.Collection.ArenaCards[1].Quantity == 3 &&
             removed.Current.Collection.PhysicalCards.SequenceEqual(first.Collection.PhysicalCards) &&
             removed.Current.Collection.Decks == 1 &&
             removed.Current.Collection.DeckRevisions == 1 &&
             removed.Current.Collection.PreferredArtworks == 1,
             "Removing an Arena row altered unrelated physical/deck/art data.");

        using (var db = new SqliteConnection(new SqliteConnectionStringBuilder
            { DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString()))
        {
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT backup FROM manifest WHERE id=1";
            using var parsed = JsonDocument.Parse((byte[])cmd.ExecuteScalar()!);
            var root = parsed.RootElement;
            Must(root.GetProperty("cards")[0].GetProperty("note").GetString() ==
                     "Keep favorite annotation" &&
                 root.GetProperty("cards")[1].GetProperty("note").GetString() ==
                     "Never touch" &&
                 root.GetProperty("physicalCards")[0].GetProperty("location").GetString() ==
                     "Binder" &&
                 root.GetProperty("favorites")[0].GetString() == "Arena Card" &&
                 root.GetProperty("extraFutureField").GetProperty("future").GetString() ==
                     "preserve",
                 "Edit removed legacy annotations, physical locations or opaque fields.");
        }

        Must(NativeWorkspaceSafety.RecoveryPaths(path).Count == 4,
            "Each successful Arena add/edit/remove must retain one verified rollback.");
        var restored = NativeWorkspaceSafety.Restore(path,
            second.RecoveryPath, removed.Current.BackupSha256);
        Must(restored.Current.BackupSha256 == first.BackupSha256 &&
             restored.Current.Collection.ArenaCopies == 5 &&
             restored.Current.Collection.ArenaCards.Count == 2 &&
             NativeWorkspace.Open(restored.RecoveryPath).Collection.ArenaCopies == 7 &&
             sourceHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(originalJsonPath))),
             "Recovery did not restore original Arena inventory or left the export modified.");

        // Legacy imports can have two name-only rows with matching names.
        // Do not silently merge them; explicit row editing must still work.
        var duplicates = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema = 2,
            cards = new object[] {
                new { name = "Repeated", qty = 1, collection = "first" },
                new { name = "Repeated", qty = 2, collection = "second" }
            },
            physicalCards = Array.Empty<object>(),
            deckLibrary = Array.Empty<object>(),
            preferredArtwork = new { }
        });
        var dup = NativeWorkspace.ImportNewBytes(duplicates, "duplicate-fixture",
            Path.Combine(work, "arena-duplicate.sqlite"));
        Reject(() => NativeWorkspaceSafety.AddArenaCopies(dup.DatabasePath,
            "Repeated", 1, dup.BackupSha256));
        var selected = NativeWorkspaceSafety.EditArenaRow(dup.DatabasePath, 1,
            dup.Collection.ArenaCards[1], "Repeated", 3, dup.BackupSha256);
        Must(selected.Current.Collection.ArenaCards[0].Quantity == 1 &&
             selected.Current.Collection.ArenaCards[1].Quantity == 3,
             "A duplicate name must be editable by exact selected ordinal only.");

        Console.WriteLine("PASS: native Arena add, case-insensitive increment, rename/quantity edit, selected-row removal, stale SHA, duplicate preservation, full rollback and unknown-data retention");
    }
}
