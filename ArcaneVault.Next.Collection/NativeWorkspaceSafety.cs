using ArcaneVault.Next.Domain;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ArcaneVault.Next.Collection;

/// <summary>
/// Atomic update and recovery for the private native SQLite workspace.
/// Deliberately does not touch the legacy Arcane Vault profile, catalog,
/// localStorage, deck files, or any existing recovery point.
/// </summary>
public static class NativeWorkspaceSafety
{
    private static readonly object UpdateGate = new();

    public sealed record Diff(
        string CurrentBackupSha256,
        string CandidateBackupSha256,
        bool InventoryEqual,
        bool OtherCountsEqual,
        bool PayloadEquivalent,
        long CurrentPhysicalCopies,
        long CandidatePhysicalCopies,
        int CurrentPhysicalRows,
        int CandidatePhysicalRows,
        long CurrentArenaCopies,
        long CandidateArenaCopies,
        int CurrentArenaRows,
        int CandidateArenaRows,
        int CurrentDecks,
        int CandidateDecks,
        int CurrentRevisions,
        int CandidateRevisions,
        int CurrentArtworks,
        int CandidateArtworks,
        long CurrentUnresolvedCopies,
        long CandidateUnresolvedCopies)
    {
        public bool HasChanges => !PayloadEquivalent;
    }

    public sealed record Replacement(
        NativeWorkspace.Workspace Current,
        string RecoveryPath,
        string RecoveryBackupSha256);

