namespace ArcaneVault.Next.EDHLab;

/// <summary>
/// Read-only, conservative alternatives for a missing MAINBOARD card.
/// The catalog is already filtered for the verified commander, format and
/// color identity; the caller supplies the saved format's separate owned pool.
/// Never treats shared role hints as functional equivalence or guaranteed power.
/// </summary>
public static class EdhLabMissingCardAdvisor
{
    public sealed record Alternative(string Name, long UnusedRecordedCopies,
        string SharedRoles, int Score, string Explanation)
    {
        public string Display =>
            $"{Name} · {UnusedRecordedCopies:N0} unused recorded · {SharedRoles}";
    }

    public static IReadOnlyList<Alternative> Analyze(
        EdhLabDraftCatalog catalog,
        IEnumerable<(string Name, int Quantity)> mainboard,
        string missingName, int limit = 8)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(mainboard);
        if (string.IsNullOrWhiteSpace(missingName)) return [];
        if (limit is < 1 or > 20)
            throw new ArgumentOutOfRangeException(nameof(limit));

        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, qty) in mainboard)
        {
            if (string.IsNullOrWhiteSpace(name) || qty is < 1 or > 250)
                throw new ArgumentException("A valid mainboard is required.", nameof(mainboard));
            counts[name] = checked(counts.GetValueOrDefault(name) + qty);
        }
        // Commander-only shortages are not mainboard cuts. In that case
        // changing commanders needs its own explicit deck editing workflow.
        if (counts.GetValueOrDefault(missingName) < 1) return [];
        var source = catalog.Find(missingName);
        if (source is null) return [];
        var neededRoles = source.RoleHints.Split(" · ",
            StringSplitOptions.RemoveEmptyEntries);
        if (neededRoles.Length == 0) return []; // no defensible match signals
        bool land = source.Type.Contains("Land", StringComparison.OrdinalIgnoreCase);

        return catalog.AllEligibleCards
            .Where(x => x.RecordedCopies > counts.GetValueOrDefault(x.Name) &&
                x.SuggestedCopyLimit > counts.GetValueOrDefault(x.Name) &&
                !x.Name.Equals(missingName, StringComparison.OrdinalIgnoreCase) &&
                x.Type.Contains("Land", StringComparison.OrdinalIgnoreCase) == land)
            .Select(x =>
            {
                var roles = x.RoleHints.Split(" · ",
                    StringSplitOptions.RemoveEmptyEntries);
                // Keep every recognized role of the card being cut. A
                // heuristic cannot certify equivalent card text or effects.
                bool keepsRoles = neededRoles.All(role =>
                    roles.Contains(role, StringComparer.OrdinalIgnoreCase));
                if (!keepsRoles) return null;
                int synergy = catalog.SynergyFor(x.Name)?.Score ?? 0;
                int delta = double.IsFinite(x.ManaValue) &&
                    double.IsFinite(source.ManaValue)
                    ? (int)Math.Min(12, Math.Abs(x.ManaValue - source.ManaValue)) : 0;
                long available = x.RecordedCopies - counts.GetValueOrDefault(x.Name);
                string shared = string.Join(", ", neededRoles);
                return new Alternative(x.Name, available, shared,
                    neededRoles.Length * 20 + synergy * 4 - delta,
                    $"{x.Name} shares identified role hints ({shared}) with " +
                    $"{missingName}; {available:N0} additional copies are recorded " +
                    "in this format's separate inventory pool. Compare actual " +
                    "rules, mana requirements and commander interactions. " +
                    "This is not a verified equivalent or automatic upgrade.");
            })
            .Where(x => x is not null)
            .Select(x => x!)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToArray();
    }
}
