using ArcaneVault.Next.Collection;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;

internal static class NativeDeckCoverCacheChecks
{
    private static void Must(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException("Native deck cover cache: " + message);
    }

    public static void Run(string work)
    {
        string catalog = Path.Combine(work, "deck-cover-catalog.sqlite");
        string cache = Path.Combine(work, "deck-cover-cache");
        using (var db = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = catalog, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false
        }.ToString()))
        {
            db.Open();
            using var command = db.CreateCommand();
            command.CommandText = """
                CREATE TABLE cards (
                    name TEXT PRIMARY KEY COLLATE NOCASE, image TEXT NOT NULL);
                INSERT INTO cards(name,image) VALUES
                  ('Atraxa, Praetors'' Voice',
                   'https://cards.scryfall.io/normal/front/a/1/a1111111.jpg'),
                  ('Unsafe Commander','https://example.invalid/card.jpg'),
                  ('No Art','');
                """;
            command.ExecuteNonQuery();
        }
        string sha = new string('A', 64);
        byte[] sourceHash = SHA256.HashData(File.ReadAllBytes(catalog));
        string[] names = { "atraxa, praetors' voice", "Unsafe Commander",
            "No Art", "Unknown Commander" };
        var initial = NativeDeckCoverCache.Get(names, catalog, sha, cache);
        Must(initial.Count == 4 &&
             initial["Atraxa, Praetors' Voice"].StartsWith(
                 "https://cards.scryfall.io/", StringComparison.Ordinal) &&
             initial["Unsafe Commander"] == "" &&
             initial["No Art"] == "" && initial["Unknown Commander"] == "",
             "Missing and nonofficial images must fall back to branded deck covers.");
        var cached = NativeDeckCoverCache.Get(names, catalog, sha, cache);
        Must(cached.SequenceEqual(initial) ||
             cached.All(x => initial.TryGetValue(x.Key, out var v) && v == x.Value),
             "Cache hit must return exactly the verified cover map.");
        Must(sourceHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(catalog))),
             "Cache access unexpectedly changed the official Scryfall catalog.");

        string stored = Path.Combine(cache, "commander-covers.json");
        Must(File.Exists(stored), "Local metadata cache was not created.");
        File.WriteAllText(stored, "not valid JSON");
        var rebuilt = NativeDeckCoverCache.Get(names, catalog, sha, cache);
        Must(rebuilt["Atraxa, Praetors' Voice"] == initial["atraxa, praetors' voice"],
             "A corrupt optional cache must rebuild safely from catalog.");

        // Catalog stamp includes file size and last-modified timestamp. A new
        // catalog must not keep obsolete cover metadata from earlier syncs.
        using (var db = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = catalog, Mode = SqliteOpenMode.ReadWrite, Pooling = false
        }.ToString()))
        {
            db.Open();
            using var command = db.CreateCommand();
            command.CommandText = """
                UPDATE cards SET image =
                    'https://cards.scryfall.io/normal/front/b/2/b2222222.jpg'
                WHERE name = 'Atraxa, Praetors'' Voice';
                """;
            command.ExecuteNonQuery();
        }
        File.SetLastWriteTimeUtc(catalog, DateTime.UtcNow.AddMinutes(2));
        var updated = NativeDeckCoverCache.Get(names, catalog, sha, cache);
        Must(updated["Atraxa, Praetors' Voice"].Contains("b2222222",
                 StringComparison.Ordinal),
             "Catalog changes must invalidate saved commander cover URLs.");
        var refreshedCollection = NativeDeckCoverCache.Get(
            names, catalog, new string('B', 64), cache);
        Must(refreshedCollection.Count == updated.Count &&
             refreshedCollection["Atraxa, Praetors' Voice"] ==
                 updated["Atraxa, Praetors' Voice"],
             "A changed collection SHA must rebuild the cover cache without inventing new art.");
        Must(!Directory.GetFiles(cache, "*.tmp", SearchOption.AllDirectories).Any(),
             "No staging files should remain after cache writes.");
        Console.WriteLine("PASS: native deck cover cache (safe URLs, invalidation, fallback)");
    }
}
