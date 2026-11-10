using System.Collections.ObjectModel;

namespace ArcaneVault.Next.Domain;

/// <summary>
/// Phase 1 immutable read model. Legacy name-only imports must keep PrintId null;
/// set and collector number hints by themselves are NOT confirmed printings.
/// No persistence or migration is performed by these types.
/// </summary>
public sealed record PhysicalInventoryLine(
    Guid LineId,
    string Name,
    int Quantity,
    Guid? PrintId,
    string? SetCode,
    string? CollectorNumber,
    string Finish);

/// <summary>Trusted, indexed Scryfall catalog record, never a user-owned row.</summary>
public sealed record CatalogPrinting(Guid PrintId, string Name, string SetCode, string CollectorNumber);

public enum UnresolvedReason
{
    NoPrintId,
    PrintIdAbsentFromCatalog,
    SourceMetadataConflict
}

public sealed record UnresolvedPrinting(Guid LineId, long Copies, UnresolvedReason Reason);
public sealed record ConfirmedSetCount(string SetCode, int DistinctPrintings, long Copies);

public sealed record PrintingReconciliationResult(
    long TotalCopies,
    long ConfirmedCopies,
    long UnresolvedCopies,
    int ConfirmedDistinctPrintings,
    IReadOnlyList<UnresolvedPrinting> UnresolvedLines,
    IReadOnlyDictionary<string, ConfirmedSetCount> ConfirmedBySet);

/// <summary>
/// Joins physical inventory exclusively by explicit printing ID in a trusted catalog.
/// Never guesses identities by name, collector hint, or imported set code.
/// Does not modify the inventory or the catalog.
/// </summary>
public static class PrintingReconciliation
{
    public static PrintingReconciliationResult Reconcile(
        IEnumerable<PhysicalInventoryLine> physicalLines,
        IEnumerable<CatalogPrinting> catalogPrintings)
    {
        ArgumentNullException.ThrowIfNull(physicalLines);
        ArgumentNullException.ThrowIfNull(catalogPrintings);

        var catalog = new Dictionary<Guid, CatalogPrinting>();
        foreach (var printing in catalogPrintings)
        {
            if (printing is null || printing.PrintId == Guid.Empty ||
                string.IsNullOrWhiteSpace(printing.Name) ||
                string.IsNullOrWhiteSpace(printing.SetCode) ||
                string.IsNullOrWhiteSpace(printing.CollectorNumber))
                throw new ArgumentException("Catalog contains an invalid printing identity.", nameof(catalogPrintings));
            if (!catalog.TryAdd(printing.PrintId, printing))
                throw new ArgumentException("Catalog contains a duplicate printing ID.", nameof(catalogPrintings));
        }

        var seenLineIds = new HashSet<Guid>();
        var seenPrintings = new HashSet<Guid>();
        var sets = new Dictionary<string, (HashSet<Guid> Printings, long Copies)>(StringComparer.OrdinalIgnoreCase);
        var unresolved = new List<UnresolvedPrinting>();
        long totalCopies = 0, confirmedCopies = 0, unresolvedCopies = 0;

        foreach (var line in physicalLines)
        {
            if (line is null || line.LineId == Guid.Empty ||
                string.IsNullOrWhiteSpace(line.Name) || string.IsNullOrWhiteSpace(line.Finish) ||
                line.Quantity < 0 || line.PrintId == Guid.Empty)
                throw new ArgumentException("Physical inventory contains an invalid line.", nameof(physicalLines));
            if (!seenLineIds.Add(line.LineId))
                throw new ArgumentException("Physical inventory contains duplicate stable line IDs.", nameof(physicalLines));

            totalCopies = checked(totalCopies + line.Quantity);
            if (line.Quantity == 0) continue; // Zero-quantity tombstones are not owned.

            UnresolvedReason? reason = null;
            CatalogPrinting? matched = null;
            if (line.PrintId is null)
                reason = UnresolvedReason.NoPrintId;
            else if (!catalog.TryGetValue(line.PrintId.Value, out matched))
                reason = UnresolvedReason.PrintIdAbsentFromCatalog;
            else if ((HasValue(line.SetCode) &&
                      !string.Equals(line.SetCode!.Trim(), matched.SetCode.Trim(), StringComparison.OrdinalIgnoreCase)) ||
                     (HasValue(line.CollectorNumber) &&
                      !string.Equals(line.CollectorNumber!.Trim(), matched.CollectorNumber.Trim(), StringComparison.OrdinalIgnoreCase)))
                reason = UnresolvedReason.SourceMetadataConflict;

            if (reason is not null)
            {
                unresolvedCopies = checked(unresolvedCopies + line.Quantity);
                unresolved.Add(new UnresolvedPrinting(line.LineId, line.Quantity, reason.Value));
                continue;
            }

            // Credit only the trusted catalog's set, never the import's set hint.
            var setCode = matched!.SetCode.Trim().ToLowerInvariant();
            if (!sets.TryGetValue(setCode, out var state))
                state = (new HashSet<Guid>(), 0);
            state.Printings.Add(matched.PrintId);
            state.Copies = checked(state.Copies + line.Quantity);
            sets[setCode] = state;
            seenPrintings.Add(matched.PrintId);
            confirmedCopies = checked(confirmedCopies + line.Quantity);
        }

        var bySet = new SortedDictionary<string, ConfirmedSetCount>(StringComparer.Ordinal);
        foreach (var (setCode, state) in sets)
            bySet.Add(setCode, new ConfirmedSetCount(setCode, state.Printings.Count, state.Copies));

        return new PrintingReconciliationResult(totalCopies, confirmedCopies, unresolvedCopies,
            seenPrintings.Count, unresolved.AsReadOnly(),
            new ReadOnlyDictionary<string, ConfirmedSetCount>(bySet));
    }

    private static bool HasValue(string? text) => !string.IsNullOrWhiteSpace(text);
}
