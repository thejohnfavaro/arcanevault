namespace ArcaneVault.Next.EDHLab;

/// <summary>
/// Read-only, collection-aware deck tuning suggestions. Detects broad mana
/// curve shape and surfaces locally legal, recorded early spells not already
/// exhausted by the draft. It never makes swap/power claims or edits cards.
/// </summary>
public static class EdhLabDeckOptimizer
{
    private const int MainboardLimit = 99;

    public sealed record Opportunity(string Code, string Title, string Explanation);
    public sealed record Candidate(string Name, double ManaValue, long UnusedRecordedCopies,
        string Why, int SynergyScore, string RoleHints);

    public sealed record Report(int MainboardCopies, int KnownNonlandCopies,
        int EarlySpellCopies, int ExpensiveSpellCopies, int UnknownCopies,
        bool HasManaValues, bool IsMatureEnoughToJudge,
        IReadOnlyList<Opportunity> Opportunities,
        IReadOnlyList<string> ExpensiveCards,
        IReadOnlyList<Candidate> OwnedCandidates)
    {
        public bool CanBrowse => OwnedCandidates.Count > 0;

        public string Headline => !HasManaValues
            ? "Optimization: mana values unavailable"
            : UnknownCopies > 0
                ? "Optimization: verify unknown deck cards"
                : KnownNonlandCopies == 0
                    ? "Optimization: add spells to evaluate your curve"
                    : $"Optimization: {EarlySpellCopies} early (MV 0–2) · " +
                      $"{ExpensiveSpellCopies} expensive (MV 6+) · " +
                      $"{OwnedCandidates.Count} unused owned candidates";

        public string Display
        {
            get
            {
                if (!HasManaValues)
                    return "This catalog does not include mana values. Run Full Sync " +
                        "before asking for ranked early-game options. No cards were changed.";
                if (UnknownCopies > 0 || MainboardCopies > MainboardLimit)
                    return $"Advice paused: {UnknownCopies} unverified copies, " +
                        $"{MainboardCopies}/99 mainboard. Correct the draft first; no cards were changed.";
                if (KnownNonlandCopies == 0)
                    return "Add nonland spells to the draft to evaluate its early-game " +
                        "curve and suggest eligible cards from your recorded collection.";

                var lines = new List<string>
                {
                    $"Current nonland profile: {EarlySpellCopies}/{KnownNonlandCopies} " +
                        "at MV 0–2 · " +
                        $"{ExpensiveSpellCopies}/{KnownNonlandCopies} at MV 6+.",
                    IsMatureEnoughToJudge
                        ? "Curve thresholds are broad advisory signals; " +
                          "expensive finishers and deliberate strategies may be appropriate."
                        : "Draft still sparse: diagnosing a final curve from this partial " +
                          "spell list would be premature."
                };
                foreach (var opportunity in Opportunities)
                    lines.Add("• " + opportunity.Title + ": " + opportunity.Explanation);
                if (Opportunities.Count == 0 && IsMatureEnoughToJudge)
                    lines.Add("No generic high-curve / low-curve threshold was triggered.");
                if (ExpensiveCards.Count > 0 && Opportunities.Any(x => x.Code == "high-end"))
                    lines.Add("Review costly slots (not automatic cuts): " +
                        string.Join(", ", ExpensiveCards.Take(4)) + ".");
                if (OwnedCandidates.Count == 0)
                    lines.Add("No unused, locally eligible MV 0–2 nonland candidates " +
                        "were recorded in this format's collection.");
                else
                {
                    lines.Add("Recorded, locally eligible early options to review " +
                        "(not guaranteed upgrades):");
                    foreach (var pick in OwnedCandidates.Take(5))
                        lines.Add($"• {pick.Name} (MV {pick.ManaValue:0.#}; " +
                            $"{pick.UnusedRecordedCopies} unused recorded): {pick.Why}");
                    if (MainboardCopies >= MainboardLimit)
                        lines.Add("Mainboard is full: review a possible cut yourself " +
                            "before adding anything.");
                }
                lines.Add("Advisory only. Card role tags and commander-text matches " +
                    "are heuristics; owned-by-name is not printing verification. " +
                    "Nothing is added, removed or saved automatically.");
                return string.Join("\n", lines);
            }
        }
    }

