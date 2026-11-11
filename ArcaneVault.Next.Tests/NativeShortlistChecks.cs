using ArcaneVault.Next.Collection;
using ArcaneVault.Next.Domain;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text.Json;

internal static class NativeShortlistChecks
{
    private const string RingId = "00000000-0000-4000-8000-000000000e01";
    private const string DigitalId = "00000000-0000-4000-8000-000000000e02";
    private static void Must(bool value, string why)
    {
        if (!value) throw new InvalidOperationException("Native shortlist: " + why);
    }
    private static void Reject(Action action)
    {
        try { action(); }
        catch (InvalidDataException) { return; }
        throw new InvalidOperationException("Unsafe shortlist mutation was accepted.");
    }
    private static string Hash(string file) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));

    public static void Run(string work)
    {
        byte[] backup = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema = 2,
            source = "legacy-webview-full-backup",
            cards = new[] { new { name = "Arena Owned", qty = 2 },
                new { name = "Sol Ring", qty = 1 } },
            physicalCards = new[] {
                new { name = "Sol Ring", qty = 2, set = "cmm", number = "396",
                    finish = "nonfoil", scryfallId = RingId },
                new { name = "Mystery Card", qty = 3, set = "", number = "",
                    finish = "unspecified", scryfallId = "" },
                new { name = "Digital Only", qty = 1, set = "tst", number = "1",
                    finish = "nonfoil", scryfallId = DigitalId }
            },
            deckLibrary = new[] { new {
                id = "deck-1", name = "Keep Deck", folder = "Personal",
                entries = new[] { new { name = "Sol Ring", qty = 1 } },
                revisions = Array.Empty<object>(), unknownDeck = "preserved"
            } },
            preferredArtwork = new Dictionary<string, object> {
                ["sol ring"] = new { id = RingId, unknownArt = "preserved" } },
            favorites = new[] { "Sol Ring", "Old Missing Card", "Arena Owned" },
            studio = new { draft = "preserve this" },
            otherUnknown = new { keep = 987 }
        });
        var before = NativeBackupReader.Read(backup);
        var favorites = NativeShortlist.Read(backup);
        Must(favorites.SequenceEqual(new[] { "Sol Ring", "Old Missing Card", "Arena Owned" }) &&
             NativeShortlist.ArenaText(favorites) ==
                "1 Sol Ring\n1 Old Missing Card\n1 Arena Owned",
            "Legacy bookmark ordering and one-copy Arena text must match WebView semantics.");

        Must(NativeShortlist.Read(JsonSerializer.SerializeToUtf8Bytes(new {
            schema = 2, cards = Array.Empty<object>(),
            physicalCards = Array.Empty<object>(), deckLibrary = Array.Empty<object>(),
            preferredArtwork = new { }
        })).Count == 0, "Missing legacy favorites must mean empty shortlist.");

        Reject(() => NativeShortlist.Set(backup, "sol ring", true));
        Reject(() => NativeShortlist.Set(backup, "not saved", false));
        Reject(() => NativeShortlist.Set(backup, "invalid\nline", true));
        Reject(() => NativeShortlist.Read(JsonSerializer.SerializeToUtf8Bytes(new {
            schema = 2, cards = Array.Empty<object>(),
            physicalCards = Array.Empty<object>(), deckLibrary = Array.Empty<object>(),
            preferredArtwork = new { }, favorites = new[] { "Sol Ring", "sol ring" }
        })));

        byte[] added = NativeShortlist.Set(backup, "Mystery Card", true);
        var addedFavorites = NativeShortlist.Read(added);
        Must(addedFavorites.Count == 4 && addedFavorites[^1] == "Mystery Card",
            "Adding an entry must append, without changing existing favorites.");
        var after = NativeBackupReader.Read(added);
        Must(before.PhysicalCards.SequenceEqual(after.PhysicalCards) &&
             before.ArenaCards.SequenceEqual(after.ArenaCards) &&
             before.PhysicalCopies == after.PhysicalCopies &&
             before.ArenaCopies == after.ArenaCopies &&
             before.Decks == after.Decks &&
             before.DeckRevisions == after.DeckRevisions &&
             before.PreferredArtworks == after.PreferredArtworks,
            "Bookmark edits must not modify collection, decks or preferred artwork.");
        using (var doc = JsonDocument.Parse(added))
        {
            var root = doc.RootElement;
            Must(root.GetProperty("otherUnknown").GetProperty("keep").GetInt32() == 987 &&
                 root.GetProperty("studio").GetProperty("draft").GetString() ==
                    "preserve this" &&
                 root.GetProperty("deckLibrary")[0].GetProperty("unknownDeck")
                    .GetString() == "preserved" &&
                 root.GetProperty("preferredArtwork").GetProperty("sol ring")
                    .GetProperty("unknownArt").GetString() == "preserved",
                "Opaque legacy fields should never be dropped from shortlist edits.");
        }
        Must(NativeShortlist.Read(NativeShortlist.Set(added,
            "OLD MISSING CARD", false)).SequenceEqual(
                new[] { "Sol Ring", "Arena Owned", "Mystery Card" }),
            "Case-insensitive remove must preserve other bookmarks.");

        string catalog = Path.Combine(work, "native-shortlist-catalog.sqlite");
        using (var db = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = catalog, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false }.ToString()))
        {
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE variants(
                    print_id TEXT PRIMARY KEY,name TEXT,setcode TEXT,collector TEXT,
                    games TEXT,digital INTEGER);
                INSERT INTO variants VALUES
                ('00000000-0000-4000-8000-000000000e01','Sol Ring','cmm','396','paper',0),
                ('00000000-0000-4000-8000-000000000e02','Digital Only','tst','1','arena',1);
                """;
            cmd.ExecuteNonQuery();
        }
        var review = CollectionCatalogReview.Examine(after, catalog);
        var summaries = NativeShortlist.Summarize(addedFavorites, after, review);
        Must(summaries.Single(x => x.Name == "Sol Ring").VerifiedPhysicalCopies == 2 &&
             summaries.Single(x => x.Name == "Sol Ring").ArenaCopies == 1 &&
             summaries.Single(x => x.Name == "Arena Owned").ArenaCopies == 2 &&
             !summaries.Single(x => x.Name == "Old Missing Card").ConfirmedOwned &&
             summaries.Single(x => x.Name == "Mystery Card").UnresolvedPhysicalCopies == 3 &&
             !summaries.Single(x => x.Name == "Mystery Card").ConfirmedOwned,
            "Physical verified, unresolved and Arena owned statuses must not be conflated.");
        string catalogHash = Hash(catalog);

        string path = Path.Combine(work, "native-shortlist.sqlite");
        var original = NativeWorkspace.ImportNewBytes(backup, "legacy-shortlist", path);
        Must(NativeWorkspaceSafety.ListShortlist(path).Count == 3,
            "Native workspace must read the unchanged legacy favorites array.");
        var saved = NativeWorkspaceSafety.SetShortlistCard(
            path, "Mystery Card", true, original.BackupSha256);
        Must(File.Exists(saved.RecoveryPath) &&
             saved.Current.Collection.PhysicalCopies == before.PhysicalCopies &&
             saved.Current.Collection.ArenaCopies == before.ArenaCopies &&
             NativeWorkspaceSafety.ListShortlist(path).Count == 4 &&
             NativeWorkspace.Open(saved.RecoveryPath).BackupSha256 ==
                original.BackupSha256,
            "SHA-checked write must preserve complete recovery and quantities.");
        string sha = Hash(path);
        Reject(() => NativeWorkspaceSafety.SetShortlistCard(
            path, "Mystery Card", true, saved.Current.BackupSha256));
        Reject(() => NativeWorkspaceSafety.SetShortlistCard(
            path, "Another Card", true, original.BackupSha256));
        Must(Hash(path) == sha,
            "No-op and stale writes must leave authoritative native database byte-identical.");

        var removed = NativeWorkspaceSafety.SetShortlistCard(
            path, "Old Missing Card", false, saved.Current.BackupSha256);
        Must(NativeWorkspaceSafety.ListShortlist(path).Count == 3,
            "Removing a legacy bookmark must not erase other favorites.");
        var restored = NativeWorkspaceSafety.Restore(path,
            saved.RecoveryPath, removed.Current.BackupSha256);
        Must(restored.Current.BackupSha256 == original.BackupSha256 &&
             NativeWorkspaceSafety.ListShortlist(path).SequenceEqual(favorites) &&
             Hash(catalog) == catalogHash,
            "Full rollback should restore exact favorite list and not touch catalog.");
    }
}
