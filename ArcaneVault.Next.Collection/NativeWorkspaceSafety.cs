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

    /// <summary>
    /// Opt-in reset to a complete EMPTY native inventory, deck library, and
    /// artwork/preferences. The independent Scryfall catalog is not changed.
    /// For an existing workspace, preserve the ENTIRE verified prior SQLite
    /// database as a new recovery point; never clear it in place.
    /// </summary>
    public sealed record FreshStartOutcome(
        NativeWorkspace.Workspace Current,
        string? RecoveryPath,
        bool Created,
        bool AlreadyEmpty);

    private static byte[] EmptyCollectionBackup() =>
        JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema = 2,
            source = "arcane-vault-next-fresh-collection",
            exported = DateTimeOffset.UtcNow.ToString("O"),
            cards = Array.Empty<object>(),
            physicalCards = Array.Empty<object>(),
            deckLibrary = Array.Empty<object>(),
            preferredArtwork = new Dictionary<string, object>(),
            favorites = Array.Empty<string>(),
            deck = (object?)null,
            studio = new Dictionary<string, object>(),
            wildcards = new { common = 0, uncommon = 0, rare = 0, mythic = 0 },
            collectionHistory = Array.Empty<object>()
        });

    /// <summary>
    /// Create a brand-new empty native collection without a legacy profile or
    /// JSON export. When a workspace already exists, require its caller-observed
    /// SHA, then stage-and-swap using the same verified rollback pipeline as
    /// native refresh. An already-fresh workspace is an unchanged no-op.
    /// </summary>
    public static FreshStartOutcome StartFresh(string nativePath,
        string? expectedCurrentBackupSha = null,
        CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nativePath);
        lock (UpdateGate)
        {
            cancellation.ThrowIfCancellationRequested();
            var path = Path.GetFullPath(nativePath);
            byte[] blank = EmptyCollectionBackup();
            if (!File.Exists(path))
            {
                if (!string.IsNullOrWhiteSpace(expectedCurrentBackupSha))
                    throw new InvalidDataException(
                        "Native collection was removed since confirmation. No new collection was created.");
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var created = NativeWorkspace.ImportNewBytes(blank,
                    "fresh-native-collection", path, cancellation);
                return new FreshStartOutcome(created, null, Created: true, AlreadyEmpty: false);
            }

            if (string.IsNullOrWhiteSpace(expectedCurrentBackupSha))
                throw new InvalidDataException(
                    "Reset of an existing native collection requires its verified current checksum.");
            CheckCurrent(path, expectedCurrentBackupSha);
            var diff = Compare(path, blank);
            if (!diff.HasChanges)
                return new FreshStartOutcome(
                    NativeWorkspace.Open(path, cancellation), null,
                    Created: false, AlreadyEmpty: true);

            var replacement = Refresh(path, blank, "fresh-native-collection",
                expectedCurrentBackupSha, cancellation);
            var result = replacement.Current;
            if (result.Collection.PhysicalCopies != 0 ||
                result.Collection.ArenaCopies != 0 ||
                result.Collection.Decks != 0 ||
                result.Collection.DeckRevisions != 0 ||
                result.Collection.PreferredArtworks != 0)
                throw new InvalidDataException(
                    "Fresh collection verification failed. Preserve native recovery files.");
            return new FreshStartOutcome(result, replacement.RecoveryPath,
                Created: false, AlreadyEmpty: false);
        }
    }

    /// <summary>
    /// Clear one or both INVENTORY scopes without resetting saved decks,
    /// revisions, shortlist, artwork choices, wildcards or opaque legacy data.
    /// Every nonempty clear uses the same SHA-guarded stage/swap/recovery path
    /// as all other verified native edits. Never touches the Scryfall catalog.
    /// </summary>
    public enum InventoryScope { Physical, Arena, Both }

    public sealed record InventoryClearOutcome(
        NativeWorkspace.Workspace Current, string? RecoveryPath,
        bool AlreadyEmpty, InventoryScope Scope);

    public static InventoryClearOutcome ClearInventory(
        string nativePath, InventoryScope scope, string expectedCurrentBackupSha,
        CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nativePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedCurrentBackupSha);
        if (!Enum.IsDefined(scope))
            throw new ArgumentOutOfRangeException(nameof(scope));
        lock (UpdateGate)
        {
            cancellation.ThrowIfCancellationRequested();
            string path = Path.GetFullPath(nativePath);
            CheckCurrent(path, expectedCurrentBackupSha);
            var current = NativeWorkspace.Open(path, cancellation);
            bool physical = scope is InventoryScope.Physical or InventoryScope.Both;
            bool arena = scope is InventoryScope.Arena or InventoryScope.Both;
            if ((!physical || current.Collection.PhysicalCards.Count == 0) &&
                (!arena || current.Collection.ArenaCards.Count == 0))
                return new InventoryClearOutcome(current, null, true, scope);

            var source = ReadPreservedBackup(path);
            using var doc = JsonDocument.Parse(source);
            using var output = new MemoryStream();
            using (var writer = new Utf8JsonWriter(output))
            {
                writer.WriteStartObject();
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    if ((physical && prop.NameEquals("physicalCards")) ||
                        (arena && prop.NameEquals("cards")))
                    {
                        writer.WriteStartArray(prop.Name);
                        writer.WriteEndArray();
                    }
                    else prop.WriteTo(writer);
                }
                writer.WriteEndObject();
            }
            byte[] newPayload = output.ToArray();
            var candidate = NativeBackupReader.Read(newPayload);
            if ((physical && candidate.PhysicalCopies != 0) ||
                (arena && candidate.ArenaCopies != 0) ||
                (!physical && !candidate.PhysicalCards.SequenceEqual(
                    current.Collection.PhysicalCards)) ||
                (!arena && !candidate.ArenaCards.SequenceEqual(
                    current.Collection.ArenaCards)) ||
                candidate.Decks != current.Collection.Decks ||
                candidate.DeckRevisions != current.Collection.DeckRevisions ||
                candidate.PreferredArtworks != current.Collection.PreferredArtworks)
                throw new InvalidDataException(
                    "Selective clear changed unexpected collection data; no reset was applied.");
            cancellation.ThrowIfCancellationRequested();
            var change = Refresh(path, newPayload,
                "native-clear-" + scope.ToString().ToLowerInvariant(),
                expectedCurrentBackupSha, cancellation);
            var result = change.Current.Collection;
            if ((physical && result.PhysicalCopies != 0) ||
                (arena && result.ArenaCopies != 0) ||
                (!physical && !result.PhysicalCards.SequenceEqual(
                    current.Collection.PhysicalCards)) ||
                (!arena && !result.ArenaCards.SequenceEqual(
                    current.Collection.ArenaCards)) ||
                result.Decks != current.Collection.Decks ||
                result.DeckRevisions != current.Collection.DeckRevisions ||
                result.PreferredArtworks != current.Collection.PreferredArtworks)
                throw new InvalidDataException(
                    "Selective clear verification failed; preserve all recovery files.");
            return new InventoryClearOutcome(change.Current, change.RecoveryPath,
                false, scope);
        }
    }

    // Compatibility for the first XLSX-only release and its regression tests.
    public static Replacement AddExcelPhysical(string nativePath, string? catalogPath,
        IReadOnlyList<NativeExcelCollectionImport.Row> workbookRows,
        string expectedCurrentBackupSha, CancellationToken cancellation = default) =>
        AddPhysicalImport(nativePath, catalogPath, workbookRows,
            expectedCurrentBackupSha, cancellation);

    /// <summary>
    /// Add copies of one explicitly selected catalog-verified paper printing to
    /// the native collection. The expected checksum protects against stale UI
    /// edits. Refresh stages a complete verified replacement and retains the
    /// entire old database as a new, independently restorable recovery point.
    /// The legacy Windows app and external catalog are never written.
    /// </summary>
    /// <summary>
    /// Append validated CSV or XLSX physical quantities to the active native
    /// collection in ONE verified SQLite transaction and ONE recovery point.
    /// The parsed rows are revalidated against the current catalog at commit.
    /// </summary>
    public static Replacement AddPhysicalImport(string nativePath, string? catalogPath,
        IReadOnlyList<NativeExcelCollectionImport.Row> workbookRows,
        string expectedCurrentBackupSha, CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nativePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedCurrentBackupSha);
        ArgumentNullException.ThrowIfNull(workbookRows);
        lock (UpdateGate)
        {
            cancellation.ThrowIfCancellationRequested();
            string path = Path.GetFullPath(nativePath);
            CheckCurrent(path, expectedCurrentBackupSha);
            var preview = NativeExcelCollectionImport.Prepare(workbookRows, catalogPath);
            byte[] updated = NativeExcelCollectionImport.Add(
                ReadPreservedBackup(path), preview);
            cancellation.ThrowIfCancellationRequested();
            return Refresh(path, updated, "native-excel-physical-addition",
                expectedCurrentBackupSha, cancellation);
        }
    }

    /// <summary>
    /// Preview CSV/XLSX as an idempotent quantity snapshot. Import file rows
    /// missing from the file leave local-only rows alone, and unchanged
    /// quantities never create a recovery point.
    /// </summary>
    public static NativeCollectionReconciliation.Result PreviewPhysicalReconciliation(
        string nativePath, string? catalogPath,
        IReadOnlyList<NativeExcelCollectionImport.Row> rows,
        string expectedCurrentBackupSha)
    {
        lock (UpdateGate)
        {
            string path = Path.GetFullPath(nativePath);
            CheckCurrent(path, expectedCurrentBackupSha);
            var prepared = NativeExcelCollectionImport.Prepare(rows, catalogPath);
            return NativeCollectionReconciliation.Physical(
                ReadPreservedBackup(path), prepared);
        }
    }

    public static Replacement ReconcilePhysicalImport(
        string nativePath, string? catalogPath,
        IReadOnlyList<NativeExcelCollectionImport.Row> rows,
        string expectedCurrentBackupSha, string expectedCandidateSha,
        CancellationToken cancellation = default)
    {
        lock (UpdateGate)
        {
            cancellation.ThrowIfCancellationRequested();
            string path = Path.GetFullPath(nativePath);
            CheckCurrent(path, expectedCurrentBackupSha);
            var prepared = NativeExcelCollectionImport.Prepare(rows, catalogPath);
            var preview = NativeCollectionReconciliation.Physical(
                ReadPreservedBackup(path), prepared);
            if (!preview.HasChanges)
                throw new InvalidDataException(
                    "The imported quantities already match your collection.");
            if (!preview.CandidateSha256.Equals(expectedCandidateSha,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(
                    "Import diff changed since review. Review the latest quantities again.");
            return Refresh(path, preview.Candidate, "native-physical-csv-xlsx-reconciliation",
                expectedCurrentBackupSha, cancellation);
        }
    }

    public static NativeCollectionReconciliation.Result PreviewArenaReconciliation(
        string nativePath, IReadOnlyList<NativeExcelCollectionImport.Row> rows,
        string expectedCurrentBackupSha)
    {
        lock (UpdateGate)
        {
            string path = Path.GetFullPath(nativePath);
            CheckCurrent(path, expectedCurrentBackupSha);
            return NativeCollectionReconciliation.Arena(
                ReadPreservedBackup(path), rows);
        }
    }

    public static Replacement ReconcileArenaImport(
        string nativePath, IReadOnlyList<NativeExcelCollectionImport.Row> rows,
        string expectedCurrentBackupSha, string expectedCandidateSha,
        CancellationToken cancellation = default)
    {
        lock (UpdateGate)
        {
            cancellation.ThrowIfCancellationRequested();
            string path = Path.GetFullPath(nativePath);
            CheckCurrent(path, expectedCurrentBackupSha);
            var preview = NativeCollectionReconciliation.Arena(
                ReadPreservedBackup(path), rows);
            if (!preview.HasChanges)
                throw new InvalidDataException(
                    "The imported quantities already match your Arena collection.");
            if (!preview.CandidateSha256.Equals(expectedCandidateSha,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(
                    "Arena import diff changed since review. Review the latest quantities again.");
            return Refresh(path, preview.Candidate, "native-arena-csv-xlsx-reconciliation",
                expectedCurrentBackupSha, cancellation);
        }
    }

    public static Replacement AddVerifiedPhysical(string nativePath,
        string catalogPath, Guid printId, int quantity, string finish,
        string expectedCurrentBackupSha, CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nativePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedCurrentBackupSha);
        lock (UpdateGate)
        {
            cancellation.ThrowIfCancellationRequested();
            var path = Path.GetFullPath(nativePath);
            CheckCurrent(path, expectedCurrentBackupSha);
            var printing = NativePhysicalEditing.RequirePaperPrinting(catalogPath, printId);
            byte[] updated = NativePhysicalEditing.AddCopies(
                ReadPreservedBackup(path), printing, quantity, finish);
            cancellation.ThrowIfCancellationRequested();
            return Refresh(path, updated, "verified-native-paper-printing",
                expectedCurrentBackupSha, cancellation);
        }
    }

    /// <summary>
    /// Reduce or remove ONE explicitly selected physical line, including a
    /// legacy unresolved row. Recheck live checksum and selected row before a
    /// staged complete replacement; all other collection sections survive.
    /// Returns the new complete workspace and its automatic rollback path.
    /// </summary>
    /// <summary>
    /// Add Arena name-only copies to a native inventory. No claim is made
    /// about exact physical printing, set ownership or Arena format legality.
    /// Retains one complete verified recovery file for every successful edit.
    /// </summary>
    /// <summary>
    /// Read-only saved deck listing from the independent validated native
    /// database. No legacy WebView interaction or catalog dependency.
    /// </summary>
    public static IReadOnlyList<NativeDeckEditing.DeckRow> ListDecks(
        string nativePath, CancellationToken cancellation = default)
    {
        var verified = NativeWorkspace.Open(nativePath, cancellation);
        return NativeDeckLibraryCache.List(verified.BackupSha256,
            () => ReadPreservedBackup(nativePath), cancellation: cancellation);
    }

    /// <summary>
    /// List decks from an ALREADY fully verified replacement result. This
    /// skips a second expensive SQLite integrity/row-projection traversal,
    /// but still hashes the source backup bytes and fails closed if another
    /// update happened between verification and deck-list extraction.
    /// UI cache entries are presentation only, never write authorization.
    /// </summary>
    public static IReadOnlyList<NativeDeckEditing.DeckRow> ListDecksFromVerified(
        NativeWorkspace.Workspace verified, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(verified);
        cancellation.ThrowIfCancellationRequested();
        // Check current source even when the display cache itself would hit.
        byte[] bytes = ReadPreservedBackup(verified.DatabasePath);
        if (!Digest(bytes).Equals(verified.BackupSha256,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(
                "Native deck list changed after the verified save. Reload the workspace.");
        cancellation.ThrowIfCancellationRequested();
        return NativeDeckLibraryCache.List(verified.BackupSha256,
            () => bytes, cancellation: cancellation);
    }

    /// <summary>
    /// Add or update the exact native deck with optimistic SHA + selected
    /// deck fingerprint validation and a complete automatic rollback copy.
    /// </summary>
    /// <summary>Read the legacy-compatible preference without writing a backup.</summary>
    public static NativePrintingArtwork.Preferred? GetPreferredArtwork(
        string nativePath, string cardName)
    {
        if (string.IsNullOrWhiteSpace(nativePath) || !File.Exists(nativePath))
            return null;
        // Reject corrupt workspace metadata before trusting preference URLs.
        _ = NativeWorkspace.Open(nativePath);
        return NativePrintingArtwork.ReadPreferred(ReadPreservedBackup(nativePath), cardName);
    }

    /// <summary>
    /// Save exactly one card's preferred Scryfall printing. The ID/name/image
    /// are revalidated against the current official read-only catalog at the
    /// moment of the SHA-guarded write; the existing legacy JSON preference
    /// shape and all other backed-up data are preserved with a full rollback.
    /// </summary>
    public static Replacement SavePreferredArtwork(string nativePath,
        string catalogPath, string cardName, Guid printId,
        string expectedCurrentBackupSha, CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nativePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedCurrentBackupSha);
        lock (UpdateGate)
        {
            cancellation.ThrowIfCancellationRequested();
            string path = Path.GetFullPath(nativePath);
            CheckCurrent(path, expectedCurrentBackupSha);
            var verified = NativePrintingArtwork.Require(catalogPath, cardName, printId);
            byte[] changed = NativePrintingArtwork.Choose(
                ReadPreservedBackup(path), verified);
            cancellation.ThrowIfCancellationRequested();
            return Refresh(path, changed, "native-preferred-artwork",
                expectedCurrentBackupSha, cancellation);
        }
    }

    /// <summary>Clear only this card's preference, never the cached images or holdings.</summary>
    public static Replacement ResetPreferredArtwork(string nativePath,
        string cardName, string expectedCurrentBackupSha,
        CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nativePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedCurrentBackupSha);
        lock (UpdateGate)
        {
            cancellation.ThrowIfCancellationRequested();
            string path = Path.GetFullPath(nativePath);
            CheckCurrent(path, expectedCurrentBackupSha);
            byte[] changed = NativePrintingArtwork.Reset(
                ReadPreservedBackup(path), cardName);
            cancellation.ThrowIfCancellationRequested();
            return Refresh(path, changed, "native-preferred-artwork-reset",
                expectedCurrentBackupSha, cancellation);
        }
    }

    /// <summary>
    /// Read legacy WebView favorites from the complete verified native backup.
    /// No write or automatic migration of the installed WebView profile.
    /// </summary>
    public static IReadOnlyList<string> ListShortlist(string nativePath)
    {
        _ = NativeWorkspace.Open(nativePath);
        return NativeShortlist.Read(ReadPreservedBackup(nativePath));
    }

    /// <summary>
    /// Bookmark/unbookmark one card name using the original schema-2 favorites
    /// array. Optimistic SHA plus atomically swapped SQLite and full recovery
    /// point prevent stale dialogs from overwriting other collection changes.
    /// </summary>
    public static Replacement SetShortlistCard(string nativePath, string name,
        bool bookmarked, string expectedCurrentBackupSha,
        CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nativePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedCurrentBackupSha);
        lock (UpdateGate)
        {
            cancellation.ThrowIfCancellationRequested();
            string path = Path.GetFullPath(nativePath);
            CheckCurrent(path, expectedCurrentBackupSha);
            byte[] updated = NativeShortlist.Set(
                ReadPreservedBackup(path), name, bookmarked);
            cancellation.ThrowIfCancellationRequested();
            return Refresh(path, updated,
                bookmarked ? "native-shortlist-add" : "native-shortlist-remove",
                expectedCurrentBackupSha, cancellation);
        }
    }

    public static Replacement SaveDeck(string nativePath,
        string name, string commander, string mainboard,
        NativeDeckEditing.DeckRow? selected, string expectedCurrentBackupSha,
        CancellationToken cancellation = default, string? format = null,
        string? folder = null, bool? favorite = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedCurrentBackupSha);
        lock (UpdateGate)
        {
            cancellation.ThrowIfCancellationRequested();
            string path = Path.GetFullPath(nativePath);
            CheckCurrent(path, expectedCurrentBackupSha);
            var changed = NativeDeckEditing.Save(ReadPreservedBackup(path),
                name, commander, mainboard, selected, format, folder, favorite);
            cancellation.ThrowIfCancellationRequested();
            return Refresh(path, changed, "native-deck-library-save",
                expectedCurrentBackupSha, cancellation);
        }
    }

    /// <summary>
    /// Duplicate one selected deck without copying its revision history or
    /// Arena link. Uses the same atomic SQLite swap and verified rollback.
    /// </summary>
    public static Replacement DuplicateDeck(string nativePath,
        NativeDeckEditing.DeckRow selected, string expectedCurrentBackupSha,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(selected);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedCurrentBackupSha);
        lock (UpdateGate)
        {
            cancellation.ThrowIfCancellationRequested();
            string path = Path.GetFullPath(nativePath);
            CheckCurrent(path, expectedCurrentBackupSha);
            var changed = NativeDeckEditing.Duplicate(
                ReadPreservedBackup(path), selected);
            cancellation.ThrowIfCancellationRequested();
            return Refresh(path, changed, "native-deck-library-duplicate",
                expectedCurrentBackupSha, cancellation);
        }
    }

    /// <summary>
    /// Delete only one exact selected saved deck. Its complete payload and
    /// every revision remain in the automatic SQLite rollback file.
    /// </summary>
    public static Replacement DeleteDeck(string nativePath,
        NativeDeckEditing.DeckRow selected,
        string expectedCurrentBackupSha,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(selected);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedCurrentBackupSha);
        lock (UpdateGate)
        {
            cancellation.ThrowIfCancellationRequested();
            string path = Path.GetFullPath(nativePath);
            CheckCurrent(path, expectedCurrentBackupSha);
            var changed = NativeDeckEditing.Delete(
                ReadPreservedBackup(path), selected);
            cancellation.ThrowIfCancellationRequested();
            return Refresh(path, changed, "native-deck-library-delete",
                expectedCurrentBackupSha, cancellation);
        }
    }

    /// <summary>
    /// Add CSV/XLSX Arena name-based quantities atomically with one recovery
    /// point, retaining the complete existing physical/deck/art collection.
    /// </summary>
    public static Replacement AddArenaImport(string nativePath,
        IReadOnlyList<NativeExcelCollectionImport.Row> imported,
        string expectedCurrentBackupSha, CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nativePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedCurrentBackupSha);
        ArgumentNullException.ThrowIfNull(imported);
        lock (UpdateGate)
        {
            cancellation.ThrowIfCancellationRequested();
            string path = Path.GetFullPath(nativePath);
            CheckCurrent(path, expectedCurrentBackupSha);
            byte[] updated = NativeArenaEditing.AddImport(
                ReadPreservedBackup(path), imported);
            cancellation.ThrowIfCancellationRequested();
            return Refresh(path, updated, "native-arena-collection-import",
                expectedCurrentBackupSha, cancellation);
        }
    }

    public static Replacement AddArenaCopies(string nativePath, string cardName,
        int quantity, string expectedCurrentBackupSha,
        CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nativePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedCurrentBackupSha);
        lock (UpdateGate)
        {
            cancellation.ThrowIfCancellationRequested();
            string path = Path.GetFullPath(nativePath);
            CheckCurrent(path, expectedCurrentBackupSha);
            byte[] updated = NativeArenaEditing.AddCopies(
                ReadPreservedBackup(path), cardName, quantity);
            cancellation.ThrowIfCancellationRequested();
            return Refresh(path, updated, "native-arena-add-copies",
                expectedCurrentBackupSha, cancellation);
        }
    }

    /// <summary>
    /// Change one user-selected Arena name/quantity or remove that exact row.
    /// The source ordinal, old row and native workspace SHA must all match.
    /// </summary>
    public static Replacement EditArenaRow(string nativePath, int arenaOrdinal,
        NativeArenaRow expectedRow, string newCardName, int newQuantity,
        string expectedCurrentBackupSha, CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nativePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedCurrentBackupSha);
        ArgumentNullException.ThrowIfNull(expectedRow);
        lock (UpdateGate)
        {
            cancellation.ThrowIfCancellationRequested();
            string path = Path.GetFullPath(nativePath);
            CheckCurrent(path, expectedCurrentBackupSha);
            byte[] updated = NativeArenaEditing.EditRow(
                ReadPreservedBackup(path), arenaOrdinal, expectedRow, newCardName, newQuantity);
            cancellation.ThrowIfCancellationRequested();
            return Refresh(path, updated, "native-arena-edit-row",
                expectedCurrentBackupSha, cancellation);
        }
    }

    public static Replacement SetPhysicalQuantity(string nativePath,
        int physicalOrdinal, NativePhysicalRow expectedRow, int newQuantity,
        string expectedCurrentBackupSha, CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nativePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedCurrentBackupSha);
        ArgumentNullException.ThrowIfNull(expectedRow);
        lock (UpdateGate)
        {
            cancellation.ThrowIfCancellationRequested();
            string path = Path.GetFullPath(nativePath);
            CheckCurrent(path, expectedCurrentBackupSha);
            byte[] updated = NativePhysicalEditing.SetCopies(
                ReadPreservedBackup(path), physicalOrdinal, expectedRow, newQuantity);
            cancellation.ThrowIfCancellationRequested();
            return Refresh(path, updated, "native-paper-quantity-adjustment",
                expectedCurrentBackupSha, cancellation);
        }
    }

    public sealed record FullBackupExport(
        string FilePath, string BackupSha256, long Bytes,
        long PhysicalCopies, long ArenaCopies, int Decks, int Revisions,
        int PreferredArtwork);

    /// <summary>
    /// Export an exact, verified schema-2 backup of the CURRENT native
    /// workspace, including native inventory/deck edits and unknown legacy
    /// fields. Never overwrites an existing destination or the old app.
    /// </summary>
    public static FullBackupExport ExportFullBackup(string nativePath,
        string destinationPath, string expectedCurrentBackupSha,
        CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nativePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedCurrentBackupSha);
        lock (UpdateGate)
        {
            cancellation.ThrowIfCancellationRequested();
            string source = Path.GetFullPath(nativePath);
            string destination = Path.GetFullPath(destinationPath);
            if (!destination.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Full backups must be saved as a .json file.");
            if (!Directory.Exists(Path.GetDirectoryName(destination)!))
                throw new DirectoryNotFoundException(
                    "Choose an existing folder for your full backup.");
            string oldProfile = Path.GetFullPath(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ArcaneVault")).TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            if (destination.StartsWith(oldProfile, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(
                    "Never export directly into the installed legacy app profile.");
            if (File.Exists(destination) || Directory.Exists(destination))
                throw new InvalidDataException(
                    "That backup filename already exists. Choose a new filename; no file was overwritten.");

            CheckCurrent(source, expectedCurrentBackupSha);
            var workspace = NativeWorkspace.Open(source, cancellation);
            byte[] bytes = ReadPreservedBackup(source);
            string digest = Convert.ToHexString(SHA256.HashData(bytes));
            if (digest != workspace.BackupSha256)
                throw new InvalidDataException("Native backup checksum does not match the verified workspace.");
            var content = NativeBackupReader.Read(bytes);
            if (content.PhysicalCopies != workspace.Collection.PhysicalCopies ||
                content.ArenaCopies != workspace.Collection.ArenaCopies ||
                content.Decks != workspace.Collection.Decks ||
                content.DeckRevisions != workspace.Collection.DeckRevisions ||
                content.PreferredArtworks != workspace.Collection.PreferredArtworks ||
                !content.PhysicalCards.SequenceEqual(workspace.Collection.PhysicalCards) ||
                !content.ArenaCards.SequenceEqual(workspace.Collection.ArenaCards))
                throw new InvalidDataException(
                    "Native full backup does not match current persisted collection.");

            string staged = destination + "." + Guid.NewGuid().ToString("N") + ".partial";
            try
            {
                cancellation.ThrowIfCancellationRequested();
                using (var output = new FileStream(staged, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None, 128 * 1024, FileOptions.WriteThrough))
                {
                    output.Write(bytes);
                    output.Flush(flushToDisk: true);
                }
                cancellation.ThrowIfCancellationRequested();
                if (!Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(staged)))
                    .Equals(digest, StringComparison.Ordinal))
                    throw new InvalidDataException("Staged exported backup checksum failed.");

                // Another native edit in this process cannot interleave the
                // UpdateGate, and a changed external writer fails closed here.
                CheckCurrent(source, expectedCurrentBackupSha);
                cancellation.ThrowIfCancellationRequested();
                File.Move(staged, destination, overwrite: false);
                return new FullBackupExport(destination, digest, bytes.LongLength,
                    content.PhysicalCopies, content.ArenaCopies, content.Decks,
                    content.DeckRevisions, content.PreferredArtworks);
            }
            finally
            {
                if (File.Exists(staged)) File.Delete(staged);
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