    public static Report Analyze(EdhLabDraftCatalog catalog,
        IEnumerable<(string Name, int Quantity)> mainboard, int maxCandidates = 5)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(mainboard);
        if (maxCandidates is < 1 or > 20)
            throw new ArgumentOutOfRangeException(nameof(maxCandidates));
        var draft = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int copies = 0, nonland = 0, cheap = 0, costly = 0, unknown = 0;
        var expensiveCards = new List<(string Name, double ManaValue)>();
        foreach (var (name, quantity) in mainboard)
        {
            if (quantity is < 1 or > 250)
                throw new ArgumentOutOfRangeException(nameof(mainboard));
            copies = checked(copies + quantity);
            if (string.IsNullOrWhiteSpace(name))
            {
                unknown = checked(unknown + quantity);
                continue;
            }
            draft[name] = checked(draft.GetValueOrDefault(name) + quantity);
            var card = catalog.Find(name);
            if (card is null)
            {
                unknown = checked(unknown + quantity);
                continue;
            }
            if (card.Type.Contains("Land", StringComparison.OrdinalIgnoreCase))
                continue;

            nonland = checked(nonland + quantity);
            if (!catalog.HasManaValueMetadata) continue;
            if (!double.IsFinite(card.ManaValue) || card.ManaValue < 0)
            {
                unknown = checked(unknown + quantity);
                continue;
            }
            if (card.ManaValue <= 2.0) cheap = checked(cheap + quantity);
            if (card.ManaValue >= 6.0)
            {
                costly = checked(costly + quantity);
                expensiveCards.Add((card.Name, card.ManaValue));
            }
        }

        bool canAdvise = catalog.HasManaValueMetadata &&
            unknown == 0 && copies <= MainboardLimit;
        // Restrict strong curve warnings to reasonably filled spell lists.
        bool mature = canAdvise && nonland >= 25;
        var issues = new List<Opportunity>();
        if (mature)
        {
            if (costly >= 8 && costly * 5 > nonland)
                issues.Add(new Opportunity("high-end", "Top-heavy curve",
                    $"{costly} of {nonland} known nonland copies cost 6+ mana " +
                    "(over 20%). Consider reviewing expensive slots, not " +
                    "automatically cutting payoffs or finishers."));
            if (cheap * 4 < nonland)
                issues.Add(new Opportunity("early", "Thin early spell coverage",
                    $"{cheap} of {nonland} known nonland copies cost 0–2 mana " +
                    "(under 25%). More cheap interaction, ramp or card selection " +
                    "may smooth early turns; MV alone cannot prove playability."));
        }

        IReadOnlyList<Candidate> picks = Array.Empty<Candidate>();
        if (canAdvise)
        {
            // The catalog is already filtered by this commander's identity,
            // selected format legality and Arena availability. Never suggest
            // a card whose recorded inventory or copy allowance is exhausted.
            var eligible = catalog.RecordedNonlandCards
                .Where(card => double.IsFinite(card.ManaValue) &&
                    card.ManaValue is >= 0 and <= 2 &&
                    card.RecordedCopies > draft.GetValueOrDefault(card.Name) &&
                    card.SuggestedCopyLimit > draft.GetValueOrDefault(card.Name))
                .Select(card =>
                {
                    var fit = catalog.SynergyFor(card.Name);
                    string[] roles = card.RoleHints.Split(" · ",
                        StringSplitOptions.RemoveEmptyEntries);
                    bool earlyUtility = roles.Any(r => r is
                        "Ramp" or "Card Draw" or "Removal" or "Protection");
                    string why = (roles.Length > 0
                        ? "Role hint: " + string.Join(", ", roles.Take(2))
                        : "Low-mana eligible spell") +
                        (fit is { Matches: true }
                            ? " · Commander mechanic match" : "") +
                        "; inspect the text before considering a slot.";
                    return new
                    {
                        Card = card,
                        Utility = earlyUtility,
                        Score = fit?.Score ?? 0,
                        Why = why,
                        Available = card.RecordedCopies - draft.GetValueOrDefault(card.Name)
                    };
                })
                .OrderByDescending(x => x.Utility)
                .ThenByDescending(x => x.Score)
                .ThenBy(x => x.Card.ManaValue)
                .ThenBy(x => x.Card.Name, StringComparer.OrdinalIgnoreCase)
                .Take(maxCandidates)
                .Select(x => new Candidate(x.Card.Name, x.Card.ManaValue,
                    x.Available, x.Why, x.Score, x.Card.RoleHints))
                .ToArray();
            picks = eligible;
        }

        return new Report(copies, nonland, cheap, costly, unknown,
            catalog.HasManaValueMetadata, mature, issues,
            expensiveCards.OrderByDescending(x => x.ManaValue)
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(), picks);
    }
}
