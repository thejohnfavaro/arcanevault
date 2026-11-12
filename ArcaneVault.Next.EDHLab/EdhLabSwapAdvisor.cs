namespace ArcaneVault.Next.EDHLab;

/// <summary>
/// Read-only, opt-in single-card swap proposals for mature Commander/Brawl
/// drafts. Uses only the already format/color-filtered local candidate pool,
/// recorded name-level inventory, rough role tags and commander-text signals.
/// Never calls a network service, changes the catalog, or mutates a deck.
/// </summary>
public static class EdhLabSwapAdvisor
{
    private static readonly string[] GuardedRoles =
        ["Ramp", "Card Draw", "Removal", "Board Wipe", "Protection"];

    public sealed record Proposal(string OutName, string InName,
        double OutManaValue, double InManaValue,
        string Explanation, string RetainedRoles, string ImprovedRoles,
        long AvailableRecordedCopies, int Score,
        EdhLabStrategyFocus Focus)
    {
        public string Display => $"{OutName} (MV {OutManaValue:0.#})  →  " +
            $"{InName} (MV {InManaValue:0.#})";
    }

    public sealed record Report(int MainboardCopies, int UnknownCopies,
        int KnownNonlandCopies, IReadOnlyList<Proposal> Proposals,
        string Reason, EdhLabStrategyFocus Focus)
    {
        public bool CanSwap => Proposals.Count > 0;

        public string Headline => CanSwap
            ? $"Swap Lab: {Proposals.Count} {EdhLabStrategyGoals.Label(Focus)} proposals"
            : "Swap Lab: " + Reason;

        public string Display => CanSwap
            ? $"Priority: {EdhLabStrategyGoals.Label(Focus)} — " +
              EdhLabStrategyGoals.Description(Focus) + "\n" +
              $"Potential one-for-one edits in this UNSAVED draft. " +
              $"Each proposal preserves protected role targets and does not " +
              $"lose identified commander-mechanic matches.\n" +
              $"{string.Join("\n", Proposals.Take(4).Select(x =>
                  "• " + x.Display))}\n" +
              "Select a suggestion to preview the tradeoff; confirm explicitly " +
              "before changing the unsaved text. Ratings and combinations are " +
              "heuristics, not guaranteed improvements."
            : Reason + " Nothing was changed.";
    }

    private static HashSet<string> Roles(EdhLabDraftCatalog.Card card) =>
        card.RoleHints.Split(" · ", StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static Report Analyze(EdhLabDraftCatalog catalog,
        IEnumerable<(string Name, int Quantity)> mainboard, int landTarget = 36,
        int maxProposals = 8,
        EdhLabStrategyFocus focus = EdhLabStrategyFocus.Balanced)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(mainboard);
        if (landTarget is < 20 or > 60)
            throw new ArgumentOutOfRangeException(nameof(landTarget));
        if (maxProposals is < 1 or > 20)
            throw new ArgumentOutOfRangeException(nameof(maxProposals));

        var entries = mainboard.ToArray();
        int copies = 0, nonland = 0, unknown = 0;
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var expensive = new List<EdhLabDraftCatalog.Card>();
        foreach (var (name, quantity) in entries)
        {
            if (quantity is < 1 or > 250)
                throw new ArgumentOutOfRangeException(nameof(mainboard));
            copies = checked(copies + quantity);
            if (string.IsNullOrWhiteSpace(name))
            {
                unknown = checked(unknown + quantity);
                continue;
            }
            counts[name] = checked(counts.GetValueOrDefault(name) + quantity);
            var card = catalog.Find(name);
            if (card is null)
            {
                unknown = checked(unknown + quantity);
                continue;
            }
            if (card.Type.Contains("Land", StringComparison.OrdinalIgnoreCase))
                continue;
            nonland = checked(nonland + quantity);
            if (catalog.HasManaValueMetadata && card.ManaValue >= 4 &&
                double.IsFinite(card.ManaValue))
                expensive.Add(card);
        }

        string? blocked = !catalog.HasManaValueMetadata
            ? "Full Sync needed to supply reliable mana values."
            : unknown > 0 ? "Verify locally ineligible or unknown card names."
            : copies > 99 ? "Mainboard exceeds 99; correct its size first."
            : nonland < 25
                ? "Add more spells before proposing cuts in this incomplete draft (25 known nonland copies minimum)."
                : null;
        if (blocked is not null)
            return new Report(copies, unknown, nonland, [], blocked, focus);

        var blueprint = EdhLabDeckBlueprint.Analyze(catalog, entries, landTarget);
        var coverage = blueprint.Roles.ToDictionary(x => x.Role, x => x,
            StringComparer.OrdinalIgnoreCase);
        var targets = catalog.RecordedNonlandCards
            .Where(x => double.IsFinite(x.ManaValue) && x.ManaValue is >= 0 and <= 3 &&
                x.RecordedCopies > counts.GetValueOrDefault(x.Name) &&
                x.SuggestedCopyLimit > counts.GetValueOrDefault(x.Name))
            .Select(x => new
            {
                Card = x,
                Roles = Roles(x),
                Synergy = catalog.SynergyFor(x.Name)?.Score ?? 0,
                Unused = x.RecordedCopies - counts.GetValueOrDefault(x.Name)
            })
            // Bounded local work: include role-bearing cards before vanilla
            // ones and never scan an unbounded Cartesian product per edit.
            .OrderByDescending(x => x.Roles.Count)
            .ThenByDescending(x => x.Synergy)
            .ThenBy(x => x.Card.ManaValue)
            .ThenBy(x => x.Card.Name, StringComparer.OrdinalIgnoreCase)
            .Take(600).ToArray();

        var suggestions = new List<Proposal>();
        foreach (var high in expensive.DistinctBy(x => x.Name,
                     StringComparer.OrdinalIgnoreCase))
        {
            var highRoles = Roles(high);
            int oldSynergy = catalog.SynergyFor(high.Name)?.Score ?? 0;
            Proposal? best = null;
            foreach (var option in targets)
            {
                var low = option.Card;
                if (low.Name.Equals(high.Name, StringComparison.OrdinalIgnoreCase))
                    continue;
                double curveDelta = high.ManaValue - low.ManaValue;
                if (curveDelta < 2) continue;
                // Avoid suggesting removal of the commander's identified
                // strategy signals unless the replacement matches as well.
                if (oldSynergy > option.Synergy) continue;
                var retained = highRoles.Intersect(option.Roles,
                    StringComparer.OrdinalIgnoreCase).ToArray();

                // A cut must not worsen any already-low or just-at-target
                // critical role. If the role is safely above target, it may
                // lose one contributor; if not, the replacement must retain it.
                bool losesCriticalCoverage = GuardedRoles.Any(role =>
                {
                    if (!highRoles.Contains(role) || option.Roles.Contains(role))
                        return false;
                    if (!coverage.TryGetValue(role, out var progress))
                        return true;
                    return progress.Copies <= progress.SuggestedMinimum;
                });
                if (losesCriticalCoverage) continue;

                var newlyCovered = option.Roles.Except(highRoles,
                    StringComparer.OrdinalIgnoreCase)
                    .Where(role => coverage.TryGetValue(role, out var p) &&
                        p.Shortfall > 0).ToArray();
                int goalBias = EdhLabStrategyGoals.Bias(
                    focus, option.Roles, option.Synergy, low.ManaValue);
                int score = (int)Math.Min(30, Math.Floor(curveDelta) * 2) +
                    newlyCovered.Length * 15 +
                    retained.Length * 4 +
                    (option.Synergy - oldSynergy) * 4 + goalBias;
                string keep = retained.Length == 0 ? "none identified"
                    : string.Join(", ", retained);
                string improve = newlyCovered.Length == 0 ? "none identified"
                    : string.Join(", ", newlyCovered);
                string description =
                    $"Review removing {high.Name} (MV {high.ManaValue:0.#}) for " +
                    $"{low.Name} (MV {low.ManaValue:0.#}), reducing the slot " +
                    $"by {curveDelta:0.#} mana value. " +
                    $"Preserved role hints: {keep}. " +
                    $"Potential gap coverage: {improve}. " +
                    $"Commander-text synergy score {oldSynergy} → {option.Synergy}. " +
                    $"Selected priority: {EdhLabStrategyGoals.Label(focus)} " +
                    $"({EdhLabStrategyGoals.Description(focus)}). " +
                    $"{option.Unused} additional recorded copy/copies. " +
                    "Check strategic interactions and card text before applying; " +
                    "this does NOT establish a stronger deck.";
                var proposal = new Proposal(high.Name, low.Name, high.ManaValue,
                    low.ManaValue, description, keep, improve, option.Unused, score,
                    focus);
                if (best is null || proposal.Score > best.Score ||
                    (proposal.Score == best.Score &&
                     string.Compare(proposal.InName, best.InName,
                         StringComparison.OrdinalIgnoreCase) < 0))
                    best = proposal;
            }
            if (best is not null) suggestions.Add(best);
        }

        // Retain one suggestion per cut. The target can recur across multiple
        // alternatives; reanalyze on every actual deck edit before applying.
        var ranked = suggestions.OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.OutManaValue)
            .ThenBy(x => x.OutName, StringComparer.OrdinalIgnoreCase)
            .Take(maxProposals).ToArray();
        return new Report(copies, unknown, nonland, ranked,
            ranked.Length > 0 ? "" : "No safe one-for-one cuts found that preserve " +
                "identified strategy and critical role coverage, or no unused " +
                "low-cost eligible cards are recorded in this format.", focus);
    }

    /// <summary>
    /// Ensure the selected pair is still recommended after intervening edits,
    /// before the caller applies it to unsaved editor text. Does not mutate.
    /// </summary>
    public static bool IsCurrent(Report current, Proposal proposal) =>
        proposal.Focus == current.Focus && current.Proposals.Any(x =>
            x.Focus == current.Focus &&
            x.OutName.Equals(proposal.OutName, StringComparison.OrdinalIgnoreCase) &&
            x.InName.Equals(proposal.InName, StringComparison.OrdinalIgnoreCase));
}
