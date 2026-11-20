using ArcaneVault.Next.Domain;

namespace ArcaneVault.Next.Collection;

/// <summary>
/// Atomic candidate for moving exact, catalog-verified owned printing copies
/// into the separately unpriced proxy ledger. The workspace layer commits the
/// resulting full backup exactly once with its normal recovery point.
/// </summary>
public static class NativePhysicalProxyTransfer
{
    public sealed record Move(int Ordinal, NativePhysicalRow Expected, int Copies);

    public static byte[] Apply(byte[] completeBackup, string catalogPath,
        IReadOnlyList<Move> moves)
    {
        ArgumentNullException.ThrowIfNull(completeBackup);
        ArgumentNullException.ThrowIfNull(moves);
        if (moves.Count is < 1 or > 500)
            throw new InvalidDataException("Select between 1 and 500 physical printing rows.");

        var before = NativeBackupReader.Read(completeBackup);
        var oldProxies = NativeProxyEditing.Read(completeBackup);
        var ordinals = new HashSet<int>();
        var additions = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var move in moves)
        {
            if (move is null || move.Ordinal < 0 ||
                move.Ordinal >= before.PhysicalCards.Count ||
                !ordinals.Add(move.Ordinal))
                throw new InvalidDataException("A selected physical printing is missing or duplicated.");
            var actual = before.PhysicalCards[move.Ordinal];
            if (actual != move.Expected || move.Copies < 1 ||
                move.Copies > actual.Quantity)
                throw new InvalidDataException("The physical printing or selected quantity changed. Reopen the collection.");

            if (!Guid.TryParse(actual.ScryfallId, out var printId) ||
                printId == Guid.Empty)
                throw new InvalidDataException("Only catalog-confirmed physical printings can become proxies.");
            var confirmed = NativePhysicalEditing.RequirePaperPrinting(catalogPath, printId);
            if (!confirmed.Name.Equals(actual.Name, StringComparison.OrdinalIgnoreCase) ||
                !confirmed.SetCode.Equals(actual.SetCode, StringComparison.OrdinalIgnoreCase) ||
                !confirmed.CollectorNumber.Equals(actual.CollectorNumber, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The selected printing no longer matches the verified paper catalog.");

            additions[actual.Name] = checked(additions.GetValueOrDefault(actual.Name) + move.Copies);
        }

        // Apply decreasing ordinals so removing an entire row cannot shift a
        // still-pending selected row. This is all in memory until final commit.
        byte[] candidate = completeBackup;
        foreach (var move in moves.OrderByDescending(x => x.Ordinal))
            candidate = NativePhysicalEditing.SetCopies(candidate, move.Ordinal,
                move.Expected, move.Expected.Quantity - move.Copies);
        candidate = NativeProxyEditing.AddBatch(candidate, additions);

        var after = NativeBackupReader.Read(candidate);
        var newProxies = NativeProxyEditing.Read(candidate);
        long transferred = moves.Sum(x => (long)x.Copies);
        if (after.PhysicalCopies != before.PhysicalCopies - transferred ||
            after.ArenaCopies != before.ArenaCopies ||
            !after.ArenaCards.SequenceEqual(before.ArenaCards) ||
            after.Decks != before.Decks ||
            after.DeckRevisions != before.DeckRevisions ||
            after.PreferredArtworks != before.PreferredArtworks ||
            newProxies.Sum(x => (long)x.Quantity) !=
                oldProxies.Sum(x => (long)x.Quantity) + transferred)
            throw new InvalidDataException("Physical-to-proxy transfer failed conservation checks.");

        return candidate;
    }
}
