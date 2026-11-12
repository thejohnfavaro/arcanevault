using ArcaneVault.Next.Collection;
using ArcaneVault.Next.Domain;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

internal static class NativeLargeCollectionChecks
{
    private static void Must(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException(
            "Large native collection regression: " + reason);
    }

    /// <summary>
    /// Synthetic scale-and-integrity canary for normal CI, not a timed
    /// benchmark or a real user's collection. No writes to app data folders.
    /// </summary>
    public static void Run(string work)
    {
        const int physicalRows = 4_000;
        const int arenaRows = 4_500;
        const int deckRows = 64;

        var physical = Enumerable.Range(0, physicalRows).Select(i => new
        {
            name = $"Physical Card {i % 1100:D4}",
            qty = i % 3 + 1,
            set = $"S{i % 37:D2}",
            number = (i + 1).ToString(),
            finish = i % 5 == 0 ? "foil" : "nonfoil",
            scryfallId = $"00000000-0000-4000-8000-{i + 1:x12}",
            extraPrintingNote = "synthetic-only"
        }).ToArray();

        var arena = Enumerable.Range(0, arenaRows).Select(i => new
        {
            name = $"Arena Card {i % 1300:D4}",
            qty = i % 5,
            extraArenaField = "preserved"
        }).ToArray();

        var decks = Enumerable.Range(0, deckRows).Select(i => new
        {
            name = $"Deck #{i:D3}",
            folder = i % 2 == 0 ? "Commander" : "Brawl",
            favorite = i % 7 == 0,
            entries = new[] {
                new { name = $"Physical Card {i % 1100:D4}", qty = 1 },
                new { name = $"Arena Card {i % 1300:D4}", qty = 2 }
            },
            revisions = new[] {
                new { sequence = 1, synthetic = true },
                new { sequence = 2, synthetic = true }
            }
        }).ToArray();

        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema = 2,
            exported = "2026-11-11T00:00:00Z",
            cards = arena,
            physicalCards = physical,
            deckLibrary = decks,
            preferredArtwork = new { },
            futureExtension = new { unknownMetadata = "must survive byte-identical" }
        });
        byte[] sourceDigest = SHA256.HashData(bytes);

        var timer = Stopwatch.StartNew();
        var view = NativeBackupReader.Read(bytes);
        TimeSpan parse = timer.Elapsed;
        Must(view.PhysicalCards.Count == physicalRows &&
             view.ArenaCards.Count == arenaRows &&
             view.Decks == deckRows && view.DeckRevisions == deckRows * 2,
             "Large source inventory and deck counts changed.");
        Must(view.PhysicalCopies == physical.Sum(x => (long)x.qty) &&
             view.ArenaCopies == arena.Sum(x => (long)x.qty) &&
             view.CopiesWithUnverifiedIds == view.PhysicalCopies &&
             view.CopiesWithMissingOrInvalidIds == 0,
             "Copy totals and synthetic printing IDs were not preserved.");

        string workspacePath = Path.Combine(work, "large-native-regression.sqlite");
        timer.Restart();
        var saved = NativeWorkspace.ImportNewBytes(
            bytes, "large-regression-fixture.json", workspacePath);
        TimeSpan import = timer.Elapsed;
        timer.Restart();
        var opened = NativeWorkspace.Open(workspacePath);
        TimeSpan reopen = timer.Elapsed;
        Must(saved.BackupSha256 == Convert.ToHexString(sourceDigest) &&
             opened.BackupSha256 == saved.BackupSha256 &&
             opened.Collection.PhysicalCards.SequenceEqual(view.PhysicalCards) &&
             opened.Collection.ArenaCards.SequenceEqual(view.ArenaCards) &&
             opened.Collection.Decks == view.Decks &&
             opened.Collection.DeckRevisions == view.DeckRevisions,
             "Reopening the imported workspace changed data or its backup hash.");

        // Search and deck accounting remain separated across Physical and Arena.
        var physicalSearch = NativeDeckOwnedPicker.Search(view,
            NativeDeckOwnedPicker.Pool.Physical, "Physical Card 0001");
        var arenaSearch = NativeDeckOwnedPicker.Search(view,
            NativeDeckOwnedPicker.Pool.Arena, "Physical Card 0001");
        Must(physicalSearch.Count == 1 && physicalSearch[0].Owned > 1 &&
             arenaSearch.Count == 0,
             "Large inventory search mixed physical and Arena counts.");
        var physicalDraft = NativeDeckOwnedPicker.Review(view,
            NativeDeckOwnedPicker.Pool.Physical,
            "2 Physical Card 0001\n1 Missing Test Card", "", "60-card");
        var arenaDraft = NativeDeckOwnedPicker.Review(view,
            NativeDeckOwnedPicker.Pool.Arena,
            "2 Physical Card 0001\n1 Missing Test Card", "", "Arena Brawl");
        Must(physicalDraft.MissingCopies == 1 &&
             arenaDraft.MissingCopies == 3,
             "Review must conserve per-pool ownership on large collections.");

        // A full backup export is required to reproduce every unknown field,
        // deck revision, printing and Arena count without any migration.
        string export = Path.Combine(work, "large-native-roundtrip.json");
        timer.Restart();
        var exported = NativeWorkspaceSafety.ExportFullBackup(
            workspacePath, export, opened.BackupSha256);
        TimeSpan backup = timer.Elapsed;
        Must(exported.Bytes == bytes.LongLength &&
             exported.BackupSha256 == opened.BackupSha256 &&
             SHA256.HashData(File.ReadAllBytes(export)).SequenceEqual(sourceDigest) &&
             SHA256.HashData(bytes).SequenceEqual(sourceDigest),
             "Full native backup must reproduce the original schema-2 bytes exactly.");
        Must(NativeWorkspace.Open(workspacePath).BackupSha256 == opened.BackupSha256,
             "Read-only searches and export must leave the live workspace unchanged.");

        Console.WriteLine($"PASS: large synthetic native collection " +
            $"({physicalRows:N0} physical rows, {arenaRows:N0} Arena rows, " +
            $"{deckRows:N0} decks; parse {parse.TotalMilliseconds:F0} ms, " +
            $"import {import.TotalMilliseconds:F0} ms, " +
            $"reopen {reopen.TotalMilliseconds:F0} ms, " +
            $"export {backup.TotalMilliseconds:F0} ms; bytes unchanged)");
    }
}
