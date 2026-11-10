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
            cards = new[] { new { name = "Arena Separate", qty = 4 } },
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

        Must(sourceHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(backupFile))) &&
             catalogHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(catalog))) &&
             sourceTime == File.GetLastWriteTimeUtc(backupFile) &&
             catalogTime == File.GetLastWriteTimeUtc(catalog),
            "Native Set Progress changed backup/catalog bytes or timestamps.");

        Console.WriteLine("PASS: native Set Progress: 6 catalog paper slots, 3 owned, 8 verified copies, 9 unresolved, counted collector slots, exclude digital, chronological and numeric sorting, no source writes");
    }
}
