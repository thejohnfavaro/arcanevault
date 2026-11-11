using ArcaneVault.Next.Collection;
using ArcaneVault.Next.Domain;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text.Json;

internal static class NativeSetProgressChecks
{
    private static void Must(bool assertion, string message)
    {
        if (!assertion) throw new InvalidOperationException("Set Progress: " + message);
    }

    public static void Run(string work)
    {
        string catalog = Path.Combine(work, "native-sets-fixture.sqlite");
        string backupFile = Path.Combine(work, "native-sets-backup.json");

        const string idA = "00000000-0000-4000-8000-000000000101";
        const string idB = "00000000-0000-4000-8000-000000000102";
        const string idE = "00000000-0000-4000-8000-000000000105";
        const string idDigital = "00000000-0000-4000-8000-000000000107";
        File.WriteAllText(backupFile, JsonSerializer.Serialize(new
        {
            schema = 2,
            cards = new[] {
                new { name = "Arena Separate", qty = 4 },
                new { name = "Card A", qty = 2 },
                new { name = "Digital Only", qty = 1 }
            },
            physicalCards = new object[] {
                new { name = "Card A", qty = 2, set = "ZXQ", number = "001", finish = "foil", scryfallId = idA },
                new { name = "Card A", qty = 1, set = "ZXQ", number = "001", finish = "etched", scryfallId = idA },
                new { name = "Card B", qty = 4, set = "ZXQ", number = "002", finish = "nonfoil", scryfallId = idB },
                new { name = "Card E", qty = 1, set = "YYY", number = "100", finish = "foil", scryfallId = idE },
                new { name = "Card C", qty = 5, set = "ZXQ", number = "010", finish = "nonfoil", scryfallId = "" },
                new { name = "Digital Only", qty = 3, set = "ZXQ", number = "011", finish = "foil", scryfallId = idDigital },
                new { name = "Card A", qty = 1, set = "ZXQ", number = "099", finish = "foil", scryfallId = idA }
            },
            deckLibrary = Array.Empty<object>(),
            preferredArtwork = new { }
        }));

        using (var db = new SqliteConnection(new SqliteConnectionStringBuilder
               { DataSource = catalog, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false }.ToString()))
        {
            db.Open();
            using var sql = db.CreateCommand();
            sql.CommandText = """
                CREATE TABLE variants(
                    print_id TEXT PRIMARY KEY,name TEXT NOT NULL,setcode TEXT NOT NULL,
                    collector TEXT NOT NULL, games TEXT NOT NULL,digital INTEGER NOT NULL,
                    setname TEXT NOT NULL,release TEXT NOT NULL
                );
                INSERT INTO variants VALUES
                    ('00000000-0000-4000-8000-000000000101','Card A','ZXQ','001','paper,arena',0,'Zeta Set','2026-01-01'),
                    ('00000000-0000-4000-8000-000000000102','Card B','ZXQ','002','paper',0,'Zeta Set','2026-01-01'),
                    ('00000000-0000-4000-8000-000000000103','Card C','ZXQ','010','paper',0,'Zeta Set','2026-01-01'),
                    ('00000000-0000-4000-8000-000000000104','A Different Catalog Print','ZXQ','001','paper',0,'Zeta Set','2026-01-01'),
                    ('00000000-0000-4000-8000-000000000105','Card E','YYY','100','paper',0,'Yotta Set','2026-02-01'),
                    ('00000000-0000-4000-8000-000000000106','Card F','YYY','101','paper',0,'Yotta Set','2026-02-01'),
                    ('00000000-0000-4000-8000-000000000107','Digital Only','ZXQ','011','arena',1,'Zeta Set','2026-01-01'),
                    ('00000000-0000-4000-8000-000000000108','Arena No Paper','ZXQ','012','arena',0,'Zeta Set','2026-01-01'),
                    ('00000000-0000-4000-8000-000000000109','No Copies','NNN','1','paper',0,'No Ownership Set','2025-05-01');
                CREATE TABLE ownership(name TEXT,qty INTEGER);
                INSERT INTO ownership VALUES('Do not import ownership',987);
                """;
            sql.ExecuteNonQuery();
            sql.CommandText = """
                ALTER TABLE variants ADD COLUMN arena INTEGER NOT NULL DEFAULT 0;
                UPDATE variants SET arena=1
                WHERE instr(',' || lower(games) || ',', ',arena,') > 0;
                -- A second Arena style of the same card within this set must
                -- NOT inflate distinct card-name coverage.
                INSERT INTO variants VALUES
                    ('00000000-0000-4000-8000-000000000110',
                     'Card A','ZXQ','099','arena',1,
                     'Zeta Set','2026-01-01',1);
                """;
            sql.ExecuteNonQuery();
        }

        var sourceHash = SHA256.HashData(File.ReadAllBytes(backupFile));
        var catalogHash = SHA256.HashData(File.ReadAllBytes(catalog));
        var sourceTime = File.GetLastWriteTimeUtc(backupFile);
        var catalogTime = File.GetLastWriteTimeUtc(catalog);

        var backup = NativeBackupReader.Read(backupFile);
        var unverified = CollectionCatalogReview.Examine(backup, null);
        bool blocked = false;
        try { NativeSetProgress.Build(unverified, catalog); }
        catch (InvalidDataException) { blocked = true; }
        Must(blocked, "Unverified backups cannot create percentages.");

        var review = CollectionCatalogReview.Examine(backup, catalog);
        Must(review.TotalCopies == 17 && review.ConfirmedCopies == 8 &&
             review.UnresolvedCopies == 9 && review.QuantitiesConserved,
            "Physical copy counts incorrectly credit a non-paper print, hint-only card or conflict.");
        Must(review.Rows[5].Status == "Not a physical paper printing",
            "Digital-only print was incorrectly counted as physical ownership.");

        var progress = NativeSetProgress.Build(review, catalog);
        Must(progress.Sets.Count == 3 && progress.CatalogPaperSlots == 6 &&
             progress.ConfirmedOwnedSlots == 3 && progress.ConfirmedOwnedCopies == 8,
            "Set totals cannot invent collector slots or lose confirmed copies.");
        Must(progress.Sets.Select(s => s.SetCode).SequenceEqual(new[] { "YYY", "ZXQ", "NNN" }),
            "Set chronology should use earliest catalog print release descending.");
        var zeta = progress.Sets.Single(s => s.SetCode == "ZXQ");
        Must(zeta.TotalSlots == 3 && zeta.OwnedSlots == 2 &&
             zeta.VerifiedCopies == 7 && Math.Abs(zeta.Percent - 66.6666667) < .0001,
             "Duplicated foil/etched copies or multiple print IDs must share one collector slot.");
        var yotta = progress.Sets.Single(s => s.SetCode == "YYY");
        Must(yotta.TotalSlots == 2 && yotta.OwnedSlots == 1 && yotta.Percent == 50,
            "Second set percentage incorrect.");
        Must(progress.Sets.Single(s => s.SetCode == "NNN").OwnedSlots == 0,
            "Unowned set should be present for optional show-empty filter.");

        var slots = NativeSetProgress.LoadSetSlots(review, catalog, "ZXQ");
        Must(slots.Count == 3 &&
             slots.Select(s => s.CollectorNumber).SequenceEqual(new[] { "001", "002", "010" }),
            "Collector slot list must be sorted numerically and exclude digital/arena-only entries.");
        Must(slots[0].CardName == "Card A" &&
             slots[0].VerifiedCopies == 3 && slots[1].VerifiedCopies == 4 &&
             slots[2].VerifiedCopies == 0 && slots[2].Ownership == "Missing",
            "Only verified physical print IDs count; name-only hint cannot mark slot owned.");
        Must(NativeSetProgress.LoadSetSlots(review, catalog, "ZXQ' OR 1=1 --").Count == 0,
            "Set drilldown is required to parameterize SQL.");

        // Arena mode is a separate *name coverage* metric. An Arena-owned
        // name is not a verified set-specific printing or physical slot.
        var arenaScope = NativeSetProgress.BuildArena(backup.ArenaCards, catalog);
        Must(arenaScope.Sets.Count == 1 &&
             arenaScope.Sets[0].NameCoverage &&
             arenaScope.Sets[0].SetCode == "ZXQ" &&
             arenaScope.Sets[0].TotalSlots == 3 &&
             arenaScope.Sets[0].OwnedSlots == 2 &&
             arenaScope.Sets[0].Percent > 66 &&
             arenaScope.Sets[0].Percent < 67,
            "Arena set progress must count distinct available card names, not printings or copied styles.");
        var arenaNames = NativeSetProgress.LoadArenaSetNames(
            backup.ArenaCards, catalog, "ZXQ");
        Must(arenaNames.Count == 3 &&
             arenaNames.Count(x => x.VerifiedCopies > 0) == 2 &&
             arenaNames.All(x => x.NameCoverage) &&
             arenaNames.Single(x => x.CardName == "Card A").Ownership.StartsWith("Name-owned",
                 StringComparison.Ordinal) &&
             NativeSetProgress.LoadArenaSetNames(
                 backup.ArenaCards, catalog, "ZXQ' OR 1=1 --").Count == 0,
            "Arena checklist must use name-only quantities and parameterized set filtering.");
        Must(progress.ConfirmedOwnedCopies == 8 &&
             progress.Sets.Single(x => x.SetCode == "ZXQ").OwnedSlots == 2,
            "Arena name coverage must never change verified physical set ownership.");

        // Artwork previews are catalog metadata, not evidence of ownership.
        // Keep the original minimal-schema catalog unchanged and ensure the
        // richer catalog can add representative set/cards art read-only.
        string artCatalog = Path.Combine(work, "set-progress-artwork.sqlite");
        File.Copy(catalog, artCatalog);
        using (var db = new SqliteConnection(new SqliteConnectionStringBuilder
               { DataSource = artCatalog, Mode = SqliteOpenMode.ReadWrite, Pooling = false }.ToString()))
        {
            db.Open();
            using var update = db.CreateCommand();
            update.CommandText = """
                ALTER TABLE variants ADD COLUMN image TEXT NOT NULL DEFAULT '';
                UPDATE variants SET image =
                  'https://cards.scryfall.io/normal/front/1/1/catalog-preview.jpg'
                WHERE setcode='ZXQ' AND collector='001';
                """;
            update.ExecuteNonQuery();
        }
        var artProgress = NativeSetProgress.Build(review, artCatalog);
        Must(artProgress.Sets.Single(x => x.SetCode == "ZXQ").ArtworkUrl
                 .EndsWith("/catalog-preview.jpg", StringComparison.Ordinal),
            "Verified physical set tiles must load representative official card art.");
        var artSlots = NativeSetProgress.LoadSetSlots(review, artCatalog, "ZXQ");
        Must(artSlots[0].ArtworkUrl.EndsWith("/catalog-preview.jpg",
                 StringComparison.Ordinal) && artSlots[2].ArtworkUrl == "",
            "Set checklist must provide artwork when present and allow safe blank art.");
        var artArena = NativeSetProgress.BuildArena(backup.ArenaCards, artCatalog);
        Must(artArena.Sets.Single(x => x.SetCode == "ZXQ").ArtworkUrl
                 .EndsWith("/catalog-preview.jpg", StringComparison.Ordinal),
            "Arena set tiles should use catalog art without claiming specific owned printings.");
        var artNames = NativeSetProgress.LoadArenaSetNames(
            backup.ArenaCards, artCatalog, "ZXQ");
        Must(artNames.Single(x => x.CardName == "Card A").ArtworkUrl
                 .EndsWith("/catalog-preview.jpg", StringComparison.Ordinal),
            "Arena name checklist should show catalog art without altering name-only ownership.");

        // Cross-version persistent cache: second open must not rebuild, a
        // changed authoritative collection/catalog invalidates it, and a
        // corrupt disposable cache must be regenerated without data loss.
        var cacheFolder = Path.Combine(work, "persistent-set-progress");
        string state = new string('A', 64);
        var firstCached = NativeSetProgressCache.Physical(review, catalog,
            state, cacheFolder);
        Must(firstCached.Sets.Count == progress.Sets.Count &&
             File.Exists(Path.Combine(cacheFolder, "physical-summary.json")),
            "Initial verified summary should survive as an on-disk cache file.");
        File.WriteAllText(Path.Combine(cacheFolder, "physical-summary.json"), "{bad");
        var recovered = NativeSetProgressCache.Physical(review, catalog,
            state, cacheFolder);
        Must(recovered.CatalogPaperSlots == progress.CatalogPaperSlots,
            "A corrupt cache must rebuild from unchanged authoritative sources.");
        var arenaCached = NativeSetProgressCache.Arena(backup.ArenaCards,
            catalog, state, cacheFolder);
        Must(arenaCached.Sets.Count == arenaScope.Sets.Count &&
             File.Exists(Path.Combine(cacheFolder, "arena-summary.json")),
            "Arena card-name coverage needs an independent persisted summary.");
        var alternateIdentity = NativeSetProgressCache.Physical(review, catalog,
            new string('B', 64), cacheFolder);
        Must(alternateIdentity.ConfirmedOwnedCopies == progress.ConfirmedOwnedCopies,
            "A different backup identity should safely recompute ownership.");
        using (var db = new SqliteConnection(new SqliteConnectionStringBuilder
               { DataSource = artCatalog, Mode = SqliteOpenMode.ReadWrite, Pooling = false }.ToString()))
        {
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText =
                "UPDATE variants SET image='https://cards.scryfall.io/normal/front/1/1/changed.jpg' " +
                "WHERE setcode='ZXQ' AND collector='001'";
            cmd.ExecuteNonQuery();
        }
        var changedCatalog = NativeSetProgressCache.Physical(review,
            artCatalog, state, cacheFolder);
        Must(changedCatalog.Sets.Single(x => x.SetCode == "ZXQ").ArtworkUrl
                 .EndsWith("/changed.jpg", StringComparison.Ordinal),
            "Catalog changes must invalidate cached art and recalculate from source.");

        Must(sourceHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(backupFile))) &&
             catalogHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(catalog))) &&
             sourceTime == File.GetLastWriteTimeUtc(backupFile) &&
             catalogTime == File.GetLastWriteTimeUtc(catalog),
            "Native Set Progress changed backup/catalog bytes or timestamps.");

        Console.WriteLine("PASS: native Set Progress: 6 catalog paper slots, 3 owned, 8 verified copies, 9 unresolved, counted collector slots, exclude digital, chronological and numeric sorting, no source writes");
    }
}
