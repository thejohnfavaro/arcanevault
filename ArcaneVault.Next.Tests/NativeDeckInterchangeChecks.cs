using ArcaneVault.Next.Collection;
using ArcaneVault.Next.Domain;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text.Json;

internal static class NativeDeckInterchangeChecks
{
    private static void Must(bool test, string reason)
    {
        if (!test) throw new InvalidOperationException("Native deck interchange: " + reason);
    }

    private static void Reject(Action action)
    {
        try { action(); }
        catch (InvalidDataException) { return; }
        throw new InvalidOperationException("An unsafe or unsupported deck import was accepted.");
    }

    public static void Run(string work)
    {
        string arena = """
            Commander
            1 Yuriko, the Tiger's Shadow (C18) 49

            Deck
            2 Ninja of the Deep Hours (C18) 94
            3 Island (DMU) 267
            1 Ninja of the Deep Hours (C18) 94
            1 Sol Ring
            """;
        var imported = NativeDeckTextExchange.Parse(arena);
        Must(imported.Commander == "Yuriko, the Tiger's Shadow" &&
             imported.CardNames == 3 &&
             imported.MainboardCopies == 7 &&
             imported.RemovedPrintingAnnotations &&
             imported.MainboardText.Contains("3 Ninja of the Deep Hours") &&
             imported.MainboardText.Contains("3 Island") &&
             imported.MainboardText.Contains("1 Sol Ring"),
            "Arena list parsing, Commander recognition, printing labels or duplicates failed.");

        string exported = NativeDeckTextExchange.Export(
            imported.MainboardText, imported.Commander);
        var roundtrip = NativeDeckTextExchange.Parse(exported);
        Must(roundtrip.MainboardText == imported.MainboardText &&
             roundtrip.Commander == imported.Commander &&
             !roundtrip.RemovedPrintingAnnotations,
            "A name/quantity deck should roundtrip losslessly through Arena-style plain text.");

        var plain = NativeDeckTextExchange.Parse("4 Counterspell\n22 Island\n");
        Must(plain.Commander == "" && plain.MainboardCopies == 26 &&
             plain.SourceKind.Contains("Plain"),
            "Ordinary count/name format must remain importable.");
        Reject(() => NativeDeckTextExchange.Parse(
            "Deck\n4 Island\nSideboard\n2 Duress"));
        Reject(() => NativeDeckTextExchange.Parse(
            "Commander\n1 Partner One\n1 Partner Two\nDeck\n1 Island"));
        Reject(() => NativeDeckTextExchange.Parse(
            "Deck\n3 Plains\nCompanion\n1 Lurrus of the Dream-Den"));
        Reject(() => NativeDeckTextExchange.Parse("Deck\n3 Plains\n4x Island"));
        Reject(() => NativeDeckTextExchange.Parse("Deck\n0 Island"));
        Reject(() => NativeDeckTextExchange.Parse("Deck\n1 Island\n1 Island\nSideboard\n1 Sol Ring"));
        Reject(() => NativeDeckTextExchange.Parse("Commander\n1 Example"));
        Reject(() => NativeDeckTextExchange.Export("", ""));
        var list = NativeDeckEditing.ParseMainboard(roundtrip.MainboardText);
        Must(list.Count == 3 && list.Sum(x => x.Quantity) == 7,
            "Deck interchange must use native save validation.");

        byte[] source = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema = 2,
            cards = new[] { new { name = "Arena Owned", qty = 4 } },
            physicalCards = new[] { new {
                name = "Sol Ring", qty = 2, set = "TST", number = "21",
                scryfallId = "00000000-0000-4000-8000-000000000772", finish = "foil"
            } },
            deckLibrary = new[] { new {
                id = "saved-legacy", name = "Existing Deck", commander = "Legacy Commander",
                format = "Commander", folder = "Personal", favorite = false,
                coverCard = "Sol Ring",
                entries = new[] { new { name = "Island", qty = 40 } },
                revisions = Array.Empty<object>(),
                tags = new[] { "existing" }, unknownFuture = new { keep = "forever" }
            } },
            preferredArtwork = new { selected = "Some printing" },
            unrecognizedState = new { dontLose = true }
        });
        string workspace = Path.Combine(work, "native-deck-interchange.sqlite");
        var initial = NativeWorkspace.ImportNewBytes(source, "deck-interchange", workspace);
        var sourceDeck = NativeWorkspaceSafety.ListDecks(workspace).Single();
        Must(sourceDeck.Folder == "Personal" && !sourceDeck.Favorite &&
             sourceDeck.Editable,
            "An imported legacy folder/favorite was not available for native editing.");

        var saved = NativeWorkspaceSafety.SaveDeck(workspace, sourceDeck.Name,
            sourceDeck.Commander, sourceDeck.MainboardText, sourceDeck,
            initial.BackupSha256, format: sourceDeck.Format,
            folder: "Competitive Brawl", favorite: true);
        var edited = NativeWorkspaceSafety.ListDecks(workspace).Single();
        Must(edited.Id == "saved-legacy" &&
             edited.Folder == "Competitive Brawl" && edited.Favorite &&
             edited.FavoriteLabel.Contains("Favorite") &&
             edited.MainboardText == sourceDeck.MainboardText &&
             edited.Revisions == 1,
            "Folder/favorite-only edit should update exactly one deck and append a revision.");

        var prior = NativeWorkspace.Open(saved.RecoveryPath).Collection;
        var current = saved.Current.Collection;
        Must(prior.ArenaCopies == current.ArenaCopies &&
             prior.PhysicalCopies == current.PhysicalCopies &&
             prior.PreferredArtworks == current.PreferredArtworks &&
             current.Decks == 1 && current.DeckRevisions == 1,
            "Deck organization changed ownership/artwork or failed to preserve rollback.");
        using (var db = new SqliteConnection(new SqliteConnectionStringBuilder {
            DataSource = workspace, Mode = SqliteOpenMode.ReadOnly, Pooling = false
        }.ToString()))
        {
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT backup FROM manifest WHERE id=1";
            using var doc = JsonDocument.Parse((byte[])cmd.ExecuteScalar()!);
            var root = doc.RootElement;
            var entry = root.GetProperty("deckLibrary")[0];
            Must(entry.GetProperty("favorite").GetBoolean() &&
                 entry.GetProperty("folder").GetString() == "Competitive Brawl" &&
                 entry.GetProperty("unknownFuture").GetProperty("keep").GetString() == "forever" &&
                 entry.GetProperty("tags")[0].GetString() == "existing" &&
                 entry.GetProperty("coverCard").GetString() == "Sol Ring" &&
                 !entry.GetProperty("revisions")[0].GetProperty("favorite").GetBoolean() &&
                 entry.GetProperty("revisions")[0].GetProperty("folder").GetString() == "Personal" &&
                 root.GetProperty("unrecognizedState").GetProperty("dontLose").GetBoolean(),
                "Deck organization edit did not preserve legacy fields or revision metadata.");
        }

        string before = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(workspace)));
        Reject(() => NativeWorkspaceSafety.SaveDeck(workspace, sourceDeck.Name,
            sourceDeck.Commander, sourceDeck.MainboardText, edited,
            saved.Current.BackupSha256, format: edited.Format,
            folder: edited.Folder, favorite: true));
        Must(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(workspace))) == before,
            "No-op favorite/folder saves must not modify the native database.");
        Reject(() => NativeWorkspaceSafety.SaveDeck(workspace,
            "Stale deck", edited.Commander, edited.MainboardText,
            sourceDeck, saved.Current.BackupSha256, folder: "Other"));
        Must(NativeWorkspaceSafety.ListDecks(workspace).Single().Favorite,
            "Stale deck edits must not change active favorite metadata.");

        // Full restore returns the original deck organization and revisions.
        var restored = NativeWorkspaceSafety.Restore(
            workspace, saved.RecoveryPath, saved.Current.BackupSha256);
        Must(restored.Current.BackupSha256 == initial.BackupSha256 &&
             !NativeWorkspaceSafety.ListDecks(workspace).Single().Favorite &&
             NativeWorkspaceSafety.ListDecks(workspace).Single().Folder == "Personal",
            "Deck folder/favorite update must restore exactly from the previous snapshot.");
    }
}
