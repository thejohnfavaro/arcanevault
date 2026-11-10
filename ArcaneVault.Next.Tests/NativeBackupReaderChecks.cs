using ArcaneVault.Next.Domain;
using System.Security.Cryptography;
using System.Text.Json;

internal static class NativeBackupReaderChecks
{
    private static void Check(bool success, string reason)
    {
        if (!success) throw new InvalidOperationException("Native backup viewer: " + reason);
    }

    private static void MustReject(Action work)
    {
        try { work(); }
        catch (InvalidDataException) { return; }
        throw new InvalidOperationException("Native backup viewer accepted an invalid export.");
    }

    public static void Run(string directory)
    {
        string backup = Path.Combine(directory, "native-collection-full-export.json");
        const string validPrintId = "00000000-0000-4000-8000-000000000111";
        var fixture = new
        {
            schema = 2,
            cards = new object[]
            {
                new { name = "Test Arena Card", qty = 3 },
                new { name = "Another Arena Card", qty = 0 }
            },
            physicalCards = new object[]
            {
                new { name = "Split // Example", qty = 2, set = "AAA", number = "100",
                    finish = "foil", scryfallId = validPrintId },
                new { name = "Split // Example", qty = 3, set = "BBB", number = "10",
                    finish = "nonfoil", scryfallId = "" },
                new { name = "Other Name", qty = 1, set = "", number = "",
                    finish = "etched", scryfallId = "invalid-uuid" }
            },
            deckLibrary = new object[]
            {
                new { name = "First Deck", entries = new object[] { },
                    revisions = new object[] { new { name = "Previous" } } }
            },
            preferredArtwork = new { first = new { url = "https://example.invalid/image" } },
            specialPreference = new { theme = "dark", wildcard = 2 }
        };
        File.WriteAllText(backup, JsonSerializer.Serialize(fixture));
        var hash = SHA256.HashData(File.ReadAllBytes(backup));
        var beforeModified = File.GetLastWriteTimeUtc(backup);

        var report = NativeBackupReader.Read(backup);
        Check(report.PhysicalCards.Count == 3 && report.PhysicalCopies == 6 &&
              report.DistinctPhysicalNames == 2, "Physical rows and separate printings were lost.");
        Check(report.ArenaCards.Count == 2 && report.ArenaCopies == 3,
            "Arena inventory must not inherit paper quantities.");
        Check(report.Decks == 1 && report.DeckRevisions == 1 &&
              report.PreferredArtworks == 1, "Deck revisions/art metadata counts incorrect.");
        Check(report.CopiesWithUnverifiedIds == 2 &&
              report.CopiesWithMissingOrInvalidIds == 4,
            "Missing IDs must not be silently confirmed or reassigned.");
        Check(report.PhysicalCards[0].HasValidPrintId &&
              !report.PhysicalCards[1].HasValidPrintId &&
              !report.PhysicalCards[2].HasValidPrintId,
            "ID syntax check must not guess printing identity.");
        Check(report.PhysicalCards[0].PrintingStatus.Contains("not catalog verified"),
            "A supplied ID cannot be claimed catalog verified.");
        Check(report.PhysicalCards[0].SetAndNumber == "AAA #100" &&
              report.PhysicalCards[1].SetAndNumber == "BBB #10",
            "Printings were silently combined.");
        Check(hash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(backup))) &&
              beforeModified == File.GetLastWriteTimeUtc(backup),
            "Read-only native viewer modified its export.");

        string bad = Path.Combine(directory, "invalid-native-collection.json");
        File.WriteAllText(bad, "{\"schema\":2,\"cards\":[],\"physicalCards\":[{\"name\":\"Card\",\"qty\":-1}],\"deckLibrary\":[],\"preferredArtwork\":{}}");
        MustReject(() => NativeBackupReader.Read(bad));
        File.WriteAllText(bad, "{\"schema\":2,\"cards\":[],\"physicalCards\":[{\"name\":\"Card\",\"qty\":2.5}],\"deckLibrary\":[],\"preferredArtwork\":{}}");
        MustReject(() => NativeBackupReader.Read(bad));
        File.WriteAllText(bad, "{\"schema\":2,\"cards\":[],\"physicalCards\":[],\"deckLibrary\":[]}");
        MustReject(() => NativeBackupReader.Read(bad));
        File.WriteAllText(bad, "{\"schema\":2,\"cards\":[],\"physicalCards\":[],\"deckLibrary\":[],\"preferredArtwork\":[]}");
        MustReject(() => NativeBackupReader.Read(bad));
        File.WriteAllText(bad, "{\"format\":\"arcane-vault-collection-sync\",\"schema\":1,\"arenaCards\":[],\"physicalCards\":[]}");
        MustReject(() => NativeBackupReader.Read(bad));
        File.WriteAllText(bad, "{\"schema\":99,\"cards\":[],\"physicalCards\":[],\"deckLibrary\":[],\"preferredArtwork\":{}}");
        MustReject(() => NativeBackupReader.Read(bad));
        Check(hash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(backup))),
            "Rejected backups changed original data.");
        Console.WriteLine("PASS: Windows-first native collection viewer preserves full backup physical/Arena copies, print distinctions, deck/art totals; rejects incomplete snapshots");
    }
}
