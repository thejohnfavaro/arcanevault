using ArcaneVault.Next.Domain;

namespace ArcaneVault.Next.Collection;

/// <summary>
/// Local, read-only comparison of an explicitly chosen exported schema-2
/// backup and the default active native workspace. No restore, import,
/// backup replacement or DB writes. The backup is historical data, not
/// automatically an error if it differs from the current collection.
/// </summary>
public static class NativeCollectionIntegrity
{
    public sealed record Finding(string Scope, string Item, long BackupCopies,
        long ActiveCopies)
    {
        public string Display =>
            $"{Scope}: {Item} · backup {BackupCopies:N0} → active {ActiveCopies:N0} " +
            $"({ActiveCopies - BackupCopies:+#;-#;0})";
    }

    public sealed record Report(string BackupSha256, string ActiveSha256,
        bool ByteIdentical, long BackupPhysical, long ActivePhysical,
        long BackupArena, long ActiveArena, int BackupDecks, int ActiveDecks,
        int BackupRevisions, int ActiveRevisions, int BackupArt, int ActiveArt,
        int BackupPhysicalRows, int ActivePhysicalRows, int BackupArenaRows,
        int ActiveArenaRows, int BackupNames, int ActiveNames,
        long BackupMissingIds, long ActiveMissingIds,
        IReadOnlyList<Finding> Differences, int TotalDifferingEntries)
    {
        public bool AggregateMatch =>
            BackupPhysical == ActivePhysical && BackupArena == ActiveArena &&
            BackupDecks == ActiveDecks && BackupRevisions == ActiveRevisions &&
            BackupArt == ActiveArt && BackupPhysicalRows == ActivePhysicalRows &&
            BackupArenaRows == ActiveArenaRows && BackupNames == ActiveNames &&
            BackupMissingIds == ActiveMissingIds;
        public bool InventoryMatch => TotalDifferingEntries == 0;
        public string Status => ByteIdentical ? "Identical export bytes"
            : InventoryMatch && AggregateMatch
                ? "Inventory/totals match; backup content differs"
                : "Differences found — review before relying on this backup";

        public string Display => Status + "\n" +
            $"Selected backup SHA-256: {BackupSha256}\n" +
            $"Active preserved backup SHA-256: {ActiveSha256}\n" +
            $"Physical copies: backup {BackupPhysical:N0} | active {ActivePhysical:N0}\n" +
            $"Arena copies: backup {BackupArena:N0} | active {ActiveArena:N0}\n" +
            $"Printing rows: backup {BackupPhysicalRows:N0} | active {ActivePhysicalRows:N0}\n" +
            $"Distinct physical names: backup {BackupNames:N0} | active {ActiveNames:N0}\n" +
            $"Arena rows: backup {BackupArenaRows:N0} | active {ActiveArenaRows:N0}\n" +
            $"Decks: backup {BackupDecks:N0} | active {ActiveDecks:N0}\n" +
            $"Deck revisions: backup {BackupRevisions:N0} | active {ActiveRevisions:N0}\n" +
            $"Preferred artwork entries: backup {BackupArt:N0} | active {ActiveArt:N0}\n" +
            $"Missing/invalid physical print IDs (copies): backup {BackupMissingIds:N0} | active {ActiveMissingIds:N0}\n" +
            $"Physical/Arena differing keys: {TotalDifferingEntries:N0}\n" +
            (Differences.Count == 0 ? "No card-quantity differences found.\n"
                : "Examples (first 20, preserving each physical printing):\n" +
                  string.Join("\n", Differences.Select(x => x.Display)) + "\n") +
            (TotalDifferingEntries > Differences.Count
                ? $"… {TotalDifferingEntries - Differences.Count:N0} further differing rows not displayed.\n"
                : "") +
            "This compares one selected historical export to the active verified native snapshot. " +
            "Different values may reflect intentional edits since the export. " +
            "A matching inventory does not prove equality of hidden/unknown backup fields; " +
            "only matching SHA-256 values establish byte identity. " +
            "Neither source was changed.";
    }

