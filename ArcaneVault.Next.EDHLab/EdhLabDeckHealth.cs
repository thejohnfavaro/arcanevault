namespace ArcaneVault.Next.EDHLab;

/// <summary>
/// Read-only, format-filtered deck-health checklist. Counts and targets are
/// heuristic coverage signals, never a single numeric deck-power score.
/// Reuses the existing local role blueprint and cached mana-value optimizer.
/// No collection, network, card, or saved deck changes are made.
/// </summary>
public static class EdhLabDeckHealth
{
    public sealed record Area(string Key, string Title, int Copies,
        int? SuggestedTarget, string State, string Explanation,
        string BrowseRole, string CandidateName, long CandidateUnused)
    {
        public bool CanBrowse => CandidateUnused > 0 && CandidateName.Length > 0;
        public string Display => Title + "  ·  " +
            (SuggestedTarget is int target
                ? $"{Copies}/{target} suggested" : $"{Copies} signals") +
            "  ·  " + State;
    }

    public sealed record Report(int MainboardCopies, int UnknownCopies,
        int MissingOracleCopies, int SynergyCopies,
        EdhLabStrategyFocus Focus, bool SafeToSuggest,
        IReadOnlyList<Area> Areas, string ReviewNext)
    {
        public string Headline => "Deck health · " + MainboardCopies +
            "/99 mainboard · " + EdhLabStrategyGoals.Label(Focus);
        public string Explanation =>
            (UnknownCopies > 0
                ? $"{UnknownCopies} draft copies are unverified or format-ineligible. " +
                  "Collection-aware browsing is paused until the draft is corrected. "
                : MainboardCopies > 99
                    ? "Mainboard exceeds 99; correct draft size before following advice. "
                    : MainboardCopies < 60
                        ? "Partial deck: these are construction checkpoints, not diagnoses. "
                        : "Broad deck-composition checks, not a power or win-rate grade. ") +
            (MissingOracleCopies > 0
                ? $"{MissingOracleCopies} copies lack cached Oracle text, so roles and synergy may be undercounted. "
                : "") +
            "Cards can count toward multiple roles. Targets are adjustable design starting points, " +
            "not Commander format requirements or verified playability. No cards were changed.";
    }

    private static int Priority(EdhLabStrategyFocus focus, string role) => focus switch
    {
        EdhLabStrategyFocus.FastStart => role switch
        {
            "Early" => 0, "Ramp" => 1, "Land" => 2, "Card Draw" => 3,
            _ => 10
        },
        EdhLabStrategyFocus.CardAdvantage => role switch
        {
            "Card Draw" => 0, "Early" => 1, "Ramp" => 2, _ => 10
        },
        EdhLabStrategyFocus.Interaction => role switch
        {
            "Removal" => 0, "Protection" => 1, "Board Wipe" => 2,
            _ => 10
        },
        EdhLabStrategyFocus.CommanderSynergy => role switch
        {
            "Synergy" => 0, "Card Draw" => 1, "Ramp" => 2,
            _ => 10
        },
        _ => role switch
        {
            "Land" => 0, "Ramp" => 1, "Card Draw" => 2,
            "Removal" => 3, "Board Wipe" => 4, "Protection" => 5,
            "Early" => 6, "Synergy" => 7, _ => 10
        }
    };

