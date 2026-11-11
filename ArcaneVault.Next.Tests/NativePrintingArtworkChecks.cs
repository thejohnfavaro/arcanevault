using ArcaneVault.Next.Collection;
using ArcaneVault.Next.Domain;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text.Json;

internal static class NativePrintingArtworkChecks
{
    private const string Original = "00000000-0000-4000-8000-000000000901";
    private const string Preferred = "00000000-0000-4000-8000-000000000902";
    private const string Other = "00000000-0000-4000-8000-000000000903";
    private const string LegacyUrl = "https://cards.scryfall.io/normal/front/1/1/legacy.jpg";
    private const string NewUrl = "https://cards.scryfall.io/normal/front/2/2/new.jpg";

    private static void Must(bool check, string reason)
    {
        if (!check) throw new InvalidOperationException("Preferred artwork: " + reason);
    }

    private static void Reject(Action operation)
    {
        try { operation(); }
        catch (InvalidDataException) { return; }
        throw new InvalidOperationException("Unsafe or no-op artwork update was accepted.");
    }

    private static string Hash(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private static byte[] Manifest(string path)
    {
        using var db = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        db.Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT backup FROM manifest WHERE id=1";
        return (byte[])cmd.ExecuteScalar()!;
    }

    public static void Run(string work)
    {
        Must(NativePrintingArtwork.TrustedImage(NewUrl) &&
             !NativePrintingArtwork.TrustedImage("http://cards.scryfall.io/normal/card.jpg") &&
             !NativePrintingArtwork.TrustedImage("https://evil.example/card.jpg") &&
             !NativePrintingArtwork.TrustedImage("https://cards.scryfall.io.evil.example/card.jpg") &&
             !NativePrintingArtwork.TrustedImage("file:///c:/card.jpg"),
            "Only HTTPS official Scryfall CDN images may be saved.");

        string catalogPath = Path.Combine(work, "artwork-catalog.sqlite");
        using (var db = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = catalogPath, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false }.ToString()))
        {
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE variants(
                    print_id TEXT PRIMARY KEY,name TEXT NOT NULL COLLATE NOCASE,
                    setcode TEXT NOT NULL,collector TEXT NOT NULL,
                    setname TEXT NOT NULL,image TEXT NOT NULL,
                    games TEXT NOT NULL,digital INTEGER NOT NULL,
                    release TEXT NOT NULL);
                INSERT INTO variants VALUES
                ('00000000-0000-4000-8000-000000000901','Sol Ring','cmm','396',
                 'Commander Masters','https://cards.scryfall.io/normal/front/1/1/legacy.jpg',
                 'paper',0,'2023-08-04'),
                ('00000000-0000-4000-8000-000000000902','Sol Ring','sld','123',
                 'Secret Lair','https://cards.scryfall.io/normal/front/2/2/new.jpg',
                 'paper',0,'2025-02-10'),
                ('00000000-0000-4000-8000-000000000903','Unrelated Card','tst','7',
                 'Test Set','https://cards.scryfall.io/normal/front/3/3/other.jpg',
                 'arena',1,'2024-01-01'),
                ('00000000-0000-4000-8000-000000000904','Sol Ring','tst','11',
                 'Test Set','https://evil.example/stolen.jpg',
                 'paper',0,'2024-02-01'),
                ('00000000-0000-4000-8000-000000000905','Sol Ring','ana','12',
                 'Arena Styles','https://cards.scryfall.io/normal/front/5/5/arena.jpg',
                 'arena',1,'2022-01-01'),
                ('00000000-0000-4000-8000-000000000906','Sol Ring','trk','111',
                 'Shared Set','https://cards.scryfall.io/normal/front/6/6/base.jpg',
                 'paper,arena',0,'2026-11-13'),
                ('00000000-0000-4000-8000-000000000907','Sol Ring','trk','428',
                 'Shared Set','https://cards.scryfall.io/normal/front/7/7/paper-alt.jpg',
                 'paper,arena',0,'2026-11-13'),
                ('00000000-0000-4000-8000-000000000908','Sol Ring','trk','438',
                 'Shared Set','https://cards.scryfall.io/normal/front/8/8/paper-showcase.jpg',
                 'paper,arena',0,'2026-11-13');
                """;
            cmd.ExecuteNonQuery();
        }
        string catalogHash = Hash(catalogPath);
        var gallery = NativePrintingArtwork.Read(catalogPath, "Sol Ring");
        Must(gallery.Printings.Count == 7 && !gallery.Truncated &&
             gallery.Printings[0].SetCode == "trk" &&
             gallery.Printings.Any(p => p.Id.ToString("D") == Preferred) &&
             gallery.Printings.Any(p => !NativePrintingArtwork.TrustedImage(p.ImageUrl)),
            "Gallery must show exact printings ordered newest first, even with unavailable artwork.");
        var arenaStyles = NativePrintingArtwork.Read(catalogPath, "Sol Ring",
            arenaOnly: true);
        Must(arenaStyles.Printings.Count == 2 &&
             arenaStyles.Printings.Any(x => x.SetCode == "ana" &&
                 x.ImageUrl.EndsWith("/arena.jpg", StringComparison.Ordinal)) &&
             arenaStyles.Printings.Single(x => x.SetCode == "trk")
                 .CollectorNumber == "111" &&
             !arenaStyles.Printings.Any(x => x.CollectorNumber is "428" or "438"),
            "Arena gallery must show the base shared art, not collector-only paper alternatives; independently digital styles are retained.");
        Must(NativePrintingArtwork.Read(catalogPath, "Sol Ring", 1).Truncated,
            "Gallery must disclose truncated printing lists.");
        Must(NativePrintingArtwork.Require(catalogPath, "Sol Ring",
                 Guid.Parse(Preferred)).ImageUrl == NewUrl,
            "Selected Scryfall printing must be revalidated at write time.");
        Reject(() => NativePrintingArtwork.Require(catalogPath, "Sol Ring",
            Guid.Parse(Other)));
        Reject(() => NativePrintingArtwork.Require(catalogPath, "Sol Ring",
            Guid.Parse("00000000-0000-4000-8000-000000000904")));

        byte[] original = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema = 2,
            cards = new[] { new { name = "Arena Card", qty = 2 } },
            physicalCards = new[]
            {
                new { name = "Sol Ring", qty = 4, set = "cmm", number = "396",
                    finish = "nonfoil", scryfallId = Original }
            },
            deckLibrary = new[] { new
            {
                id = "saved-artwork-deck", name = "My Deck",
                entries = new[] { new { name = "Sol Ring", qty = 1 } },
                revisions = Array.Empty<object>(),
                extraFutureDeckMetadata = "preserve"
            } },
            preferredArtwork = new Dictionary<string, object>
            {
                ["sol ring"] = new
                {
                    id = Original, image = LegacyUrl,
                    set = "cmm", collectorNumber = "396",
                    legacyUnknown = "keep old notes"
                },
                ["unrelated card"] = new
                {
                    id = Other, image = "https://cards.scryfall.io/normal/front/3/3/other.jpg",
                    set = "tst", collectorNumber = "7"
                }
            },
            themeSettings = new { accent = "orange" },
            ownedSource = "legacy-full-backup"
        });
        string nativePath = Path.Combine(work, "preferred-art-native.sqlite");
        var workspace = NativeWorkspace.ImportNewBytes(original,
            "artwork-test-original", nativePath);
        var oldPref = NativePrintingArtwork.ReadPreferred(original, "Sol Ring");
        Must(oldPref is not null && oldPref.Id.ToString("D") == Original &&
             oldPref.Image == LegacyUrl,
            "A legacy WebView preferredPrintingArtworkV1 record must remain readable.");
        Must(NativeWorkspaceSafety.GetPreferredArtwork(nativePath, "SOL RING")?.Image
             == LegacyUrl, "Native workspace should expose existing legacy artwork preferences.");

        var selected = NativePrintingArtwork.Require(catalogPath, "Sol Ring",
            Guid.Parse(Preferred));
        byte[] transformed = NativePrintingArtwork.Choose(original, selected);
        var transformedView = NativeBackupReader.Read(transformed);
        var previousView = NativeBackupReader.Read(original);
        Must(transformedView.PhysicalCards.SequenceEqual(previousView.PhysicalCards) &&
             transformedView.ArenaCards.SequenceEqual(previousView.ArenaCards) &&
             transformedView.Decks == previousView.Decks &&
             transformedView.DeckRevisions == previousView.DeckRevisions &&
             transformedView.PreferredArtworks == previousView.PreferredArtworks,
            "Artwork preference transformation must not modify inventories, decks or counts.");
        using (var json = JsonDocument.Parse(transformed))
        {
            var root = json.RootElement;
            var saved = root.GetProperty("preferredArtwork");
            var chosen = saved.GetProperty("sol ring");
            Must(chosen.GetProperty("id").GetString() == Preferred &&
                 chosen.GetProperty("image").GetString() == NewUrl &&
                 chosen.GetProperty("set").GetString() == "sld" &&
                 chosen.GetProperty("collectorNumber").GetString() == "123" &&
                 chosen.GetProperty("legacyUnknown").GetString() == "keep old notes" &&
                 saved.GetProperty("unrelated card").GetProperty("id").GetString() == Other &&
                 root.GetProperty("themeSettings").GetProperty("accent").GetString() == "orange" &&
                 root.GetProperty("deckLibrary")[0].GetProperty("extraFutureDeckMetadata")
                    .GetString() == "preserve",
                "Must preserve the WebView preference record format and all unrelated opaque data.");
        }
        Reject(() => NativePrintingArtwork.Choose(transformed, selected));
        Reject(() => NativePrintingArtwork.Reset(original, "missing card"));

        var savedWorkspace = NativeWorkspaceSafety.SavePreferredArtwork(
            nativePath, catalogPath, "Sol Ring", Guid.Parse(Preferred),
            workspace.BackupSha256);
        Must(NativeWorkspaceSafety.GetPreferredArtwork(nativePath, "Sol Ring")?.Image == NewUrl &&
             savedWorkspace.Current.Collection.PhysicalCopies == 4 &&
             savedWorkspace.Current.Collection.ArenaCopies == 2 &&
             savedWorkspace.Current.Collection.PreferredArtworks == 2 &&
             File.Exists(savedWorkspace.RecoveryPath),
            "Atomic native preference save should keep ownership and preserve a complete recovery file.");
        Must(NativeWorkspace.Open(savedWorkspace.RecoveryPath).BackupSha256 ==
             workspace.BackupSha256 && Hash(catalogPath) == catalogHash,
            "Artwork update must leave original catalog unchanged and retain prior collection.");
        Reject(() => NativeWorkspaceSafety.SavePreferredArtwork(
            nativePath, catalogPath, "Sol Ring", Guid.Parse(Preferred),
            workspace.BackupSha256));
        Reject(() => NativeWorkspaceSafety.SavePreferredArtwork(
            nativePath, catalogPath, "Unrelated Card", Guid.Parse(Preferred),
            savedWorkspace.Current.BackupSha256));
        Reject(() => NativeWorkspaceSafety.SavePreferredArtwork(
            nativePath, catalogPath, "Sol Ring",
            Guid.Parse("00000000-0000-4000-8000-000000000904"),
            savedWorkspace.Current.BackupSha256));

        var cleared = NativeWorkspaceSafety.ResetPreferredArtwork(
            nativePath, "sol ring", savedWorkspace.Current.BackupSha256);
        Must(NativeWorkspaceSafety.GetPreferredArtwork(nativePath, "Sol Ring") is null &&
             cleared.Current.Collection.PreferredArtworks == 1 &&
             cleared.Current.Collection.PhysicalCopies == 4 &&
             cleared.Current.Collection.ArenaCopies == 2 &&
             File.Exists(cleared.RecoveryPath),
            "Reset should remove only one display preference, not owned copies.");
        var restored = NativeWorkspaceSafety.Restore(nativePath,
            savedWorkspace.RecoveryPath, cleared.Current.BackupSha256);
        Must(restored.Current.BackupSha256 == workspace.BackupSha256 &&
             NativeWorkspaceSafety.GetPreferredArtwork(nativePath, "Sol Ring")?.Image ==
                LegacyUrl,
            "Full recovery must restore the original legacy preference exactly.");
        Must(Hash(catalogPath) == catalogHash,
            "Catalog should remain untouched throughout artwork edits.");
    }
}
