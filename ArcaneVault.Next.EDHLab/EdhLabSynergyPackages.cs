namespace ArcaneVault.Next.EDHLab;

/// <summary>
/// Read-only package view over an already format/identity-filtered local catalog.
/// Group membership is based only on commander and candidate cached card text.
/// No deck changes, network requests, printing claims, or competitive rankings.
/// </summary>
public static class EdhLabSynergyPackages
{
    public sealed record Option(string Name, string Kind, string Why,
        long RecordedCopies, int InDeck, long UnusedOwnedCopies, int MatchStrength,
        string RoleHints);

    public sealed record Package(string Theme, int InDeckCount,
        IReadOnlyList<Option> InDeck, IReadOnlyList<Option> OwnedOptions,
        IReadOnlyList<Option> MissingOptions, string Coverage, string NextStep)
    {
        public bool HasSuggestions => OwnedOptions.Count > 0 || MissingOptions.Count > 0;
    }

    public sealed record Report(IReadOnlyList<Package> Packages,
        int UnknownDeckCopies, string Explanation)
    {
        public bool HasRecognizedThemes => Packages.Count > 0;
    }

    private static string Kind(EdhLabDraftCatalog.Card card)
    {
        var o = card.Oracle;
        // Descriptors are intentionally modest: Oracle substring cues do not
        // establish a working combo, a beneficial trigger, or a power level.
        if (o.Contains("whenever", StringComparison.OrdinalIgnoreCase) ||
            o.Contains("for each", StringComparison.OrdinalIgnoreCase) ||
            o.Contains("you control get", StringComparison.OrdinalIgnoreCase))
            return "Potential payoff";
        if (o.Contains("create", StringComparison.OrdinalIgnoreCase) ||
            o.Contains("proliferate", StringComparison.OrdinalIgnoreCase) ||
            o.Contains("put a", StringComparison.OrdinalIgnoreCase) ||
            o.Contains("mill ", StringComparison.OrdinalIgnoreCase))
            return "Potential enabler";
        return "Supporting match";
    }

    private static string RoleCoverage(
        IEnumerable<(EdhLabDraftCatalog.Card Card, int InDeck)> selected)
    {
        var counts = selected.Where(x => x.InDeck > 0)
            .GroupBy(x => Kind(x.Card))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.InDeck),
                StringComparer.OrdinalIgnoreCase);
        return $"In-deck text signals: {counts.GetValueOrDefault("Potential enabler")} potential enablers, " +
            $"{counts.GetValueOrDefault("Potential payoff")} potential payoffs, " +
            $"{counts.GetValueOrDefault("Supporting match")} supporting matches.";
    }

    private static string NextStep(
        IReadOnlyList<(EdhLabDraftCatalog.Card Card, int InDeck, long Unused)> selected)
    {
        bool hasEnabler = selected.Any(x => x.InDeck > 0 && Kind(x.Card) == "Potential enabler");
        bool hasPayoff = selected.Any(x => x.InDeck > 0 && Kind(x.Card) == "Potential payoff");
        if (!hasEnabler && selected.Any(x => x.Unused > 0 &&
                x.InDeck == 0 && Kind(x.Card) == "Potential enabler"))
            return "Review an owned potential enabler; none is recognized in the current draft.";
        if (!hasPayoff && selected.Any(x => x.Unused > 0 &&
                x.InDeck == 0 && Kind(x.Card) == "Potential payoff"))
            return "Review an owned potential payoff; none is recognized in the current draft.";
        if (!hasEnabler || !hasPayoff)
            return "No complete enabler/payoff pairing is established by these text hints; inspect individual cards.";
        return "Both potential enabler and payoff signals appear in the draft; verify actual interactions manually.";
    }

    public static Report Analyze(EdhLabDraftCatalog catalog,
        IEnumerable<(string Name, int Quantity)> mainboard, int optionsPerGroup = 5)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(mainboard);
        if (optionsPerGroup is < 1 or > 20)
            throw new ArgumentOutOfRangeException(nameof(optionsPerGroup));

        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int unknown = 0;
        foreach (var (name, quantity) in mainboard)
        {
            if (quantity is < 1 or > 250)
                throw new ArgumentOutOfRangeException(nameof(mainboard));
            if (string.IsNullOrWhiteSpace(name) || !catalog.Contains(name))
            {
                unknown = checked(unknown + quantity);
                continue;
            }
            counts[name] = checked(counts.GetValueOrDefault(name) + quantity);
        }

        if (catalog.Synergy.ThemeCount == 0)
            return new Report(Array.Empty<Package>(), unknown,
                "No recognized commander-text mechanics are available in the cached catalog. " +
                "Sync card details or review your commander; no archetype was invented.");

        // Catalog.Search is bounded by design; it must NOT be used here or
        // unowned candidates past the first page could be silently lost.
        // Its complete filtered snapshot remains local, with no new SQL calls.
        var available = catalog.AllEligibleCards.ToArray();
        var packages = new List<Package>();
        foreach (string theme in catalog.Synergy.Themes)
        {
            var options = available.Select(card =>
            {
                var fit = catalog.Synergy.EvaluateTheme(theme, card.Type, card.Oracle);
                int inDeck = counts.GetValueOrDefault(card.Name);
                long unused = Math.Max(0, Math.Min(
                    card.RecordedCopies - inDeck,
                    (long)card.SuggestedCopyLimit - inDeck));
                return (Card: card, Fit: fit, InDeck: inDeck, Unused: unused);
            })
                .Where(x => x.Fit.Matches)
                .OrderByDescending(x => x.Fit.Score)
                .ThenBy(x => x.Card.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            Option Convert((EdhLabDraftCatalog.Card Card,
                EdhLabSynergyAdvisor.Fit Fit, int InDeck, long Unused) x) =>
                new(x.Card.Name, Kind(x.Card), x.Fit.Why,
                    x.Card.RecordedCopies, x.InDeck, x.Unused,
                    x.Fit.Score, x.Card.RoleHints);

            // Do not offer already-in-deck names as new singleton additions.
            // Missing means genuinely no recorded copies, not simply exhausted
            // ownership or copy allowance.
            var inDeckCards = options.Where(x => x.InDeck > 0)
                .Take(optionsPerGroup).Select(Convert).ToArray();
            var owned = options.Where(x => x.InDeck == 0 && x.Unused > 0)
                .Take(optionsPerGroup).Select(Convert).ToArray();
            var missing = options.Where(x => x.InDeck == 0 && x.Card.RecordedCopies == 0)
                .Take(optionsPerGroup).Select(Convert).ToArray();

            packages.Add(new Package(theme,
                options.Where(x => x.InDeck > 0).Sum(x => x.InDeck),
                inDeckCards, owned, missing,
                RoleCoverage(options.Select(x => (x.Card, x.InDeck))),
                NextStep(options.Select(x => (x.Card, x.InDeck, x.Unused)).ToArray())));
        }
        return new Report(packages, unknown,
            "Commander mechanic packages are cached-text hints, not verified combos or upgrades. " +
            "Owned counts are name-based; physical printings remain unverified. " +
            "Suggestions are filtered for this deck's format, color identity and Arena availability. " +
            "No cards, collection quantities, or saved decks were changed.");
    }
}