    public static Report Analyze(EdhLabDraftCatalog catalog,
        IEnumerable<(string Name, int Quantity)> mainboard,
        int landTarget = 36,
        EdhLabStrategyFocus focus = EdhLabStrategyFocus.Balanced,
        EdhLabDeckOptimizer.Report? curve = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(mainboard);
        if (landTarget is < 20 or > 60)
            throw new ArgumentOutOfRangeException(nameof(landTarget));
        if (!Enum.IsDefined(focus))
            throw new ArgumentOutOfRangeException(nameof(focus));
        var draft = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, qty) in mainboard)
        {
            if (qty is < 1 or > 250)
                throw new ArgumentOutOfRangeException(nameof(mainboard));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Blank draft card name.", nameof(mainboard));
            draft[name] = checked(draft.GetValueOrDefault(name) + qty);
        }
        var pairs = draft.Select(x => (Name: x.Key, Quantity: x.Value)).ToArray();
        var blueprint = EdhLabDeckBlueprint.Analyze(catalog, pairs, landTarget);
        var optimizer = curve ?? EdhLabDeckOptimizer.Analyze(catalog, pairs, focus: focus);
        int unknown = blueprint.UnverifiedCopies;
        int missingOracle = pairs.Sum(x =>
        {
            var card = catalog.Find(x.Name);
            return card is not null && string.IsNullOrWhiteSpace(card.Oracle)
                ? x.Quantity : 0;
        });
        int synergy = pairs.Sum(x =>
            catalog.SynergyFor(x.Name)?.Matches == true ? x.Quantity : 0);
        bool safe = unknown == 0 && blueprint.MainboardCopies <= 99;

        // Candidate search is confined to one already identity-, legality-
        // and format-filtered local snapshot. Never recommend a spent copy.
        (string Name, long Unused) FindCandidate(string role)
        {
            if (!safe) return ("", 0);
            var candidates = catalog.AllEligibleCards
                .Where(x => x.RecordedCopies > draft.GetValueOrDefault(x.Name) &&
                    x.SuggestedCopyLimit > draft.GetValueOrDefault(x.Name) &&
                    (role == "Synergy"
                        ? catalog.SynergyFor(x.Name)?.Matches == true
                        : role == "Early"
                            ? catalog.HasManaValueMetadata &&
                              !x.Type.Contains("Land", StringComparison.OrdinalIgnoreCase) &&
                              double.IsFinite(x.ManaValue) &&
                              x.ManaValue is >= 0 and <= 2
                            : x.RoleHints.Split(" · ", StringSplitOptions.RemoveEmptyEntries)
                                .Contains(role, StringComparer.OrdinalIgnoreCase)))
                .Select(x =>
                {
                    var tokens = x.RoleHints.Split(" · ",
                        StringSplitOptions.RemoveEmptyEntries)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                    var fit = catalog.SynergyFor(x.Name);
                    return new
                    {
                        Card = x,
                        Bonus = EdhLabStrategyGoals.Bias(focus, tokens,
                            fit?.Score ?? 0, x.ManaValue),
                        Fit = fit?.Score ?? 0,
                        Unused = Math.Min(x.RecordedCopies -
                            draft.GetValueOrDefault(x.Name),
                            (long)x.SuggestedCopyLimit -
                            draft.GetValueOrDefault(x.Name))
                    };
                })
                .OrderByDescending(x => x.Bonus)
                .ThenByDescending(x => x.Fit)
                .ThenBy(x => role == "Land" ? 0.0 : x.Card.ManaValue)
                .ThenBy(x => x.Card.Name, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            return candidates is null ? ("", 0) :
                (candidates.Card.Name, candidates.Unused);
        }

        var areas = new List<Area>();
        foreach (var role in blueprint.Roles)
        {
            string state = !safe ? "Review unavailable"
                : role.Shortfall == 0 ? "At starting target"
                : blueprint.MainboardCopies < 60 ? "Draft building"
                : "Below suggested target";
            string why = role.Role switch
            {
                "Land" => "Mana-source count only. Color pips, untapped sources, and land quality are reviewed separately in Mana.",
                "Ramp" => "Mana acceleration and extra-mana role hints. Normal mana-producing lands do not count as ramp.",
                "Card Draw" => "Card-access hints. A draw trigger's frequency and card quality are not simulated.",
                "Removal" => "Spot interaction text cues; counterspells and unusual interaction may not be detected.",
                "Board Wipe" => "Broad reset cues. The right number depends on your board-centric strategy.",
                "Protection" => "Hexproof, indestructible and ward hints. Resilience includes recursion and other tools not counted here.",
                _ => "Cached-text role estimate."
            };
            var c = role.Shortfall > 0 ? FindCandidate(role.Role) : ("", 0L);
            areas.Add(new Area(role.Role, role.Label, role.Copies,
                role.SuggestedMinimum, state, why, role.Role, c.Item1, c.Item2));
        }

        string earlyState = !catalog.HasManaValueMetadata
            ? "Mana values unavailable"
            : optimizer.UnknownCopies > 0 ? "Metadata incomplete"
            : optimizer.KnownNonlandCopies < 25 ? "Sample too small"
            : optimizer.EarlySpellCopies * 4 < optimizer.KnownNonlandCopies
                ? "Thin early options" : "Within broad band";
        var earlyPick = earlyState == "Thin early options"
            ? FindCandidate("Early") : ("", 0L);
        areas.Add(new Area("Early", "Early plays (MV 0–2)",
            optimizer.EarlySpellCopies, null, earlyState,
            $"Of {optimizer.KnownNonlandCopies} known nonland copies, " +
            $"{optimizer.ExpensiveSpellCopies} cost MV 6+. " +
            "Curve alone cannot predict opening hands; use Mana to inspect actual sources.",
            "", earlyPick.Item1, earlyPick.Item2));

        string synergyState = catalog.Synergy.ThemeCount == 0
            ? "Commander text not recognized"
            : synergy == 0 ? "No direct matches recognized" : "Matches observed";
        var synergyPick = synergy == 0 && catalog.Synergy.ThemeCount > 0
            ? FindCandidate("Synergy") : ("", 0L);
        areas.Add(new Area("Synergy", "Commander mechanic signals",
            synergy, null, synergyState,
            "Direct cached commander-text matches only. This is not a coherence, " +
            "combo, resilience or power score. See Synergy Chains for specific cues.",
            "", synergyPick.Item1, synergyPick.Item2));

        // Prioritize gaps within the user's selected strategy. Never call a
        // partial deck deficient or recommend a forced cut based on ratios.
        var focusOrder = areas.OrderBy(x => Priority(focus, x.Key))
            .ThenByDescending(x => x.SuggestedTarget is int target &&
                target > 0 ? (double)Math.Max(0, target - x.Copies) / target : 0)
            .ToArray();
        var next = !safe
            ? "Correct unknown/ineligible cards or an overfull draft before browsing suggestions."
            : focusOrder.FirstOrDefault(x =>
                x.SuggestedTarget is int t && x.Copies < t &&
                x.CanBrowse) is { } gap
                ? $"Review owned {gap.Title.ToLowerInvariant()} option: {gap.CandidateName} " +
                  $"({gap.CandidateUnused} unused recorded). Manual choice only."
                : focus == EdhLabStrategyFocus.CommanderSynergy &&
                  synergyPick.Item2 > 0
                    ? $"Review owned commander-mechanic match: {synergyPick.Item1}. " +
                      "Read the full card text before choosing a slot."
                    : "No recorded eligible suggestion found for a tracked gap. " +
                      "Targets are advisory; adjust the deck to your game plan.";

        return new Report(blueprint.MainboardCopies, unknown, missingOracle,
            synergy, focus, safe, areas, next);
    }
}
