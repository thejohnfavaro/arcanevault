using ArcaneVault.Next.Collection;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;

internal static class NativeFormatLegalityChecks
{
    private static void Must(bool pass, string message)
    {
        if (!pass) throw new InvalidOperationException("Native format legality: " + message);
    }

    private static string FileHash(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    public static void Run(string work)
    {
        const string json = """
            {
              "standard":"not_legal", "future":"not_legal", "historic":"legal",
              "timeless":"restricted", "commander":"legal", "modern":"banned",
              "pauper":"not_legal", "standardbrawl":"legal",
              "paupercommander":"legal", "oathbreaker":"banned"
            }
            """;
        var view = NativeFormatLegality.Parse(json);
        Must(view.Available && view.Rows.Count == 10,
            "Valid Scryfall format object should produce a row per status.");
        Must(view.Rows[0].Name == "Standard" &&
             view.Rows[0].StatusText == "Not legal" &&
             view.Rows[1].Name == "Modern" &&
             view.Rows[1].StatusText == "Banned",
            "Format order and not_legal vs banned display labels must be correct.");
        Must(view.Rows.Single(r => r.Code == "future").Name == "Future Standard",
            "Scryfall future format must receive a human-readable label.");
        Must(view.Rows.Single(r => r.Code == "standardbrawl").Name == "Standard Brawl" &&
             view.Rows.Single(r => r.Code == "paupercommander").Name == "Pauper Commander",
            "Multiword formats should be displayed as readable names.");
        Must(view.Rows.Single(r => r.Code == "timeless").StatusText == "Restricted" &&
             view.Rows.Single(r => r.Code == "historic").StatusText == "Legal",
            "Legal, restricted and banned states must not be conflated.");
        Must(view.LegalCount == 4 && view.BannedCount == 2 &&
             view.RestrictedCount == 1 &&
             view.Summary.Contains("4 legal") &&
             view.Summary.Contains("2 banned"),
            "Legality summary must count meaningful status categories.");

        Must(!NativeFormatLegality.Parse(null).Available &&
             !NativeFormatLegality.Parse("").Available &&
             !NativeFormatLegality.Parse("{}").Available &&
             !NativeFormatLegality.Parse("not json").Available &&
             !NativeFormatLegality.Parse("[]").Available,
            "Missing or invalid legality payloads must not leak raw JSON or claim legality.");
        var unexpected = NativeFormatLegality.Parse(
            """{"commander":"legal","mystery_format":"new_state"}""");
        Must(unexpected.Available &&
             unexpected.Rows.Single(x => x.Code == "mystery_format").Name == "Mystery Format" &&
             unexpected.Rows.Single(x => x.Code == "mystery_format").StatusCode == "unknown" &&
             unexpected.Rows.Single(x => x.Code == "mystery_format").StatusText == "Unknown",
            "Unexpected format/status keys must be friendly but never treated as legal.");
        Must(!NativeFormatLegality.Parse(
                "{" + new string('a', 65_000) + "}").Available,
            "Oversized legality metadata must fail gracefully.");

        string catalog = Path.Combine(work, "legalities-format-catalog.sqlite");
        using (var db = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = catalog, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false
        }.ToString()))
        {
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE cards(name TEXT, type TEXT, oracle TEXT, rarity TEXT,
                    mv REAL, colors TEXT, legalities TEXT, setcode TEXT, image TEXT);
                INSERT INTO cards VALUES
                    ($name,'Artifact','Tap: Add one colorless mana.','uncommon',1,
                     '',$legalities,'tst','');
                """;
            cmd.Parameters.AddWithValue("$name", "Sample Relic");
            cmd.Parameters.AddWithValue("$legalities", json);
            cmd.ExecuteNonQuery();
        }
        string beforeHash = FileHash(catalog);
        var details = CollectionCatalogReview.ReadDetails(catalog, "Sample Relic");
        Must(details is not null && details.Legalities == json &&
             NativeFormatLegality.Parse(details.Legalities).LegalCount == 4,
            "Card Details must preserve original Scryfall JSON while formatting it.");
        Must(FileHash(catalog) == beforeHash,
            "Read-only display formatting must never modify the native Scryfall catalog.");
    }
}