    private static string Digest(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes));

    // A direct legacy source is converted afresh each time: the "exported"
    // clock changes even if the actual user data has not. Exclude only known
    // provenance fields, not any ownership, decks, art or opaque preferences.
    private static string PayloadDigest(byte[] bytes)
    {
        using var parsed = JsonDocument.Parse(bytes);
        using var result = new MemoryStream();
        using (var writer = new Utf8JsonWriter(result))
        {
            writer.WriteStartObject();
            foreach (var prop in parsed.RootElement.EnumerateObject())
            {
                if (prop.Name is "exported" or "source") continue;
                prop.WriteTo(writer);
            }
            writer.WriteEndObject();
        }
        return Digest(result.ToArray());
    }

    private static Diff Summarize(NativeWorkspace.Workspace current,
        NativeBackupView candidate, string proposedDigest,
        string semanticCurrent, string semanticCandidate)
    {
        var prev = current.Collection;
        bool inventoryEqual =
            prev.PhysicalCards.SequenceEqual(candidate.PhysicalCards) &&
            prev.ArenaCards.SequenceEqual(candidate.ArenaCards);
        bool countsEqual = prev.Decks == candidate.Decks &&
                           prev.DeckRevisions == candidate.DeckRevisions &&
                           prev.PreferredArtworks == candidate.PreferredArtworks;
        return new Diff(current.BackupSha256, proposedDigest,
            inventoryEqual, countsEqual,
            string.Equals(semanticCurrent, semanticCandidate, StringComparison.Ordinal),
            prev.PhysicalCopies, candidate.PhysicalCopies,
            prev.PhysicalCards.Count, candidate.PhysicalCards.Count,
            prev.ArenaCopies, candidate.ArenaCopies,
            prev.ArenaCards.Count, candidate.ArenaCards.Count,
            prev.Decks, candidate.Decks,
            prev.DeckRevisions, candidate.DeckRevisions,
            prev.PreferredArtworks, candidate.PreferredArtworks,
            prev.CopiesWithMissingOrInvalidIds,
            candidate.CopiesWithMissingOrInvalidIds);
    }

    public static Diff Compare(string nativePath, byte[] incoming)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        var current = NativeWorkspace.Open(nativePath);
        var proposed = NativeBackupReader.Read(incoming);
        return Summarize(current, proposed, Digest(incoming),
            PayloadDigest(ReadPreservedBackup(nativePath)), PayloadDigest(incoming));
    }

    private static byte[] ReadPreservedBackup(string nativePath)
    {
        // The complete source backup is preserved in the manifest. All
        // structure, ownership and deck/art rows were verified by Open.
        using var db = new Microsoft.Data.Sqlite.SqliteConnection(
            new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
            {
                DataSource = nativePath,
                Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadOnly,
                Pooling = false
            }.ToString());
        db.Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT backup FROM manifest WHERE id=1";
        return (byte[])(cmd.ExecuteScalar() ??
            throw new InvalidDataException("The native workspace backup is missing."));
    }

    private static string MakeScratch(string path) =>
        path + ".refresh-" + Guid.NewGuid().ToString("N") + ".sqlite";

    private static string MakeRecovery(string path)
    {
        string directory = Path.GetDirectoryName(path)!;
        string root = Path.GetFileNameWithoutExtension(path);
        return Path.Combine(directory,
            root + ".rollback-" + DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss") +
            "-" + Guid.NewGuid().ToString("N")[..8] + ".sqlite");
    }

    private static void CheckCurrent(string path, string expectedSha)
    {
        var actual = NativeWorkspace.Open(path);
        if (!string.Equals(actual.BackupSha256, expectedSha, StringComparison.Ordinal))
            throw new InvalidDataException(
                "The native collection changed since comparison. No refresh was applied.");
    }

    private static Replacement ReplaceVerified(string nativePath,
        string stagedPath, string expectedCurrentSha, string expectedIncomingSha)
    {
        // Windows/NTFS atomic swap: original becomes a uniquely named retained
        // recovery file. No deletion, no overwrite of any previous recovery.
        string rollback = MakeRecovery(nativePath);
        if (File.Exists(rollback))
            throw new InvalidDataException("A native recovery file already exists.");
        CheckCurrent(nativePath, expectedCurrentSha);
        NativeWorkspace.Workspace staged = NativeWorkspace.Open(stagedPath);
        if (staged.BackupSha256 != expectedIncomingSha)
            throw new InvalidDataException("Staged native workspace failed SHA verification.");

        File.Replace(stagedPath, nativePath, rollback, ignoreMetadataErrors: false);

        // Never silently undo a failed post-swap verification. At this point
        // two independent copies may exist. Leave them intact for recovery.
        var now = NativeWorkspace.Open(nativePath);
        var former = NativeWorkspace.Open(rollback);
        if (now.BackupSha256 != expectedIncomingSha ||
            former.BackupSha256 != expectedCurrentSha)
            throw new InvalidDataException(
                "Native replacement verification failed. Preserve both the workspace and recovery file.");
        return new Replacement(now, rollback, former.BackupSha256);
    }

    public static Replacement Refresh(string nativePath, byte[] incoming,
        string sourceDescription, string expectedCurrentBackupSha,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDescription);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedCurrentBackupSha);
        lock (UpdateGate)
        {
            string path = Path.GetFullPath(nativePath);
            CheckCurrent(path, expectedCurrentBackupSha);
            var comparison = Compare(path, incoming);
            if (!comparison.HasChanges)
                throw new InvalidDataException("The native collection is already up to date.");

            string stagedPath = MakeScratch(path);
            try
            {
                cancellation.ThrowIfCancellationRequested();
                NativeWorkspace.ImportNewBytes(incoming, sourceDescription, stagedPath, cancellation);
                cancellation.ThrowIfCancellationRequested();
                return ReplaceVerified(path, stagedPath, expectedCurrentBackupSha,
                    comparison.CandidateBackupSha256);
            }
            finally
            {
                // Never touch a preserved .rollback-* recovery point.
                if (File.Exists(stagedPath)) File.Delete(stagedPath);
            }
        }
    }

    public static IReadOnlyList<string> RecoveryPaths(string nativePath)
    {
        string path = Path.GetFullPath(nativePath);
        string directory = Path.GetDirectoryName(path)!;
        if (!Directory.Exists(directory)) return Array.Empty<string>();
        return Directory.GetFiles(directory,
                Path.GetFileNameWithoutExtension(path) + ".rollback-*.sqlite",
                SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToArray();
    }

    public static Replacement Restore(string nativePath, string recoveryPath,
        string expectedCurrentBackupSha,
        CancellationToken cancellation = default)
    {
        lock (UpdateGate)
        {
            string path = Path.GetFullPath(nativePath);
            string recovery = Path.GetFullPath(recoveryPath);
            if (!RecoveryPaths(path).Contains(recovery, StringComparer.OrdinalIgnoreCase))
                throw new InvalidDataException(
                    "Only verified native workspace recovery points are eligible for restore.");
            CheckCurrent(path, expectedCurrentBackupSha);
            var old = NativeWorkspace.Open(recovery, cancellation);
            if (old.BackupSha256 == expectedCurrentBackupSha)
                throw new InvalidDataException("Recovery point already matches the native collection.");

            string staged = MakeScratch(path);
            try
            {
                cancellation.ThrowIfCancellationRequested();
                // Recovery copy is a closed, consistent SQLite file produced
                // by File.Replace; copy, then verify the new scratch clone.
                using (var read = new FileStream(recovery, FileMode.Open,
                           FileAccess.Read, FileShare.Read))
                using (var write = new FileStream(staged, FileMode.CreateNew,
                           FileAccess.Write, FileShare.None))
                {
                    read.CopyTo(write);
                    write.Flush(flushToDisk: true);
                }
                if (NativeWorkspace.Open(staged, cancellation).BackupSha256 != old.BackupSha256)
                    throw new InvalidDataException("Recovery staging validation failed.");
                cancellation.ThrowIfCancellationRequested();
                return ReplaceVerified(path, staged, expectedCurrentBackupSha,
                    old.BackupSha256);
            }
            finally
            {
                if (File.Exists(staged)) File.Delete(staged);
            }
        }
    }
}
