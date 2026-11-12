using System.Text.RegularExpressions;
using ArcaneVault.Next.Domain;

namespace ArcaneVault.Next.EDHLab;

/// <summary>
/// Arena-style suggested basic-land balancing for an UNSAVED Commander/Brawl
/// draft. Counts already-added lands first, then fills the remaining land
/// target with an even spread of colored mana access. Never touches a user's
/// collection or changes non-managed card lines.
/// </summary>
public static class EdhLabManaPlanner
{
    private const string ColorOrder = "WUBRG";
    private static readonly string[] Basics =
        ["Plains", "Island", "Swamp", "Mountain", "Forest"];
    private static readonly Regex AddClause = new(
        @"\badd\b(?<mana>[^.\r\n]{0,160})",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(50));
    private static readonly Regex ColorSymbol = new(
        @"\{([WUBRG])\}", RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
    private static readonly Regex MainboardLine = new(
        @"^\s*[0-9]{1,4}\s+(.+?)\s*$",
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));

    public sealed record Plan(int TargetLands, int CurrentLands,
        int FixedLands, int FlexibleLands, int UnclassifiedLands,
        int DesiredBasics, int TotalAfter, int CopiesAfter,
        IReadOnlyList<(string Name, int Quantity)> Basics,
        IReadOnlyList<string> UnverifiedCards,
        IReadOnlyDictionary<string, double> ApproximateColoredSources,
        IReadOnlyDictionary<string, double> ColoredPipDemand,
        int ParsedManaCostCopies, int MissingManaCostCopies,
        bool UsingSpellPips, bool ManaCostsAvailable, bool WantsSpellPips,
        IReadOnlyDictionary<string, double> CurrentColoredSources)
    {
        public bool CanApply => UnverifiedCards.Count == 0 && CopiesAfter <= 99;

        public string Summary
        {
            get
            {
                string parts = string.Join(" · ", Basics
                    .Where(x => x.Quantity > 0)
                    .Select(x => x.Quantity + " " + x.Name));
                string sources = ApproximateColoredSources.Count == 0 ? "Colorless" :
                    string.Join(" · ", ApproximateColoredSources
                        .Select(x => x.Key + " ~" + x.Value.ToString("0.#")));
                string pipSummary = string.Join(" · ",
                    ColoredPipDemand.Where(x => x.Value > 0)
                        .Select(x => x.Key + " " + x.Value.ToString("0.#")));
                string demandNote = !WantsSpellPips
                    ? "Even-color mode (spell costs not weighted)."
                    : !ManaCostsAvailable
                        ? "Even-color fallback: Full Sync needed to add mana costs to this older catalog."
                        : UsingSpellPips
                            ? $"Spell-pip mode: {pipSummary} ({ParsedManaCostCopies} costed nonland copies; smoothed ratios)."
                            : "Even-color fallback: no colored spell symbols parsed in this draft.";
                return $"Land plan: {TotalAfter}/{TargetLands} target " +
                    $"({FixedLands} fixed, {DesiredBasics} suggested basics)" +
                    (parts.Length > 0 ? "\nBasics: " + parts : "") +
                    $"\nApprox. colored sources: {sources}" +
                    $"\n{demandNote}" +
                    (WantsSpellPips && ManaCostsAvailable && MissingManaCostCopies > 0
                        ? $" {MissingManaCostCopies} nonland copies have no cached casting cost."
                        : "") +
                    (FlexibleLands > 0 ? $" · {FlexibleLands} flexible land(s)" : "") +
                    (UnclassifiedLands > 0
                        ? $"\n{UnclassifiedLands} land(s) with no recognized colored production; still count toward land total."
                        : "") +
                    (TotalAfter < TargetLands
                        ? "\nNot enough space under the 99-card mainboard limit to reach the target."
                        : "") +
                    (FixedLands > TargetLands
                        ? "\nFixed lands exceed target; they will not be removed."
                        : "") +
                    (UnverifiedCards.Count > 0
                        ? $"\nAutomatic balancing paused: {UnverifiedCards.Count} names have no eligible local catalog match."
                        : "") +
                    "\nEstimated access only; hybrid costs, fetchlands, conditional sources and commander recasts are approximate.";
            }
        }
    }

