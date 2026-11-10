using ArcaneVault.Next.Collection;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text.Json;

internal static class NativeWorkspaceSafetyChecks
{
    private static void Must(bool ok, string why)
    {
        if (!ok) throw new InvalidOperationException("Native workspace refresh: " + why);
    }

    private static void Reject(Action action)
    {
        try { action(); }
        catch (InvalidDataException) { return; }
        throw new InvalidOperationException("Native refresh accepted a rejected operation.");
    }

    private static byte[] Backup(int physical, int arena, int revisions,
        string exportTime, string theme)
    {
        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema = 2,
            exported = exportTime,
            source = "installed-webview2-offline-snapshot",
            cards = new object[] { new { name = "Arena Card", qty = arena } },
            physicalCards = new object[] {
                new { name = "Split // Back", qty = physical,
                    set = "AAA", number = "010", finish = "foil",
                    scryfallId = "00000000-0000-4000-8000-000000000111",
                    location = "Binder 1" },
                new { name = "Unresolved", qty = 3,
                    set = "", number = "", finish = "nonfoil",
                    scryfallId = "", location = "Deck Box" }
            },
            deckLibrary = new object[] {
                new { name = "Commander Deck",
                    entries = new object[] { new { name = "Split // Back", qty = 1 }},
                    revisions = Enumerable.Range(0, revisions).Select(i => new { number = i }).ToArray()
                }
            },
            preferredArtwork = new { preferred = new { id = "cover", image = "art.png" } },
            extraSetting = new { theme, unknownFutureKey = 17 }
        });
    }

    public static void Run(string work)
    {
        string original = Path.Combine(work, "native-safe-refresh.sqlite");
        byte[] v1 = Backup(2, 4, 1, "2026-11-10T00:00:00Z", "red");
        byte[] v1NewTimestamp = Backup(2, 4, 1, "2026-11-10T02:00:00Z", "red");
        byte[] v2 = Backup(6, 5, 2, "2026-11-10T04:00:00Z", "black");

        var imported = NativeWorkspace.ImportNewBytes(v1, "original-legacy", original);
        var initialHash = SHA256.HashData(File.ReadAllBytes(original));
        var initialTime = File.GetLastWriteTimeUtc(original);

        var noChange = NativeWorkspaceSafety.Compare(original, v1NewTimestamp);
        Must(!noChange.HasChanges && noChange.PayloadEquivalent &&
             noChange.InventoryEqual && noChange.OtherCountsEqual,
            "A new export timestamp should not trigger data replacement.");
        Reject(() => NativeWorkspaceSafety.Refresh(original, v1NewTimestamp,
            "new-export-time-only", imported.BackupSha256));
        Must(NativeWorkspaceSafety.RecoveryPaths(original).Count == 0,
            "No-op refresh created an unnecessary backup.");

        var changed = NativeWorkspaceSafety.Compare(original, v2);
        Must(changed.HasChanges && !changed.InventoryEqual &&
             !changed.OtherCountsEqual &&
             changed.CurrentPhysicalCopies == 5 && changed.CandidatePhysicalCopies == 9 &&
             changed.CurrentArenaCopies == 4 && changed.CandidateArenaCopies == 5 &&
             changed.CurrentRevisions == 1 && changed.CandidateRevisions == 2,
             "Refresh diff did not distinguish source/deck/inventory changes.");

        Reject(() => NativeWorkspaceSafety.Refresh(original, v2,
            "changed-legacy", "WRONG-SHA"));
        Must(initialHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(original))) &&
             initialTime == File.GetLastWriteTimeUtc(original),
             "Stale refresh changed the native workspace.");

        byte[] invalid = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema = 2, cards = Array.Empty<object>(),
            physicalCards = new object[] { new { name = "Invalid", qty = -2 }},
            deckLibrary = Array.Empty<object>(), preferredArtwork = new { }
        });
        Reject(() => NativeWorkspaceSafety.Refresh(original, invalid,
            "invalid-source", imported.BackupSha256));
        Must(NativeWorkspaceSafety.RecoveryPaths(original).Count == 0,
            "Invalid source generated a backup or replaced native data.");

        var result = NativeWorkspaceSafety.Refresh(original, v2,
            "changed-legacy", imported.BackupSha256);
        var live = NativeWorkspace.Open(original);
        var rollback = NativeWorkspace.Open(result.RecoveryPath);
        Must(live.BackupSha256 == result.Current.BackupSha256 &&
             live.Collection.PhysicalCopies == 9 &&
             live.Collection.ArenaCopies == 5 &&
             live.Collection.DeckRevisions == 2,
             "Refreshed native workspace did not preserve the new source.");
        Must(rollback.BackupSha256 == imported.BackupSha256 &&
             rollback.Collection.PhysicalCopies == 5 &&
             rollback.Collection.ArenaCopies == 4 &&
             rollback.Collection.DeckRevisions == 1,
             "Automatic recovery point did not retain the complete old native collection.");
        Must(NativeWorkspaceSafety.RecoveryPaths(original).Count == 1 &&
             File.Exists(result.RecoveryPath),
             "Verified refresh did not publish exactly one recovery point.");
        Reject(() => NativeWorkspaceSafety.Refresh(original, v1,
            "stale-refresh", imported.BackupSha256));

        string unrelated = Path.Combine(work, "arbitrary.sqlite");
        File.Copy(result.RecoveryPath, unrelated);
        Reject(() => NativeWorkspaceSafety.Restore(original, unrelated,
            live.BackupSha256));

        // A recovery-looking file with damaged typed rows must not be accepted.
        string damaged = Path.Combine(work, "native-safe-refresh.rollback-corrupt.sqlite");
        File.Copy(result.RecoveryPath, damaged);
        using (var db = new SqliteConnection(new SqliteConnectionStringBuilder {
            DataSource = damaged, Mode = SqliteOpenMode.ReadWrite, Pooling = false
        }.ToString()))
        {
            db.Open();
            using var sql = db.CreateCommand();
            sql.CommandText = "UPDATE physical_lines SET qty=87 WHERE ordinal=0";
            sql.ExecuteNonQuery();
        }
        Reject(() => NativeWorkspaceSafety.Restore(original, damaged,
            live.BackupSha256));
        Must(NativeWorkspace.Open(original).BackupSha256 == live.BackupSha256,
            "Corrupt recovery changed the current collection.");

        File.Delete(damaged);
        var restored = NativeWorkspaceSafety.Restore(original,
            result.RecoveryPath, live.BackupSha256);
        Must(restored.Current.BackupSha256 == imported.BackupSha256 &&
             restored.Current.Collection.PhysicalCopies == 5 &&
             NativeWorkspace.Open(original).Collection.ArenaCopies == 4,
             "Native recovery restore did not reinstate the original inventories.");
        Must(File.Exists(result.RecoveryPath) &&
             File.Exists(restored.RecoveryPath) &&
             NativeWorkspace.Open(restored.RecoveryPath).BackupSha256 == live.BackupSha256,
             "Restoring an old version destroyed the previous/newer recovery point.");
        Reject(() => NativeWorkspaceSafety.Restore(original,
            result.RecoveryPath, imported.BackupSha256));
        Must(NativeWorkspaceSafety.RecoveryPaths(original).Count == 2,
            "Restore/re-refresh lost recovery history.");

        Console.WriteLine("PASS: native refresh compares real changes, ignores timestamps, rejects stale/corrupt sources, atomically swaps with rollback, restores previous data without losing newer versions");
    }
}
