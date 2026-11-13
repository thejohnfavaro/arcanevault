using System.Security.Cryptography;

namespace ArcaneVault.Next.Domain;

/// <summary>
/// Explicit, read-only verification of an exported schema-2 full backup.
/// This does not open or alter the active workspace, restore backups,
/// synchronize accounts, or change any card data.
/// </summary>
public static class NativeReleaseReadiness
{
    public sealed record Snapshot(string Sha256, long FileBytes,
        int PhysicalRows, int DistinctPhysicalNames, long PhysicalCopies,
        int ArenaRows, long ArenaCopies, int Decks, int DeckRevisions,
        int PreferredArtworks, long UnverifiedPrintingIdCopies,
        long MissingOrInvalidPrintingIdCopies)
    {
        public bool HasPrintingIdGaps => MissingOrInvalidPrintingIdCopies > 0;
        public string Display =>
            "Full backup format: schema 2 (readable)\n" +
            $"SHA-256: {Sha256}\n" +
            $"Size: {FileBytes:N0} bytes\n" +
            $"Physical: {PhysicalCopies:N0} copies · {PhysicalRows:N0} printing rows · " +
                $"{DistinctPhysicalNames:N0} distinct names\n" +
            $"Arena: {ArenaCopies:N0} copies · {ArenaRows:N0} rows\n" +
            $"Decks: {Decks:N0} · Revisions: {DeckRevisions:N0} · " +
                $"Preferred artworks: {PreferredArtworks:N0}\n" +
            $"Physical copies with supplied but unverified printing IDs: {UnverifiedPrintingIdCopies:N0}\n" +
            $"Physical copies with missing/invalid printing IDs: {MissingOrInvalidPrintingIdCopies:N0}\n" +
            (HasPrintingIdGaps
                ? "REVIEW: Some physical cards cannot yet be tied to a specific printing. " +
                  "No printing has been inferred or changed.\n"
                : "No missing/invalid printing ID was found; supplied IDs are not verified against Scryfall.\n") +
            "This confirms the selected export can be parsed and hashed. " +
            "It does NOT confirm that the backup matches the active workspace, " +
            "that a restore was performed, or that all card metadata is correct. " +
            "No files or collection data were modified.";
    }

    public static Snapshot Inspect(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        // Read only. Existing parser validates schema/rows/quantity bounds;
        // hashing is streamed and never writes a manifest or temporary file.
        var file = new FileInfo(path);
        if (!file.Exists || file.Length is < 1 or > 100 * 1024 * 1024)
            throw new InvalidDataException("Select a nonempty full backup of 100 MB or less.");
        long originalSize = file.Length;
        DateTime originalModified = file.LastWriteTimeUtc;
        var view = NativeBackupReader.Read(path);
        string sha;
        using (var stream = new FileStream(path, FileMode.Open,
            FileAccess.Read, FileShare.Read))
            sha = Convert.ToHexString(SHA256.HashData(stream));
        file.Refresh();
        if (!file.Exists || file.Length != originalSize ||
            file.LastWriteTimeUtc != originalModified)
            throw new InvalidDataException(
                "The backup changed while it was being inspected. Select a stable exported backup.");
        return new Snapshot(sha, originalSize, view.PhysicalCards.Count,
            view.DistinctPhysicalNames, view.PhysicalCopies, view.ArenaCards.Count,
            view.ArenaCopies, view.Decks, view.DeckRevisions,
            view.PreferredArtworks, view.CopiesWithUnverifiedIds,
            view.CopiesWithMissingOrInvalidIds);
    }
}