    private static string? ManagedBasic(string name, int colors)
    {
        for (int i = 0; i < Basics.Length; i++)
            if ((colors & (1 << i)) != 0 &&
                name.Equals(Basics[i], StringComparison.OrdinalIgnoreCase))
                return Basics[i];
        return colors == 0 && name.Equals("Wastes",
            StringComparison.OrdinalIgnoreCase) ? "Wastes" : null;
    }

    private static int ProductionMask(EdhLabDraftCatalog.Card card, int allowed)
    {
        int mask = 0;
        // A basic land type gives that color of mana even when Oracle text
        // does not explicitly repeat its intrinsic mana ability.
        string type = card.Type;
        int separator = type.IndexOf('—');
        if (separator >= 0)
        {
            string subtypes = type[(separator + 1)..];
            for (int i = 0; i < Basics.Length; i++)
                if (Regex.IsMatch(subtypes,
                        @"\b" + Basics[i] + @"\b",
                        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                        TimeSpan.FromMilliseconds(50)))
                    mask |= 1 << i;
        }
        foreach (Match match in AddClause.Matches(card.Oracle))
        {
            string produced = match.Groups["mana"].Value;
            if (produced.Contains("any color", StringComparison.OrdinalIgnoreCase))
                mask |= allowed;
            foreach (Match mana in ColorSymbol.Matches(produced))
            {
                int i = ColorOrder.IndexOf(
                    char.ToUpperInvariant(mana.Groups[1].Value[0]));
                if (i >= 0) mask |= 1 << i;
            }
        }
        return mask & allowed;
    }

    public static Plan Analyze(EdhLabDraftCatalog catalog,
        IEnumerable<(string Name, int Quantity)> entries, string commanderColors,
        int targetLands = 36, bool weightSpellPips = false)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(entries);
        if (targetLands is < 0 or > 99)
            throw new ArgumentOutOfRangeException(nameof(targetLands));
        int allowed = EdhLabCommanderRules.ColorBits(commanderColors);
        int fixedLands = 0, currentLands = 0, fixedCopies = 0;
        int flexible = 0, unclassified = 0;
        double[] coverage = new double[5];
        double[] currentBasics = new double[5];
        double[] pipDemand = new double[5];
        int parsedCostCopies = 0, missingCostCopies = 0;
        var unverified = new List<string>();

        foreach (var entry in entries)
        {
            if (entry.Quantity is < 1 or > 250)
                throw new ArgumentOutOfRangeException(nameof(entries));
            if (ManagedBasic(entry.Name, allowed) is not null)
            {
                currentLands = checked(currentLands + entry.Quantity);
                for (int i = 0; i < Basics.Length; i++)
                    if ((allowed & (1 << i)) != 0 &&
                        entry.Name.Equals(Basics[i], StringComparison.OrdinalIgnoreCase))
                        currentBasics[i] += entry.Quantity;
                continue;
            }
            fixedCopies = checked(fixedCopies + entry.Quantity);
            var card = catalog.Find(entry.Name);
            if (card is null)
            {
                unverified.Add(entry.Name);
                continue;
            }
            if (!card.Type.Contains("Land", StringComparison.OrdinalIgnoreCase))
            {
                if (card.ManaCost.Length > 0)
                {
                    parsedCostCopies = checked(parsedCostCopies + entry.Quantity);
                    double[] colorsInCost = EdhLabColoredPips.Count(card.ManaCost);
                    for (int i = 0; i < 5; i++)
                        if ((allowed & (1 << i)) != 0)
                            pipDemand[i] += colorsInCost[i] * entry.Quantity;
                }
                else missingCostCopies = checked(missingCostCopies + entry.Quantity);
                continue;
            }

            currentLands = checked(currentLands + entry.Quantity);
            fixedLands = checked(fixedLands + entry.Quantity);
            int producedMask = ProductionMask(card, allowed);
            int count = System.Numerics.BitOperations.PopCount((uint)producedMask);
            if (count > 1) flexible += entry.Quantity;
            if (count == 0) unclassified += entry.Quantity;
            if (count > 0)
                for (int i = 0; i < 5; i++)
                    if ((producedMask & (1 << i)) != 0)
                        coverage[i] += (double)entry.Quantity / count;
        }

