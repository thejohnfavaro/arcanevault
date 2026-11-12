using ArcaneVault.Next.Domain;

namespace ArcaneVault.Next.EDHLab;

/// <summary>
/// Read-only, name-level inventory lookup for commander discovery. Physical
/// names are inventory entries, NOT proof of a catalog-verified printing.
/// Arena ownership is likewise based on recorded Arena names. This view
/// never writes to any native collection, catalog or deck.
/// </summary>
public sealed class CommanderInventory
{
    private readonly IReadOnlyDictionary<string, int> physical;
    private readonly IReadOnlyDictionary<string, int> arena;

    public static CommanderInventory Empty { get; } = new(null);

    public CommanderInventory(NativeBackupView? collection)
    {
        physical = Collect(collection?.PhysicalCards.Select(x => (x.Name, x.Quantity)) ??
            Enumerable.Empty<(string Name, int Quantity)>());
        arena = Collect(collection?.ArenaCards.Select(x => (x.Name, x.Quantity)) ??
            Enumerable.Empty<(string Name, int Quantity)>());
    }

    private static IReadOnlyDictionary<string, int> Collect(
        IEnumerable<(string Name, int Quantity)> rows)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.Name) || row.Quantity <= 0) continue;
            var name = row.Name.Trim();
            result[name] = checked(result.GetValueOrDefault(name) + row.Quantity);
        }
        return result;
    }

    public int Count(string name, EdhLabFormat format)
    {
        if (string.IsNullOrWhiteSpace(name)) return 0;
        return (format == EdhLabFormat.ArenaBrawl ? arena : physical)
            .GetValueOrDefault(name.Trim());
    }

    public IReadOnlyList<EdhLabCommanderCard> Filter(
        IReadOnlyList<EdhLabCommanderCard> eligible, EdhLabFormat format,
        bool inInventoryOnly)
    {
        ArgumentNullException.ThrowIfNull(eligible);
        if (!inInventoryOnly) return eligible;
        return eligible.Where(x => Count(x.Name, format) > 0).ToArray();
    }
}
