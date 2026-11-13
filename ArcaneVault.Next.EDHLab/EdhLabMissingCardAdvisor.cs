namespace ArcaneVault.Next.EDHLab;

/// <summary>
/// Read-only, evidence-based comparisons for missing MAINBOARD cards.
/// Candidates already passed the local format, Arena availability and
/// commander identity checks in EdhLabDraftCatalog. Similar role tags are
/// signals for review, never proof of equivalent effects or power.
/// </summary>
public static class EdhLabMissingCardAdvisor
{
    public sealed record CardProfile(string Name, string Type, string ManaCost,
        double? ManaValue, string Oracle, string Roles,
        long RecordedCopies, int DraftCopies, int CommanderSynergy)
    {
        public string ManaLabel => ManaValue is { } mv
            ? $"Mana value {mv:0.#}" : "Mana value unavailable";
        public string RecordedLabel => $"{RecordedCopies:N0} recorded · " +
            $"{DraftCopies:N0} in mainboard";
        public string RoleLabel => string.IsNullOrWhiteSpace(Roles)
            ? "No recognized role hints" : Roles.Replace(" · ", ", ");
    }

    public sealed record Alternative(string Name, long UnusedRecordedCopies,
        string SharedRoles, int Score, string Explanation)
    {
        public int Rank { get; init; }
        public CardProfile? MissingCard { get; init; }
        public CardProfile? ReplacementCard { get; init; }
        public string RankingReasons { get; init; } = "";
        public string Display =>
            $"#{Rank}  {Name} · {UnusedRecordedCopies:N0} unused · {SharedRoles}";
    }

    private static string PrimaryType(string line)
    {
        string beforeSubtype = (line ?? "").Split('—')[0];
        foreach (string type in new[]
            { "Land", "Creature", "Instant", "Sorcery", "Artifact",
              "Enchantment", "Planeswalker", "Battle" })
            if (beforeSubtype.Contains(type, StringComparison.OrdinalIgnoreCase))
                return type;
        return "Other";
    }

    private static CardProfile Profile(EdhLabDraftCatalog catalog,
        EdhLabDraftCatalog.Card card, int draftCopies)
    {
        double? manaValue = catalog.HasManaValueMetadata &&
            double.IsFinite(card.ManaValue) && card.ManaValue >= 0
                ? card.ManaValue : null;
        return new CardProfile(card.Name, card.Type,
            catalog.HasManaCostMetadata ? card.ManaCost : "",
            manaValue, card.Oracle, card.RoleHints, card.RecordedCopies,
            draftCopies, catalog.SynergyFor(card.Name)?.Score ?? 0);
    }

