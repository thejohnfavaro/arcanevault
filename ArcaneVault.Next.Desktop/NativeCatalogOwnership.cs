using ArcaneVault.Next.Domain;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// Immutable, read-only ownership index built from a verified native workspace.
/// Physical cards are owned only when their exact Scryfall printing ID AND
/// supplied name/set/collector information agree with the catalog printing.
/// Arena holdings use card names (Arena inventory is name-based).
/// Missing physical IDs stay unresolved, never assigned to another printing.
/// </summary>
public sealed class NativeCatalogOwnership
{
    private readonly Dictionary<Guid, NativePhysicalRow[]> physicalById;
    private readonly Dictionary<string, int> arenaByName;
    private readonly Dictionary<string, int> unresolvedByName;

    public sealed record Counts(int VerifiedPhysical, int Arena, int UnresolvedName)
    {
        public string Label
        {
            get
            {
                var parts = new List<string>();
                if (VerifiedPhysical > 0) parts.Add($"Physical {VerifiedPhysical}");
                if (Arena > 0) parts.Add($"Arena {Arena}");
                if (UnresolvedName > 0) parts.Add($"Unresolved name {UnresolvedName}");
                return parts.Count == 0 ? "Not verified as owned" : string.Join(" · ", parts);
            }
        }
    }

    private NativeCatalogOwnership(
        Dictionary<Guid, NativePhysicalRow[]> physical,
        Dictionary<string, int> arena, Dictionary<string, int> unresolved)
    {
        physicalById = physical;
        arenaByName = arena;
        unresolvedByName = unresolved;
    }

    public static NativeCatalogOwnership From(NativeBackupView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        var byId = new Dictionary<Guid, List<NativePhysicalRow>>();
        var unresolved = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var arena = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in view.PhysicalCards)
        {
            if (row.Quantity <= 0) continue;
            if (Guid.TryParse(row.ScryfallId, out var id) && id != Guid.Empty)
            {
                if (!byId.TryGetValue(id, out var copies))
                    byId[id] = copies = new List<NativePhysicalRow>();
                copies.Add(row);
            }
            else
            {
                // Even a supplied set/collector hint is not enough to claim
                // that a catalog printing is owned without a verified ID.
                unresolved[row.Name] = checked(
                    unresolved.GetValueOrDefault(row.Name) + row.Quantity);
            }
        }
        foreach (var row in view.ArenaCards)
        {
            if (row.Quantity <= 0) continue;
            arena[row.Name] = checked(
                arena.GetValueOrDefault(row.Name) + row.Quantity);
        }

        return new NativeCatalogOwnership(
            byId.ToDictionary(x => x.Key, x => x.Value.ToArray()), arena, unresolved);
    }

    public Counts ForPrinting(string printId, string name,
        string setCode, string collectorNumber)
    {
        int physical = 0;
        if (Guid.TryParse(printId, out Guid guid) &&
            physicalById.TryGetValue(guid, out var candidates))
        {
            foreach (var record in candidates)
            {
                if (!record.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                    (record.SetCode.Length > 0 &&
                     !record.SetCode.Equals(setCode, StringComparison.OrdinalIgnoreCase)) ||
                    (record.CollectorNumber.Length > 0 &&
                     !record.CollectorNumber.Equals(collectorNumber, StringComparison.OrdinalIgnoreCase)))
                    continue;
                physical = checked(physical + record.Quantity);
            }
        }
        return new Counts(physical, arenaByName.GetValueOrDefault(name),
            unresolvedByName.GetValueOrDefault(name));
    }
}
