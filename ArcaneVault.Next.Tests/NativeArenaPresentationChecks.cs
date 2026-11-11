using ArcaneVault.Next.Desktop;
using ArcaneVault.Next.Domain;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text.Json;

internal static class NativeArenaPresentationChecks
{
    private static void Must(bool condition, string text)
    {
        if (!condition) throw new InvalidOperationException("Arena presentation: " + text);
    }

    public static void Run(string work)
    {
        string path = Path.Combine(work, "arena-single-name.sqlite");
        using (var db = new SqliteConnection(new SqliteConnectionStringBuilder
            { DataSource = path, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false }.ToString()))
        {
            db.Open();
            using var c = db.CreateCommand();
            c.CommandText = """
                CREATE TABLE cards(name TEXT PRIMARY KEY COLLATE NOCASE,
                    type TEXT NOT NULL,colors TEXT NOT NULL,oracle TEXT NOT NULL);
                CREATE TABLE variants(print_id TEXT PRIMARY KEY,
                    name TEXT NOT NULL COLLATE NOCASE,setcode TEXT NOT NULL,
                    collector TEXT NOT NULL,rarity TEXT NOT NULL,arena INTEGER NOT NULL,
                    games TEXT NOT NULL,digital INTEGER NOT NULL,image TEXT NOT NULL);
                INSERT INTO cards VALUES ('A Good Day to Die','Instant','B','Target creature gets +2/+0');
                INSERT INTO variants VALUES
                    ('1','A Good Day to Die','trk','111','common',1,'paper,arena',0,'https://cards.scryfall.io/base.jpg'),
                    ('2','A Good Day to Die','trk','428','common',1,'paper,arena',0,'https://cards.scryfall.io/alt.jpg'),
                    ('3','A Good Day to Die','trk','438','common',1,'paper,arena',0,'https://cards.scryfall.io/showcase.jpg');
                """;
            c.ExecuteNonQuery();
        }
        string original = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        byte[] backup = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema = 2,
            cards = new[] { new { name = "A Good Day to Die", qty = 1 } },
            physicalCards = Array.Empty<object>(),
            deckLibrary = Array.Empty<object>(),
            preferredArtwork = new Dictionary<string, object>()
        });
        var ownership = NativeCatalogOwnership.From(NativeBackupReader.Read(backup));
        using var catalog = CatalogSnapshot.Open(path);
        var owned = catalog.SearchFiltered("", new(
            Ownership: CatalogSnapshot.OwnershipMode.ArenaOwned), ownership: ownership);
        Must(owned.Rows.Count == 1 && owned.Rows[0].CollectorNumber == "111",
            "The three physical catalog versions must collapse to the base Arena card.");
        Must(owned.Rows[0].ArenaOwned == 1 && owned.Rows[0].OwnershipLabel == "" &&
             owned.Rows[0].ArenaPresentation &&
             owned.Rows[0].DisplaySetCode == "ARENA CARD" &&
             owned.Rows[0].DisplayCollectorNumber == "",
            "Arena ownership count must remain correct without numbered badges.");
        var available = catalog.SearchFiltered("A Good", new(ArenaOnly: true),
            ownership: ownership);
        Must(available.Rows.Count == 1 &&
             available.Rows[0].ImageUrl.EndsWith("/base.jpg", StringComparison.Ordinal),
             "On Arena search must show the base artwork rather than showcase copies.");
        Must(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) == original,
            "Catalog data must remain unmodified by Arena filtering.");
    }
}