    public static IReadOnlyList<Alternative> Analyze(
        EdhLabDraftCatalog catalog,
        IEnumerable<(string Name, int Quantity)> mainboard,
        string missingName, int limit = 8,
        EdhLabStrategyFocus focus = EdhLabStrategyFocus.Balanced)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(mainboard);
        if (string.IsNullOrWhiteSpace(missingName)) return [];
        if (limit is < 1 or > 20)
            throw new ArgumentOutOfRangeException(nameof(limit));
        _ = EdhLabStrategyGoals.Label(focus); // reject unsupported focus instead of silent fallback

        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, qty) in mainboard)
        {
            if (string.IsNullOrWhiteSpace(name) || qty is < 1 or > 250)
                throw new ArgumentException("A valid mainboard is required.", nameof(mainboard));
            counts[name] = checked(counts.GetValueOrDefault(name) + qty);
        }
        // Commander-only shortages cannot be resolved by a mainboard cut.
        if (counts.GetValueOrDefault(missingName) < 1) return [];
        var source = catalog.Find(missingName);
        if (source is null) return [];
        var neededRoles = source.RoleHints.Split(" · ",
            StringSplitOptions.RemoveEmptyEntries);
        if (neededRoles.Length == 0) return []; // never invent equivalent effects

        bool land = source.Type.Contains("Land", StringComparison.OrdinalIgnoreCase);
        var sourceProfile = Profile(catalog, source, counts.GetValueOrDefault(source.Name));
        string sourceType = PrimaryType(source.Type);

        var ranked = catalog.AllEligibleCards
            .Where(x => x.RecordedCopies > counts.GetValueOrDefault(x.Name) &&
                x.SuggestedCopyLimit > counts.GetValueOrDefault(x.Name) &&
                !x.Name.Equals(missingName, StringComparison.OrdinalIgnoreCase) &&
                x.Type.Contains("Land", StringComparison.OrdinalIgnoreCase) == land)
            .Select(x =>
            {
                var candidateRoles = x.RoleHints.Split(" · ",
                    StringSplitOptions.RemoveEmptyEntries);
                // All identified source roles must remain; this is necessary
                // but NOT sufficient for actual card-text equivalence.
                if (!neededRoles.All(role => candidateRoles.Contains(role,
                        StringComparer.OrdinalIgnoreCase))) return null;

                var candidateProfile = Profile(catalog, x,
                    counts.GetValueOrDefault(x.Name));
                string[] extras = candidateRoles.Except(neededRoles,
                    StringComparer.OrdinalIgnoreCase).ToArray();
                bool sameType = PrimaryType(x.Type) == sourceType;
                int synergyDifference = candidateProfile.CommanderSynergy -
                    sourceProfile.CommanderSynergy;
                bool comparableMana = sourceProfile.ManaValue.HasValue &&
                    candidateProfile.ManaValue.HasValue;
                double manaDelta = comparableMana
                    ? candidateProfile.ManaValue!.Value - sourceProfile.ManaValue!.Value : 0;
                int manaPenalty = comparableMana
                    ? Math.Min(24, (int)Math.Round(Math.Abs(manaDelta) * 6,
                        MidpointRounding.AwayFromZero)) : 0;
                // Equal-size curve shifts are not necessarily symmetric:
                // reducing MV is a modest usability signal, while a
                // more expensive replacement should require extra scrutiny.
                // Small enough not to swamp role or commander fit.
                int curveDirection = !comparableMana ? 0
                    : manaDelta < 0 ? 6 : manaDelta > 0 ? -6 : 0;
                int addedRolePoints = Math.Min(18, extras.Length * 6);
                var roleSet = candidateRoles.ToHashSet(StringComparer.OrdinalIgnoreCase);
                // Strategy role preferences remain meaningful even when an
                // older Scryfall catalog lacks mana-value metadata. Only
                // the fast-start *mana* bonus requires an actual known MV.
                int focusPoints = candidateProfile.ManaValue is { } knownMv
                    ? EdhLabStrategyGoals.Bias(focus, roleSet,
                        candidateProfile.CommanderSynergy, knownMv) / 4
                    : focus switch
                    {
                        EdhLabStrategyFocus.FastStart => roleSet.Contains("Ramp") ? 8 : 0,
                        EdhLabStrategyFocus.CardAdvantage =>
                            (roleSet.Contains("Card Draw") ? 12 : 0) +
                            (roleSet.Contains("Tutor") ? 8 : 0),
                        EdhLabStrategyFocus.Interaction =>
                            (roleSet.Contains("Removal") ? 12 : 0) +
                            (roleSet.Contains("Protection") ? 9 : 0) +
                            (roleSet.Contains("Board Wipe") ? 4 : 0),
                        EdhLabStrategyFocus.CommanderSynergy =>
                            Math.Clamp(candidateProfile.CommanderSynergy, 0, 20) * 3,
                        _ => 0
                    };
                int copiesBonus = x.RecordedCopies -
                    counts.GetValueOrDefault(x.Name) > 1 ? 3 : 0;
                int score = 80 + (sameType ? 15 : 0) + addedRolePoints +
                    Math.Clamp(synergyDifference * 4, -48, 48) +
                    focusPoints + copiesBonus + curveDirection - manaPenalty;

                string manaNote = !comparableMana
                    ? "Mana-value comparison unavailable (sync metadata for a curve comparison)"
                    : manaDelta == 0 ? "Same mana value"
                    : manaDelta < 0
                        ? $"{Math.Abs(manaDelta):0.#} lower mana value"
                        : $"{manaDelta:0.#} higher mana value";
                string extraText = extras.Length == 0
                    ? "No additional detected roles" : "Additional role hints: " +
                        string.Join(", ", extras);
                string reasons =
                    $"Shared roles: {string.Join(", ", neededRoles)}. " +
                    $"Type: {(sameType ? "same primary type" : sourceType + " → " + PrimaryType(x.Type))}. " +
                    $"{manaNote}. " +
                    $"Commander-mechanic signal: {sourceProfile.CommanderSynergy} → " +
                    $"{candidateProfile.CommanderSynergy}. {extraText}. " +
                    $"Strategy weighting: {EdhLabStrategyGoals.Label(focus)}.";
                long available = x.RecordedCopies - counts.GetValueOrDefault(x.Name);
                return new Alternative(x.Name, available,
                    string.Join(", ", neededRoles), score,
                    $"{x.Name} shares the recognized roles of {source.Name}. " +
                    $"{available:N0} additional copies are recorded in the selected " +
                    "format's inventory. Evaluate actual rules and interactions; " +
                    "this heuristic is not an equivalence, power or legality guarantee.")
                {
                    MissingCard = sourceProfile,
                    ReplacementCard = candidateProfile,
                    RankingReasons = reasons
                };
            })
            .Where(x => x is not null)
            .Select(x => x!)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .Select((x, i) => x with { Rank = i + 1 })
            .ToArray();
        return ranked;
    }
}
