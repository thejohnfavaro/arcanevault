using ArcaneVault.Next.Domain;
using System.Security.Cryptography;
using System.Text;

namespace ArcaneVault.Next.EDHLab;

/// <summary>
/// Bounded, process-local cache for verified advisory EDH Lab snapshots.
/// The authoritative offline Scryfall SQLite and native collection stay
/// unchanged. Source fingerprints include SQLite WAL metadata, commander,
/// format and actual pool-specific owned quantities. This cache is never a
/// substitute for the protected native deck preflight/save path.
/// </summary>
public static class EdhLabReadOnlyCache
{
    private sealed record Stamp(string Path, long Length, long Modified,
        long WalLength, long WalModified);
    private sealed record DraftKey(Stamp Catalog, string Commander,
        string Colors, EdhLabFormat Format, string PoolHash);
    private static readonly object Gate = new();
    private const int MaxDraftSnapshots = 2;
    private static readonly LinkedList<(DraftKey Key, EdhLabDraftCatalog Value)>
        DraftEntries = new();
    private static (Stamp Stamp, EdhLabCommanderIndex Value)? commanderEntry;

    private static Stamp Fingerprint(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string full = Path.GetFullPath(path);
        var file = new FileInfo(full);
        if (!file.Exists) throw new FileNotFoundException(
            "EDH Lab needs its synced offline card catalog.", full);
        var wal = new FileInfo(full + "-wal");
        return new Stamp(full, file.Length, file.LastWriteTimeUtc.Ticks,
            wal.Exists ? wal.Length : 0,
            wal.Exists ? wal.LastWriteTimeUtc.Ticks : 0);
    }

    // Pool-specific name/quantity fingerprint: deck edits don't invalidate
    // ownership, while even same-total different-name imports always do.
    private static string PoolHash(NativeBackupView inventory, EdhLabFormat format)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        var totals = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        if (format == EdhLabFormat.ArenaBrawl)
        {
            foreach (var card in inventory.ArenaCards)
                if (card.Quantity > 0 && !string.IsNullOrWhiteSpace(card.Name))
                    totals[card.Name] = checked(totals.GetValueOrDefault(card.Name) +
                        card.Quantity);
        }
        else
        {
            foreach (var card in inventory.PhysicalCards)
                if (card.Quantity > 0 && !string.IsNullOrWhiteSpace(card.Name))
                    totals[card.Name] = checked(totals.GetValueOrDefault(card.Name) +
                        card.Quantity);
        }
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var card in totals.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            hash.AppendData(Encoding.UTF8.GetBytes(card.Key.ToUpperInvariant()));
            hash.AppendData(new byte[] { 0 });
            hash.AppendData(Encoding.UTF8.GetBytes(card.Value.ToString(
                System.Globalization.CultureInfo.InvariantCulture)));
            hash.AppendData(new byte[] { 0 });
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static DraftKey KeyFor(string path, NativeBackupView inventory,
        string commander, string colors, EdhLabFormat format) =>
        new(Fingerprint(path), commander.Trim().ToUpperInvariant(),
            colors.Trim().ToUpperInvariant(), format, PoolHash(inventory, format));

    public static bool TryGetDraft(string catalogPath, NativeBackupView inventory,
        string commander, string colors, EdhLabFormat format,
        out EdhLabDraftCatalog? cached)
    {
        var key = KeyFor(catalogPath, inventory, commander, colors, format);
        lock (Gate)
        {
            for (var node = DraftEntries.First; node is not null; node = node.Next)
            {
                if (node.Value.Key != key) continue;
                cached = node.Value.Value;
                DraftEntries.Remove(node);
                DraftEntries.AddFirst(node);
                return true;
            }
        }
        cached = null;
        return false;
    }

    public static EdhLabDraftCatalog GetDraft(string catalogPath,
        NativeBackupView inventory, string commander, string colors,
        EdhLabFormat format, CancellationToken cancellation = default)
    {
        if (TryGetDraft(catalogPath, inventory, commander, colors, format,
                out var ready) && ready is not null)
            return ready;
        cancellation.ThrowIfCancellationRequested();
        var before = KeyFor(catalogPath, inventory, commander, colors, format);
        var loaded = EdhLabDraftCatalog.OpenReadOnly(catalogPath, inventory,
            commander, colors, format, cancellation);
        cancellation.ThrowIfCancellationRequested();
        // A simultaneous Scryfall update cannot make an old eligibility
        // snapshot appear current. Reopen on next request; never cache it.
        if (Fingerprint(catalogPath) != before.Catalog)
            return loaded;
        lock (Gate)
        {
            var current = DraftEntries.First;
            while (current is not null)
            {
                if (current.Value.Key == before)
                    return current.Value.Value;
                current = current.Next;
            }
            DraftEntries.AddFirst((before, loaded));
            while (DraftEntries.Count > MaxDraftSnapshots)
                DraftEntries.RemoveLast();
        }
        return loaded;
    }

    public static EdhLabCommanderIndex GetCommanderIndex(string catalogPath,
        CancellationToken cancellation = default)
    {
        var stamp = Fingerprint(catalogPath);
        lock (Gate)
            if (commanderEntry is { } existing && existing.Stamp == stamp)
                return existing.Value;
        cancellation.ThrowIfCancellationRequested();
        var loaded = CommanderCatalog.OpenReadOnly(catalogPath, cancellation);
        cancellation.ThrowIfCancellationRequested();
        if (Fingerprint(catalogPath) != stamp)
            return loaded;
        lock (Gate) commanderEntry = (stamp, loaded);
        return loaded;
    }

    /// <summary>
    /// Manual invalidation when native sync/recovery supplies a replacement
    /// catalog; automatic file/owned-name fingerprints cover ordinary use.
    /// </summary>
    public static void Clear()
    {
        lock (Gate)
        {
            DraftEntries.Clear();
            commanderEntry = null;
        }
    }
}
