namespace ArcaneVault.Next.EDHLab;

/// <summary>
/// A read-only whole-draft counterfactual for ONE proposed mainboard swap.
/// All metadata comes from the already eligible local EDH Lab catalog.
/// Broad role and synergy signals are not simulated win rates or proof of
/// card-text equivalence; no deck, inventory or catalog is written.
/// </summary>
public static class EdhLabSwapImpact
{
    public sealed record RoleChange(string Role, string Label, int Before, int After,
        int SuggestedTarget)
    {
        public int Delta => After - Before;
        public int GapBefore => Math.Max(0, SuggestedTarget - Before);
        public int GapAfter => Math.Max(0, SuggestedTarget - After);
        public string Display =>
            $"{Label}: {Before}/{SuggestedTarget} → {After}/{SuggestedTarget}" +
            (Delta == 0 ? " (unchanged)"
                : Delta > 0 ? $" (+{Delta})" : $" ({Delta})");
    }

    public sealed record Report(
        int MainboardBefore, int MainboardAfter,
        int UnknownBefore, int UnknownAfter,
        int SynergyBefore, int SynergyAfter,
        int MissingOracleBefore, int MissingOracleAfter,
        int KnownManaBefore, int KnownManaAfter,
        double? AverageManaBefore, double? AverageManaAfter,
        IReadOnlyList<RoleChange> Roles,
        bool HasManaValues)
    {
        public int SynergyDelta => SynergyAfter - SynergyBefore;
        public int RolesImproved => Roles.Count(x => x.GapAfter < x.GapBefore);
        public int RolesWorsened => Roles.Count(x => x.GapAfter > x.GapBefore);

        private static string Change(int delta) =>
            delta > 0 ? "+" + delta : delta.ToString();

        private string ManaSummary =>
            !HasManaValues || KnownManaBefore == 0 || KnownManaAfter == 0
                ? "Mana curve: no reliable local mana values; no curve claim."
                : $"Known-card average mana value: {AverageManaBefore:0.##} → " +
                  $"{AverageManaAfter:0.##}" +
                  (KnownManaBefore != MainboardBefore ||
                   KnownManaAfter != MainboardAfter
                      ? $" (partial: {KnownManaBefore}/{MainboardBefore} → " +
                        $"{KnownManaAfter}/{MainboardAfter} known copies)" : "");

        public string Summary =>
            $"Mainboard: {MainboardBefore} → {MainboardAfter} copies. " +
            $"Locally unverified: {UnknownBefore} → {UnknownAfter}.\n" +
            $"Commander-mechanic matches: {SynergyBefore} → {SynergyAfter} " +
            $"({Change(SynergyDelta)} identifiable copies).\n" +
            ManaSummary + "\n" +
            $"Suggested-role shortfalls: {RolesImproved} improved, " +
            $"{RolesWorsened} worsened.\n" +
            string.Join("\n", Roles.Select(x => "• " + x.Display)) +
            "\n" +
            (MissingOracleBefore > 0 || MissingOracleAfter > 0
                ? $"Oracle metadata missing for {MissingOracleBefore} → " +
                  $"{MissingOracleAfter} copies; role/commander signals may be incomplete.\n"
                : "") +
            (SynergyDelta < 0
                ? "Caution: a recognized commander-mechanic match is lost. " +
                  "Review whether the card's actual effect is important.\n"
                : "") +
            (RolesWorsened > 0
                ? "Caution: at least one tracked role shortfall increases.\n"
                : "") +
            "Advisory only: these count cached text signals, not strategic " +
            "equivalence, game outcomes, exact printings or deck legality. " +
            "Nothing has been edited.";
    }

    public static Report Analyze(EdhLabDraftCatalog catalog,
        IEnumerable<(string Name, int Quantity)> mainboard,
        string outName, string inName, int landTarget = 36)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(mainboard);
        if (landTarget is < 20 or > 60)
            throw new ArgumentOutOfRangeException(nameof(landTarget));
        if (string.IsNullOrWhiteSpace(outName) || string.IsNullOrWhiteSpace(inName) ||
            outName.Equals(inName, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Select distinct mainboard source and replacement names.");

        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, qty) in mainboard)
        {
            if (string.IsNullOrWhiteSpace(name) || qty is < 1 or > 250)
                throw new ArgumentException("Invalid mainboard entries.", nameof(mainboard));
            counts[name] = checked(counts.GetValueOrDefault(name) + qty);
        }
        if (counts.GetValueOrDefault(outName) == 0)
            throw new InvalidDataException("The missing card is not in this mainboard.");
        var target = catalog.Find(inName) ??
            throw new InvalidDataException("Replacement lacks eligible local metadata.");
        if (target.RecordedCopies <= counts.GetValueOrDefault(target.Name) ||
            target.SuggestedCopyLimit <= counts.GetValueOrDefault(target.Name))
            throw new InvalidDataException("Replacement has no unused recorded eligible copies.");

        var before = counts.Select(x => (x.Key, x.Value)).ToArray();
        counts[outName]--;
        if (counts[outName] == 0) counts.Remove(outName);
        counts[target.Name] = checked(counts.GetValueOrDefault(target.Name) + 1);
        var after = counts.Select(x => (x.Key, x.Value)).ToArray();
        var prior = EdhLabDeckBlueprint.Analyze(catalog, before, landTarget);
        var next = EdhLabDeckBlueprint.Analyze(catalog, after, landTarget);
        var roles = prior.Roles.Zip(next.Roles, (a,b) =>
            new RoleChange(a.Role, a.Label, a.Copies, b.Copies, a.SuggestedMinimum))
            .ToArray();

        static (int Synergy, int MissingOracle, int KnownMana, double? Avg)
            Signals(EdhLabDraftCatalog cat, (string Key, int Value)[] deck)
        {
            int synergy = 0, missingOracle = 0, known = 0;
            double sum = 0;
            foreach (var (name, qty) in deck)
            {
                var card = cat.Find(name);
                if (card is null) continue;
                if (cat.SynergyFor(card.Name)?.Matches == true)
                    synergy = checked(synergy + qty);
                if (string.IsNullOrWhiteSpace(card.Oracle))
                    missingOracle = checked(missingOracle + qty);
                if (cat.HasManaValueMetadata &&
                    double.IsFinite(card.ManaValue) && card.ManaValue >= 0)
                {
                    known = checked(known + qty);
                    sum += card.ManaValue * qty;
                }
            }
            return (synergy, missingOracle, known, known > 0 ? sum / known : null);
        }
        var oldSignals = Signals(catalog, before);
        var newSignals = Signals(catalog, after);

        return new Report(prior.MainboardCopies, next.MainboardCopies,
            prior.UnverifiedCopies, next.UnverifiedCopies,
            oldSignals.Synergy, newSignals.Synergy,
            oldSignals.MissingOracle, newSignals.MissingOracle,
            oldSignals.KnownMana, newSignals.KnownMana,
            oldSignals.Avg, newSignals.Avg, roles, catalog.HasManaValueMetadata);
    }
}
