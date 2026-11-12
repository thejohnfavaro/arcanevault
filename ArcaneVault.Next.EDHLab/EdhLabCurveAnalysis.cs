using System.Text.RegularExpressions;

namespace ArcaneVault.Next.EDHLab;

/// <summary>
/// Read-only, locally computed EDH Lab deck curve and opening-draw estimates.
/// Odds assume a 99-card mainboard, a seven-card opener without mulligans, and
/// one extra draw per turn beginning on turn two. No spells, fetches, ramp,
/// tapped-land timing, alternate costs or commander recasts are simulated.
/// </summary>
public static class EdhLabCurveAnalysis
{
    private const int DeckSize = 99;
    private const string Colors = "WUBRG";
    private static readonly Regex StrictColoredSymbols = new(
        @"\{([WUBRG])\}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(50));
    private static readonly double[] LogFactorials = BuildLogFactorials();

    public sealed record LandDrop(int Turn, int RequiredLands, double Chance);
    public sealed record ColorPressure(string Name, string Color,
        int RequiredSources, int ByTurn, double ApproximateSources,
        double MarginalChance);

    public sealed record Report(int MainboardCopies, int VerifiedNonlandCopies,
        int UnverifiedCopies, int ManaCostCopies, int MissingCostCopies,
        double AverageManaValue, IReadOnlyList<int> Buckets,
        int LandCountForOdds, bool UsesActualLands,
        IReadOnlyList<LandDrop> LandDrops,
        IReadOnlyList<ColorPressure> ColorWarnings,
        bool HasManaCosts)
    {
        public string Headline =>
            $"Curve: {VerifiedNonlandCopies} known nonland copies · " +
            $"avg MV {AverageManaValue:0.0} · " +
            (LandDrops.Count > 0
                ? $"T3 {LandDrops.First(x => x.Turn == 3).Chance:P0} land-drop estimate"
                : "land-drop odds unavailable");

        public string Display
        {
            get
            {
                var rows = new List<string>
                {
                    $"Nonland mana curve · {VerifiedNonlandCopies} verified copies, average MV {AverageManaValue:0.0}"
                };
                for (int i = 0; i < Buckets.Count; i++)
                {
                    string label = i == 6 ? "6+" : i.ToString();
                    string bar = new string('▮', Math.Min(Buckets[i], 22));
                    rows.Add($"{label,2}  {Buckets[i],3}  {bar}");
                }
                rows.Add(HasManaCosts
                    ? $"Colored casting costs found for {ManaCostCopies} spell copies" +
                      (MissingCostCopies > 0 ? $" · {MissingCostCopies} have no cost metadata" : "")
                    : "Full Sync is needed for colored casting-pressure estimates.");
                if (UnverifiedCopies > 0)
                    rows.Add($"{UnverifiedCopies} deck copies have no eligible local card metadata.");
                if (LandDrops.Count > 0)
                {
                    rows.Add((UsesActualLands
                        ? $"Land-drop odds · {LandCountForOdds} lands in current 99-card mainboard"
                        : $"Projected land-drop odds · {LandCountForOdds} planned lands in a hypothetical 99-card deck") +
                        " (no mulligans):");
                    rows.Add(string.Join("  ·  ", LandDrops.Select(d =>
                        $"T{d.Turn} ≥{d.RequiredLands}: {d.Chance:P0}")));
                    rows.Add("Model: 7-card opener, 1 draw/turn from turn two; natural land drops only.");
                }
                else
                    rows.Add("Land-drop odds unavailable: fix the mainboard/land plan first.");
                if (ColorWarnings.Count > 0)
                {
                    rows.Add("Potential colored-pip pressure (marginal source odds, NOT cast odds):");
                    foreach (var warning in ColorWarnings.Take(3))
                        rows.Add($"• {warning.Name}: {warning.RequiredSources} {warning.Color} symbols " +
                            $"by T{warning.ByTurn} · ~{warning.ApproximateSources:0.#} " +
                            $"source-equivalents · ~{warning.MarginalChance:P0} to draw enough.");
                }
                else if (HasManaCosts && ManaCostCopies > 0)
                    rows.Add("No low-coverage, strict-color spell costs detected in this draft.");
                rows.Add("Approximation only: multicolor land choices, hybrid payments, " +
                    "ramp, tapped lands, tutors, mulligans and simultaneous colors are not simulated.");
                return string.Join("\n", rows);
            }
        }
    }

    private static double[] BuildLogFactorials()
    {
        var values = new double[DeckSize + 1];
        for (int i = 2; i < values.Length; i++)
            values[i] = values[i - 1] + Math.Log(i);
        return values;
    }

    private static double LogChoose(int n, int k) =>
        k < 0 || k > n || n < 0 || n > DeckSize
            ? double.NegativeInfinity
            : LogFactorials[n] - LogFactorials[k] - LogFactorials[n - k];

    /// <summary>
    /// P(X >= minimum) for X hypergeometric from a shuffled 99-card deck.
    /// Uses log-factorials for numerical stability, no Monte Carlo randomness.
    /// </summary>
    public static double ProbabilityAtLeast(int available, int cardsSeen, int minimum)
    {
        if (available is < 0 or > DeckSize ||
            cardsSeen is < 0 or > DeckSize)
            throw new ArgumentOutOfRangeException(nameof(available),
                "Source and sample counts must fit a 99-card deck.");
        if (minimum <= 0) return 1.0;
        if (minimum > available || minimum > cardsSeen) return 0.0;
        double result = 0;
        for (int matches = minimum;
             matches <= Math.Min(available, cardsSeen); matches++)
        {
            double probability = Math.Exp(
                LogChoose(available, matches) +
                LogChoose(DeckSize - available, cardsSeen - matches) -
                LogChoose(DeckSize, cardsSeen));
            result += probability;
        }
        return Math.Clamp(result, 0.0, 1.0);
    }

    public static Report Analyze(EdhLabDraftCatalog catalog,
        IEnumerable<(string Name, int Quantity)> mainboard,
        EdhLabManaPlanner.Plan landPlan)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(mainboard);
        ArgumentNullException.ThrowIfNull(landPlan);
        int[] buckets = new int[7];
        int copies = 0, known = 0, unknown = 0, costed = 0, uncosted = 0;
        double manaSum = 0;
        var knownSpells = new List<(EdhLabDraftCatalog.Card Card, int Quantity)>();
        foreach (var entry in mainboard)
        {
            if (entry.Quantity is < 1 or > 250)
                throw new ArgumentOutOfRangeException(nameof(mainboard));
            copies = checked(copies + entry.Quantity);
            var card = catalog.Find(entry.Name);
            if (card is null)
            {
                unknown = checked(unknown + entry.Quantity);
                continue;
            }
            if (card.Type.Contains("Land", StringComparison.OrdinalIgnoreCase))
                continue;
            known = checked(known + entry.Quantity);
            double mv = double.IsFinite(card.ManaValue)
                ? Math.Max(0.0, card.ManaValue) : 0.0;
            int bucket = Math.Min(6, (int)Math.Min(6, Math.Floor(mv)));
            buckets[bucket] = checked(buckets[bucket] + entry.Quantity);
            manaSum += mv * entry.Quantity;
            if (card.ManaCost.Length > 0)
            {
                costed = checked(costed + entry.Quantity);
                knownSpells.Add((card, entry.Quantity));
            }
            else uncosted = checked(uncosted + entry.Quantity);
        }

        // A full deck must use the user's actual current land count, never
        // replace it with a proposed basic-land rebalance. Drafts project a
        // complete 99-card deck using the planner's suggested land total.
        bool actual = copies == DeckSize;
        int lands = actual ? landPlan.CurrentLands : landPlan.TotalAfter;
        bool validOdds = copies <= DeckSize && lands is >= 0 and <= DeckSize &&
            landPlan.UnverifiedCards.Count == 0;
        var landDrops = new List<LandDrop>();
        if (validOdds)
            for (int turn = 2; turn <= 5; turn++)
                landDrops.Add(new LandDrop(turn, turn,
                    ProbabilityAtLeast(lands, 7 + turn - 1, turn)));

        var selectedSources = actual && landPlan.CurrentColoredSources is not null
            ? landPlan.CurrentColoredSources
            : landPlan.ApproximateColoredSources;
        var warningList = new List<ColorPressure>();
        if (validOdds && catalog.HasManaCostMetadata)
            foreach (var (card, _) in knownSpells)
            {
                int[] strict = new int[5];
                foreach (Match symbol in StrictColoredSymbols.Matches(card.ManaCost))
                {
                    int idx = Colors.IndexOf(char.ToUpperInvariant(
                        symbol.Groups[1].Value[0]));
                    if (idx >= 0) strict[idx]++;
                }
                int turn = Math.Clamp((int)Math.Ceiling(
                    Math.Min(20.0, Math.Max(0.0, card.ManaValue))), 2, 6);
                for (int i = 0; i < 5; i++)
                {
                    if (strict[i] == 0) continue;
                    string color = Colors[i].ToString();
                    double sourceEstimate = selectedSources.GetValueOrDefault(color);
                    // Planner splits flexible lands fractionally, so floor to
                    // a conservative integer for a marginal draw estimate.
                    int sourceCount = Math.Clamp(
                        (int)Math.Floor(sourceEstimate + 0.000001), 0, DeckSize);
                    double chance = ProbabilityAtLeast(
                        sourceCount, 7 + turn - 1, strict[i]);
                    if (chance < 0.60)
                        warningList.Add(new ColorPressure(card.Name, color,
                            strict[i], turn, sourceEstimate, chance));
                }
            }
        return new Report(copies, known, unknown, costed, uncosted,
            known > 0 ? manaSum / known : 0.0, buckets, lands, actual,
            landDrops, warningList.OrderBy(x => x.MarginalChance)
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray(),
            catalog.HasManaCostMetadata);
    }
}
