using ArcaneVault.Next.Collection;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text.Json;

internal static class NativeDeckEditingChecks
{
    private static void Must(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("Native Deck Library: " + message);
    }

    private static void Reject(Action action)
    {
        try { action(); }
        catch (InvalidDataException) { return; }
        catch (ArgumentException) { return; }
        throw new InvalidOperationException(
            "Native deck editor unexpectedly accepted an unsafe operation.");
    }

    private static byte[] Source() => JsonSerializer.SerializeToUtf8Bytes(new
    {
        schema = 2,
        cards = new object[]
        {
            new { name = "Arena Card", qty = 3, annotation = "preserve arena extras" }
        },
        physicalCards = new object[]
        {
            new { name = "Sol Ring", qty = 2, set = "ABC", number = "123",
                finish = "foil", scryfallId = "00000000-0000-4000-8000-000000000111",
                location = "Binder A" },
            new { name = "Unresolved", qty = 1, set = "", number = "",
                finish = "nonfoil", scryfallId = "",
                location = "Deckbox B" }
        },
        deckLibrary = new object[]
        {
            new {
                id = "legacy-command-1", name = "Legacy Commander",
                commander = "Muldrotha, the Gravetide",
                entries = new object[] {
                    new { name = "Sol Ring", qty = 1, role = "ramp", custom = 31 },
                    new { name = "Island", qty = 5, role = "land" }
                },
                revisions = new object[] {
                    new { name = "Early Commander",
                        commander = "Muldrotha, the Gravetide",
                        entries = new[] { new { name = "Island", qty = 1 } },
                        label = "v1", savedAt = "2026-01-01T00:00:00Z" }
                },
                tags = new[] { "graveyard", "control" },
                folder = "My decks",
                favorite = true, arenaDeckId = "arena-55",
                coverCard = "Muldrotha, the Gravetide",
                unknownFutureMeta = "keep-me"
            },
            new {
                id = "legacy-60-card", name = "Existing Modern",
                commander = "", format = "60-card",
                entries = new[] { new { name = "Forest", qty = 20 } },
                revisions = Array.Empty<object>(),
                ownerNotes = "no commander and no auto validation"
            }
        },
        preferredArtwork = new { first = new { url = "https://example.invalid/art.jpg" } },
        favorites = new[] { "Sol Ring" },
        wildcards = new { rare = 2 },
        extraFutureSettings = new { version = 11, untouched = true }
    });