    public static Report Compare(string exportedBackupPath, string workspacePath,
        CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exportedBackupPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
        string backup = Path.GetFullPath(exportedBackupPath);
        string workspace = Path.GetFullPath(workspacePath);
        if (string.Equals(backup, workspace, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The selected backup and active workspace cannot be the same file.");
        cancellation.ThrowIfCancellationRequested();
        // ReadOnly mode + SQLite integrity and manifest validation are
        // enforced by NativeWorkspace.Open, independent of this comparison.
        var current = NativeWorkspace.Open(workspace, cancellation);
        cancellation.ThrowIfCancellationRequested();
        var selected = NativeReleaseReadiness.Inspect(backup);
        var historic = NativeBackupReader.Read(backup);
        cancellation.ThrowIfCancellationRequested();

        // Distinct physical printings are different keys even if their card
        // names match. Treat case-insensitive identifiers as equivalent, but
        // keep quantities and finish separate. No mutation or normalization.
        static Dictionary<string, long> Physical(NativeBackupView v)
        {
            var rows = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in v.PhysicalCards)
            {
                string key = string.Join("\u001f", r.Name.Trim(), r.SetCode.Trim(),
                    r.CollectorNumber.Trim(), r.Finish.Trim(), r.ScryfallId.Trim());
                rows[key] = checked(rows.GetValueOrDefault(key) + r.Quantity);
            }
            return rows;
        }
        static Dictionary<string, long> Arena(NativeBackupView v)
        {
            var rows = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in v.ArenaCards)
            {
                string key = r.Name.Trim();
                rows[key] = checked(rows.GetValueOrDefault(key) + r.Quantity);
            }
            return rows;
        }

        var differences = new List<Finding>(20);
        int total = 0;
        void Diff(string scope, Dictionary<string, long> old, Dictionary<string, long> now)
        {
            foreach (string key in old.Keys.Union(now.Keys,
                         StringComparer.OrdinalIgnoreCase)
                         .OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                cancellation.ThrowIfCancellationRequested();
                long earlier = old.GetValueOrDefault(key);
                long currentCopies = now.GetValueOrDefault(key);
                if (earlier == currentCopies) continue;
                total = checked(total + 1);
                if (differences.Count >= 20) continue;
                var parts = key.Split('\u001f');
                string display = scope == "Physical"
                    ? $"{parts[0]} · {parts[1]} #{parts[2]} · {parts[3]}" +
                      (parts[4].Length > 0 ? " · printing ID recorded" : " · no printing ID")
                    : parts[0];
                differences.Add(new Finding(scope, display, earlier, currentCopies));
            }
        }
        Diff("Physical", Physical(historic), Physical(current.Collection));
        Diff("Arena", Arena(historic), Arena(current.Collection));
        cancellation.ThrowIfCancellationRequested();

        // Guard against a selected export being overwritten during the scan.
        var selectedAgain = NativeReleaseReadiness.Inspect(backup);
        if (!string.Equals(selectedAgain.Sha256, selected.Sha256,
                StringComparison.Ordinal))
            throw new InvalidDataException("Selected backup changed during comparison. No result was accepted.");

        return new Report(selected.Sha256, current.BackupSha256,
            string.Equals(selected.Sha256, current.BackupSha256,
                StringComparison.OrdinalIgnoreCase),
            historic.PhysicalCopies, current.Collection.PhysicalCopies,
            historic.ArenaCopies, current.Collection.ArenaCopies,
            historic.Decks, current.Collection.Decks,
            historic.DeckRevisions, current.Collection.DeckRevisions,
            historic.PreferredArtworks, current.Collection.PreferredArtworks,
            historic.PhysicalCards.Count, current.Collection.PhysicalCards.Count,
            historic.ArenaCards.Count, current.Collection.ArenaCards.Count,
            historic.DistinctPhysicalNames, current.Collection.DistinctPhysicalNames,
            historic.CopiesWithMissingOrInvalidIds,
            current.Collection.CopiesWithMissingOrInvalidIds,
            differences, total);
    }
}
