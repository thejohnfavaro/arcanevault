namespace ArcaneVault.Next.EDHLab;

/// <summary>
/// Read-only, deterministic multi-card substitution planning. Every proposed
/// copy is checked against the evolving draft, so multiple missing slots never
/// claim the same recorded copy. All proposals still require user approval.
/// </summary>
public static class EdhLabAutomaticSubstitutions
{
    public sealed record Shortage(string Name, long Missing);
    public sealed record Swap(string MissingName, string ReplacementName,
        int Score, string Reasons);
    public sealed record Unresolved(string Name, long Copies, string Reason);
    public sealed record Plan(IReadOnlyList<Swap> Swaps,
        IReadOnlyList<Unresolved> Unresolved)
    {
        public string Summary =>
            Swaps.Count == 0 ? "No conservative, owned substitutions found." :
            $"{Swaps.Count:N0} proposed one-for-one swaps; " +
            $"{Unresolved.Sum(x => x.Copies):N0} copies still unresolved.";
    }

    public static Plan Analyze(EdhLabDraftCatalog catalog,
        IEnumerable<(string Name, int Quantity)> mainboard,
        IEnumerable<Shortage> shortages,
        EdhLabStrategyFocus focus = EdhLabStrategyFocus.Balanced,
        int landTarget = 36, int maxSwaps = 100)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(mainboard);
        ArgumentNullException.ThrowIfNull(shortages);
        if (maxSwaps is < 1 or > 250)
            throw new ArgumentOutOfRangeException(nameof(maxSwaps));
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, quantity) in mainboard)
        {
            if (string.IsNullOrWhiteSpace(name) || quantity is < 1 or > 250)
                throw new ArgumentException("Invalid mainboard entry.", nameof(mainboard));
            counts[name] = checked(counts.GetValueOrDefault(name) + quantity);
        }
        var swaps = new List<Swap>();
        var unresolved = new List<Unresolved>();
        // Keep ordering stable and process fewer-available-copy constraints
        // without allowing any candidate to be spent twice.
        foreach (var shortage in shortages.OrderBy(x => x.Name,
                     StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(shortage.Name) || shortage.Missing < 0)
                throw new ArgumentException("Invalid shortage.", nameof(shortages));
            long requested = Math.Min(shortage.Missing,
                counts.GetValueOrDefault(shortage.Name));
            long remaining = requested;
            while (remaining > 0 && swaps.Count < maxSwaps)
            {
                var alternatives = EdhLabMissingCardAdvisor.Analyze(catalog,
                    counts.Select(x => (x.Key, x.Value)), shortage.Name,
                    limit: 8, focus: focus, landTarget: landTarget);
                if (alternatives.Count == 0) break;
                var chosen = alternatives[0];
                // Candidates are drawn only from the catalog's locally
                // eligible inventory and must have a truly unused copy.
                var eligible = catalog.Find(chosen.Name);
                if (eligible is null ||
                    eligible.RecordedCopies <= counts.GetValueOrDefault(chosen.Name) ||
                    eligible.SuggestedCopyLimit <= counts.GetValueOrDefault(chosen.Name))
                    break;
                counts[shortage.Name]--;
                counts[chosen.Name] = checked(counts.GetValueOrDefault(chosen.Name) + 1);
                swaps.Add(new Swap(shortage.Name, chosen.Name,
                    chosen.Score, chosen.RankingReasons));
                remaining--;
            }
            if (remaining > 0)
                unresolved.Add(new Unresolved(shortage.Name, remaining,
                    swaps.Count == maxSwaps ? "Plan limit reached" :
                    "No eligible owned role-preserving alternative remains"));
        }
        return new Plan(swaps, unresolved);
    }
}