    public static void Run(string work)
    {
        var source = Path.Combine(work, "native-decks-source.json");
        var native = Path.Combine(work, "native-decks-workspace.sqlite");
        var input = Source();
        File.WriteAllBytes(source, input);
        var sourceHash = SHA256.HashData(input);

        var initial = NativeWorkspace.ImportNewBytes(input, "deck-test-source", native);
        var opened = NativeWorkspaceSafety.OpenDeckLibrary(native);
        Must(opened.Decks.Count == 2 &&
             opened.Decks[0].Id == "legacy-command-1" &&
             opened.Decks[0].Commander == "Muldrotha, the Gravetide" &&
             opened.Decks[0].Revisions == 1 &&
             opened.Decks[0].Format == "Commander" &&
             opened.Decks[1].Format == "60-card" &&
             opened.Decks[1].Commander == "" &&
             opened.Decks[0].Cards.Single(x => x.Name == "Sol Ring").Quantity == 1,
            "Native reader failed to preserve imported Commander and 60-card decks.");

        var inputLines = "2 Sol Ring\n1 Atraxa // Back\r\n4 Island\n";
        var parsed = NativeDeckEditing.ParseCardLines(inputLines);
        Must(parsed.Count == 3 && parsed[1].Name == "Atraxa // Back" &&
             parsed[2].Quantity == 4 &&
             NativeDeckEditing.FormatCardLines(parsed).Contains("1 Atraxa // Back"),
            "Deck list parser must support split card names and quantities.");
        Reject(() => NativeDeckEditing.ParseCardLines("4 Island\n3 island"));
        Reject(() => NativeDeckEditing.ParseCardLines("0 Sol Ring"));
        Reject(() => NativeDeckEditing.ParseCardLines("Sol Ring"));
        Reject(() => NativeDeckEditing.ParseCardLines("1000 Sol Ring"));
        Reject(() => NativeDeckEditing.ParseCardLines("1 \t "));

        var created = NativeWorkspaceSafety.SaveDeck(native, null, "Pauper Test", "",
            "60-card", new[] {
                new NativeDeckEditing.DeckCard("Mountain", 4),
                new NativeDeckEditing.DeckCard("Lightning Bolt", 4)
            }, opened.BackupSha256);
        var a = NativeWorkspaceSafety.OpenDeckLibrary(native);
        Must(a.Decks.Count == 3 &&
             a.Decks[2].Name == "Pauper Test" &&
             a.Decks[2].Commander == "" &&
             a.Decks[2].CardCount == 8 &&
             a.Decks[2].Revisions == 0 &&
             created.Current.Collection.PhysicalCopies == 3 &&
             created.Current.Collection.ArenaCopies == 3 &&
             created.Current.Collection.PreferredArtworks == 1 &&
             NativeWorkspace.Open(created.RecoveryPath).BackupSha256 == opened.BackupSha256,
             "Creating deck must retain existing deck and collection data.");

        var legacy = a.Decks[0];
        Reject(() => NativeWorkspaceSafety.SaveDeck(native, legacy,
            legacy.Name, legacy.Commander, legacy.Format, legacy.Cards,
            a.BackupSha256));
        int backupCount = NativeWorkspaceSafety.RecoveryPaths(native).Count;
        var currentHash = SHA256.HashData(File.ReadAllBytes(native));
        Reject(() => NativeWorkspaceSafety.SaveDeck(native, legacy,
            "Update with stale SHA", legacy.Commander, legacy.Format, legacy.Cards,
            "stale-sha"));
        Reject(() => NativeWorkspaceSafety.SaveDeck(native, legacy,
            "Invalid deck", legacy.Commander, legacy.Format,
            new[] { new NativeDeckEditing.DeckCard("Sol Ring", 0) },
            a.BackupSha256));
        Reject(() => NativeWorkspaceSafety.SaveDeck(native, legacy,
            "Invalid deck", legacy.Commander, legacy.Format,
            new[] { new NativeDeckEditing.DeckCard("Sol Ring", 1),
                    new NativeDeckEditing.DeckCard("sol ring", 2) },
            a.BackupSha256));
        Must(backupCount == NativeWorkspaceSafety.RecoveryPaths(native).Count &&
             currentHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(native))),
            "Failed deck saves must not modify native data or create rollback files.");

        var updated = NativeWorkspaceSafety.SaveDeck(native, legacy,
            "Legacy Commander Renamed", "Muldrotha, the Gravetide", "Commander",
            new[] { new NativeDeckEditing.DeckCard("Sol Ring", 2),
                    new NativeDeckEditing.DeckCard("Island", 5),
                    new NativeDeckEditing.DeckCard("Mulldrifter", 1) },
            a.BackupSha256);
        var b = NativeWorkspaceSafety.OpenDeckLibrary(native);
        Must(b.Decks.Count == 3 &&
             b.Decks[0].Id == "legacy-command-1" &&
             b.Decks[0].Revisions == 2 &&
             b.Decks[0].Cards.Count == 3 &&
             b.Decks[1].OriginalJson == a.Decks[1].OriginalJson &&
             b.Decks[2].OriginalJson == a.Decks[2].OriginalJson &&
             updated.Current.Collection.DeckRevisions == 2 &&
             updated.Current.Collection.PhysicalCopies == 3,
             "Edit unexpectedly changed deck identity, unrelated decks, or ownership.");

        // Inspect full source to ensure custom metadata and revision history
        // survive even when a deck card quantity is changed.
        using (var db = new SqliteConnection(new SqliteConnectionStringBuilder
            { DataSource = native, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString()))
        {
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT backup FROM manifest WHERE id=1";
            using var json = JsonDocument.Parse((byte[])cmd.ExecuteScalar()!);
            var root = json.RootElement;
            var deck = root.GetProperty("deckLibrary")[0];
            Must(deck.GetProperty("unknownFutureMeta").GetString() == "keep-me" &&
                 deck.GetProperty("arenaDeckId").GetString() == "arena-55" &&
                 deck.GetProperty("folder").GetString() == "My decks" &&
                 deck.GetProperty("tags").GetArrayLength() == 2 &&
                 deck.GetProperty("favorite").GetBoolean() &&
                 deck.GetProperty("entries")[0].GetProperty("role").GetString() == "ramp" &&
                 deck.GetProperty("entries")[0].GetProperty("custom").GetInt32() == 31 &&
                 deck.GetProperty("entries")[1].GetProperty("role").GetString() == "land" &&
                 deck.GetProperty("revisions")[0].GetProperty("label").GetString() == "v1" &&
                 deck.GetProperty("revisions")[1].GetProperty("name").GetString() == "Legacy Commander" &&
                 deck.GetProperty("revisions")[1].GetProperty("entries")[0]
                     .GetProperty("qty").GetInt32() == 1 &&
                 root.GetProperty("extraFutureSettings").GetProperty("version").GetInt32() == 11 &&
                 root.GetProperty("physicalCards")[0].GetProperty("location").GetString() == "Binder A" &&
                 root.GetProperty("cards")[0].GetProperty("annotation").GetString() ==
                     "preserve arena extras",
                 "Deck update discarded imported annotations, metadata, old versions or source data.");
        }

        // Selecting an old view of a deck must fail even if a caller incorrectly
        // supplies the latest workspace SHA.
        Reject(() => NativeWorkspaceSafety.SaveDeck(native, a.Decks[0],
            "Race update", "Muldrotha, the Gravetide", "Commander", a.Decks[0].Cards,
            b.BackupSha256));

        var doubled = NativeWorkspaceSafety.DuplicateDeck(native, b.Decks[0],
            b.BackupSha256);
        var c = NativeWorkspaceSafety.OpenDeckLibrary(native);
        Must(c.Decks.Count == 4 &&
             c.Decks[3].Name == "Legacy Commander Renamed — Copy" &&
             c.Decks[3].Id != c.Decks[0].Id &&
             c.Decks[3].Revisions == 0 &&
             c.Decks[3].Cards.SequenceEqual(c.Decks[0].Cards) &&
             doubled.Current.Collection.Decks == 4 &&
             doubled.Current.Collection.DeckRevisions == 2,
             "Duplicate did not produce an independent deck ID without revision history.");

        Reject(() => NativeWorkspaceSafety.DeleteDeck(native,
            b.Decks[0], c.BackupSha256));
        var deleted = NativeWorkspaceSafety.DeleteDeck(native, c.Decks[1],
            c.BackupSha256);
        var d = NativeWorkspaceSafety.OpenDeckLibrary(native);
        Must(d.Decks.Count == 3 &&
             d.Decks.All(deck => deck.Id != "legacy-60-card") &&
             deleted.Current.Collection.Decks == 3 &&
             deleted.Current.Collection.DeckRevisions == 2 &&
             deleted.Current.Collection.PhysicalCopies == 3 &&
             deleted.Current.Collection.ArenaCopies == 3,
             "Deleting selected deck must not remove unrelated decks or owned cards.");

        Must(NativeWorkspaceSafety.RecoveryPaths(native).Count == 4,
            "Every successful native deck change must create one verified recovery point.");

        var restored = NativeWorkspaceSafety.Restore(native,
            created.RecoveryPath, d.BackupSha256);
        var e = NativeWorkspaceSafety.OpenDeckLibrary(native);
        Must(e.Decks.Count == 2 &&
             e.Decks[0].Name == "Legacy Commander" &&
             e.Decks[1].Name == "Existing Modern" &&
             e.Decks[0].Revisions == 1 &&
             restored.Current.Collection.PhysicalCopies == initial.Collection.PhysicalCopies &&
             restored.Current.Collection.ArenaCopies == initial.Collection.ArenaCopies &&
             File.Exists(restored.RecoveryPath) &&
             sourceHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(source))),
            "Recovery failed to restore exact original decks and preserve original JSON export.");

        Console.WriteLine("PASS: native deck create, edit with revision snapshots, annotations, 60-card, duplicate, delete, optimistic identity/SHA safety and full rollback");
    }
}
