using ArcaneVault.Next.Collection;
using ArcaneVault.Next.Domain;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text.Json;

internal static class NativePhysicalValuationChecks
{
    private static void Must(bool ok, string reason)
    {
        if (!ok) throw new InvalidOperationException("Native physical valuation: " + reason);
    }

    private static string Hash(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    public static void Run(string work)
    {
        const string printA = "00000000-0000-4000-8000-000000000911";
        const string printB = "00000000-0000-4000-8000-000000000912";
        const string printC = "00000000-0000-4000-8000-000000000913";
        const string printD = "00000000-0000-4000-8000-000000000914";
        const string printE = "00000000-0000-4000-8000-000000000915";
        byte[] owned = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema = 2,
            cards = new[] { new { name = "Arena Name", qty = 4 } },
            physicalCards = new object[]
            {
                new { name = "Reprint", qty = 3, set = "TST", number = "1",
                    finish = "nonfoil", scryfallId = printA },
                new { name = "Reprint", qty = 2, set = "TST", number = "1",
                    finish = "foil", scryfallId = printA },
                new { name = "Reprint", qty = 1, set = "XYZ", number = "2",
                    finish = "nonfoil", scryfallId = printB },
                new { name = "Reprint", qty = 2, set = "XYZ", number = "2",
                    finish = "etched", scryfallId = printB },
                new { name = "Missing Finish", qty = 5, set = "TST", number = "3",
                    finish = "unspecified", scryfallId = printC },
                new { name = "Missing Quote", qty = 6, set = "TST", number = "4",
                    finish = "foil", scryfallId = printD },
                new { name = "Unresolved", qty = 7, set = "", number = "",
                    finish = "nonfoil", scryfallId = "" },
                new { name = "Digital Only", qty = 8, set = "TST", number = "5",
                    finish = "nonfoil", scryfallId = printE }
            },
            deckLibrary = new[] { new { id = "d1", name = "Keep My Deck",
                entries = Array.Empty<object>(), revisions = Array.Empty<object>() } },
            preferredArtwork = new { art = new { id = printA } },
            futureMeta = new { keep = "untouched" }
        });
        var backup = NativeBackupReader.Read(owned);
        string sourceBackup = Path.Combine(work, "valuation-owned-original.json");
        File.WriteAllBytes(sourceBackup, owned);
        string originalBackupHash = Hash(sourceBackup);
        string catalog = Path.Combine(work, "valuation-catalog.sqlite");
        using (var db = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = catalog, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false
        }.ToString()))
        {
            db.Open();
            using var command = db.CreateCommand();
            command.CommandText = """
                CREATE TABLE variants(
                    print_id TEXT PRIMARY KEY,name TEXT NOT NULL,setcode TEXT NOT NULL,
                    collector TEXT NOT NULL,games TEXT NOT NULL,digital INTEGER NOT NULL,
                    usd TEXT NOT NULL,usd_foil TEXT NOT NULL,usd_etched TEXT NOT NULL);
                CREATE TABLE state(key TEXT PRIMARY KEY,value TEXT NOT NULL);
                INSERT INTO state VALUES('bulk_updated_utc','2026-11-11T12:00:00Z');
                INSERT INTO variants VALUES
                ('00000000-0000-4000-8000-000000000911','Reprint','TST','1','paper',0,'2.10','6.25',''),
                ('00000000-0000-4000-8000-000000000912','Reprint','XYZ','2','paper',0,'25.00','','45.25'),
                ('00000000-0000-4000-8000-000000000913','Missing Finish','TST','3','paper',0,'12.00','',''),
                ('00000000-0000-4000-8000-000000000914','Missing Quote','TST','4','paper',0,'5.00','',''),
                ('00000000-0000-4000-8000-000000000915','Digital Only','TST','5','arena',1,'999999.99','','');
                """;
            command.ExecuteNonQuery();
        }
        string catalogHash = Hash(catalog);
        var review = CollectionCatalogReview.Examine(backup, catalog);
        Must(review.ConfirmedCopies == 19 && review.UnresolvedCopies == 15,
            "Paper verification must not guess unresolved/digital-only holdings.");
        var now = new DateTimeOffset(2026,11,11,20,0,0,TimeSpan.Zero);
        var result = NativePhysicalValuation.Calculate(review, catalog, now);
        Must(result.HasPriceColumns && !result.IsStale &&
             result.SnapshotUtc == new DateTimeOffset(2026,11,11,12,0,0,TimeSpan.Zero),
            "Should expose recent official bulk price snapshot timestamp.");
        // 3x2.10 + 2x6.25 + 1x25.00 + 2x45.25 = 134.30
        Must(result.PricedSubtotalUsd == 134.30m &&
             result.PricedCopies == 8 && result.MissingCopies == 26 &&
             result.PricedRows == 4 && result.MissingRows == 4,
            "Valuation must use each exact printing+finish and leave unknown copies out.");
        Must(result.Lines[0].UnitUsd == 2.10m &&
             result.Lines[1].UnitUsd == 6.25m &&
             result.Lines[2].UnitUsd == 25m &&
             result.Lines[3].UnitUsd == 45.25m &&
             result.Lines[0].LineLabel == "$6.30" &&
             result.Lines[7].UnitUsd is null &&
             result.Lines[7].PriceStatus.Contains("unverified", StringComparison.OrdinalIgnoreCase),
            "Per-row USD amounts must be exact decimals; digital cards are never priced as paper.");
        Must(!result.Summary.Contains("total collection value", StringComparison.OrdinalIgnoreCase) ||
             result.Summary.Contains("NOT your complete collection value"),
            "Partial valuation must never be marketed as a complete collection value.");
        Must(result.Lines[4].PriceStatus.Contains("Finish unspecified") &&
             result.Lines[5].PriceStatus.Contains("No valid foil price") &&
             result.Lines[6].PriceStatus.Contains("unverified"),
            "Unknown finish, missing finish quote, and unresolved printing must be distinct.");
        var old = NativePhysicalValuation.Calculate(review,catalog,
            new DateTimeOffset(2026,11,13,0,0,0,TimeSpan.Zero));
        Must(old.IsStale && old.SnapshotStatus.Contains("STALE"),
            "Bulk price snapshots older than 24 hours must always show a stale warning.");
        var future = NativePhysicalValuation.Calculate(review,catalog,
            new DateTimeOffset(2026,11,10,0,0,0,TimeSpan.Zero));
        Must(future.IsStale, "Future snapshot with mismatched clock must not claim fresh data.");

        string legacy = Path.Combine(work,"legacy-price-catalog.sqlite");
        using (var db = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource=legacy,Mode=SqliteOpenMode.ReadWriteCreate,Pooling=false
        }.ToString()))
        {
            db.Open();
            using var command=db.CreateCommand();
            command.CommandText="""
                CREATE TABLE variants(
                    print_id TEXT PRIMARY KEY,name TEXT,setcode TEXT,collector TEXT,
                    games TEXT,digital INTEGER);
                INSERT INTO variants VALUES
                ('00000000-0000-4000-8000-000000000911','Reprint','TST','1','paper',0);
                """;
            command.ExecuteNonQuery();
        }
        var legacyReview = CollectionCatalogReview.Examine(backup,legacy);
        var legacyValuation = NativePhysicalValuation.Calculate(legacyReview,legacy);
        Must(!legacyValuation.HasPriceColumns && legacyValuation.PricedCopies == 0 &&
             legacyValuation.PricedSubtotalUsd == 0 &&
             legacyValuation.MissingCopies == backup.PhysicalCopies &&
             legacyValuation.SnapshotStatus.Contains("Full Scryfall Sync"),
            "Old catalogs must fail closed with a sync instruction, not invent zero-dollar value.");
        var noCatalog = NativePhysicalValuation.Calculate(
            CollectionCatalogReview.Examine(backup,null),null);
        Must(noCatalog.MissingCopies == backup.PhysicalCopies &&
             noCatalog.PricedCopies == 0 &&
             noCatalog.Lines.All(x => !x.UnitUsd.HasValue),
            "No-catalog data must stay unpriced rather than joining arbitrary editions.");

        Must(Hash(catalog)==catalogHash && Hash(sourceBackup)==originalBackupHash,
            "Price reading and sorting must not modify catalog or owned-collection bytes.");
    }
}