        int slots = Math.Max(0, Math.Min(
            targetLands - fixedLands, 99 - fixedCopies));
        int[] basicCounts = new int[allowed == 0 ? 1 : 5];
        int colorCount = System.Numerics.BitOperations.PopCount((uint)allowed);
        double totalPips = pipDemand.Sum();
        bool usingPips = weightSpellPips && catalog.HasManaCostMetadata &&
            allowed != 0 && totalPips > 0;
        double[] shares = new double[5];
        if (allowed != 0)
            for (int i = 0; i < 5; i++)
                if ((allowed & (1 << i)) != 0)
                    // A modest prior keeps colors with no listed pips from
                    // being starved, especially in small/incomplete drafts.
                    shares[i] = usingPips
                        ? (pipDemand[i] + 2.0) / (totalPips + 2.0 * colorCount)
                        : 1.0 / colorCount;
        double totalColoredAccess = slots + coverage.Sum();
        if (allowed == 0)
            basicCounts[0] = slots;
        else for (int n = 0; n < slots; n++)
        {
            int best = -1;
            double largestDeficit = double.NegativeInfinity;
            for (int i = 0; i < 5; i++)
            {
                if ((allowed & (1 << i)) == 0) continue;
                double deficit = shares[i] * totalColoredAccess -
                    (coverage[i] + basicCounts[i]);
                if (best < 0 || deficit > largestDeficit + 0.00001)
                {
                    best = i;
                    largestDeficit = deficit;
                }
            }
            if (best < 0) throw new InvalidDataException("No commander colors available.");
            basicCounts[best]++;
        }

        var basics = allowed == 0
            ? new[] { (Name: "Wastes", Quantity: basicCounts[0]) }
            : Enumerable.Range(0, 5)
                .Where(i => (allowed & (1 << i)) != 0)
                .Select(i => (Name: Basics[i], Quantity: basicCounts[i]))
                .ToArray();
        var sources = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        if (allowed != 0)
            for (int i = 0; i < 5; i++)
                if ((allowed & (1 << i)) != 0)
                    sources[ColorOrder[i].ToString()] =
                        coverage[i] + basicCounts[i];

        var currentSources = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < 5; i++)
            if ((allowed & (1 << i)) != 0)
                currentSources[ColorOrder[i].ToString()] = coverage[i] + currentBasics[i];
        var demand = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < 5; i++)
            if ((allowed & (1 << i)) != 0)
                demand[ColorOrder[i].ToString()] = pipDemand[i];
        return new Plan(targetLands, currentLands, fixedLands, flexible,
            unclassified, slots, fixedLands + slots, fixedCopies + slots,
            basics, unverified, sources, demand, parsedCostCopies,
            missingCostCopies, usingPips, catalog.HasManaCostMetadata,
            weightSpellPips, currentSources);
    }

    /// <summary>
    /// Replaces ONLY the normal basic-land lines for commander colors,
    /// preserving every other raw line, including snow lands, utility lands,
    /// spelling, ordering, split names, and unknown metadata for normal saves.
    /// Invalid/unverified inputs cannot be silently normalized or dropped.
    /// </summary>
    public static string Apply(string original, string commanderColors, Plan plan)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(plan);
        if (!plan.CanApply)
            throw new InvalidDataException(
                "Verify unknown cards and the 99-card limit before balancing basics.");
        int colors = EdhLabCommanderRules.ColorBits(commanderColors);
        var lines = original.Replace("\r\n", "\n")
            .Replace('\r', '\n').Split('\n');
        var kept = new List<string>();
        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            Match match = MainboardLine.Match(line);
            if (!match.Success)
                throw new InvalidDataException("Correct the deck list before balancing.");
            if (ManagedBasic(match.Groups[1].Value.Trim(), colors) is null)
                kept.Add(line);
        }
        kept.AddRange(plan.Basics.Where(x => x.Quantity > 0)
            .Select(x => x.Quantity + " " + x.Name));
        return string.Join("\n", kept);
    }
}
