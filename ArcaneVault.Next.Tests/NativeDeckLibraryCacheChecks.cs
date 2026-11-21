using ArcaneVault.Next.Collection;
using System.Security.Cryptography;
using System.Text.Json;

internal static class NativeDeckLibraryCacheChecks
{
    private static void Must(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(
            "Deck Library summary cache: " + message);
    }

    private static byte[] Backup(string label) => JsonSerializer.SerializeToUtf8Bytes(new
    {
        schema = 2,
        cards = Array.Empty<object>(),
        physicalCards = Array.Empty<object>(),
        preferredArtwork = new { },
        deckLibrary = new[] {
            new {
                id = "deck-id-" + label,
                name = "Deck " + label,
                commander = "Atraxa, Praetors' Voice",
                format = "Commander",
                folder = "Commander",
                entries = new[] { new { name = "Sol Ring", qty = 1 } },
                revisions = new[] { new { revision = 1 } }
            }
        },
        futureSettings = new { mode = label }
    });

    public static void Run(string work)
    {
        string cache = Path.Combine(work, "deck-library-cache-regression");
        byte[] original = Backup("A");
        string sha = Convert.ToHexString(SHA256.HashData(original));
        int reads = 0;
        byte[] ReadOriginal() { reads++; return original; }

        var first = NativeDeckLibraryCache.List(sha, ReadOriginal, cache);
        Must(reads == 1 && first.Count == 1 && first[0].Name == "Deck A" &&
             first[0].Commander == "Atraxa, Praetors' Voice" &&
             first[0].MainboardText == "1 Sol Ring" &&
             first[0].CoverArtworkUrl == "",
             "First read should rebuild only presentation data from saved deck backup.");
        var cached = NativeDeckLibraryCache.List(sha, () =>
            throw new InvalidOperationException("Cache hit re-parsed the backup."), cache);
        Must(cached.Count == 1 && cached[0].Name == "Deck A",
             "An unchanged verified SHA must use the cached display summaries.");
        Must(cached[0].BracketMinimum == 1 &&
             cached[0].BracketLabel.Contains("Provisional"),
             "Existing Commander decks must show a provisional minimum.");
        // Reproduce the v0.9.180 bug: a schema-1 cache carried the old
        // DeckRow without any bracket fields even when Format was Commander.
        string stalePath = Path.Combine(cache, "deck-list.json");
        File.WriteAllBytes(stalePath, JsonSerializer.SerializeToUtf8Bytes(new {
            Schema = 1, BackupSha = sha,
            Rows = new[] { new { Ordinal = 0, Id = "deck-id-A", Name = "Deck A",
                Commander = "Atraxa, Praetors' Voice", Format = "Commander" } }
        }));
        var rebuilt = NativeDeckLibraryCache.List(sha, ReadOriginal, cache);
        Must(reads == 2 && rebuilt.Single().BracketMinimum == 1,
             "A stale cache missing bracket metadata must be discarded.");


        byte[] updated = Backup("B");
        string sha2 = Convert.ToHexString(SHA256.HashData(updated));
        var refreshed = NativeDeckLibraryCache.List(sha2, () => updated, cache);
        Must(refreshed.Count == 1 && refreshed[0].Name == "Deck B",
             "Changing authoritative backup must invalidate cached deck rows.");

        string path = Path.Combine(cache, "deck-list.json");
        File.WriteAllText(path, "{broken cache");
        var recovered = NativeDeckLibraryCache.List(sha2, () => updated, cache);
        Must(recovered.Single().Name == "Deck B",
             "Malformed cache must rebuild from verified deck data.");

        bool rejected = false;
        try { NativeDeckLibraryCache.List("not-a-hash", () => original, cache); }
        catch (InvalidDataException) { rejected = true; }
        Must(rejected, "Unverified collection identities must not select a cache.");
        Must(SHA256.HashData(original).SequenceEqual(Convert.FromHexString(sha)) &&
             SHA256.HashData(updated).SequenceEqual(Convert.FromHexString(sha2)) &&
             !Directory.GetFiles(cache, "*.tmp").Any(),
             "Cache reads/writes must not change source collection or leave staged files.");
        Console.WriteLine("PASS: Deck Library summary cache (reuse, SHA invalidation, recovery)");
    }
}
