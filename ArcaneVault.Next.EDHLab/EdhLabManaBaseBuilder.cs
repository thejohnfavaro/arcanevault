namespace ArcaneVault.Next.EDHLab;

/// <summary>
/// Preview-only mana-base proposal for the currently VERIFIED commander/format
/// catalog and unsaved mainboard. Never edits inventory or saved deck data.
/// A separate explicit UI confirmation is required to apply its exact diff.
/// </summary>
public static class EdhLabManaBaseBuilder
{
    public sealed record Change(string Name, int Delta, string Reason)
    {
        public string Label => (Delta > 0 ? "+" : "") + Delta + " " + Name +
            " · " + Reason;
    }

    public sealed record Proposal(
        string SourceFingerprint, string CommanderColors,
        int Target, int CurrentLands, int ResultingLands,
        int OriginalCopies, int ResultingCopies,
        IReadOnlyList<Change> Changes, string Warning,
        IReadOnlyList<EdhLabDraftCatalog.Card> CatalogSnapshot,
        bool IsArena)
    {
        public bool CanApply => Changes.Count > 0 &&
            ResultingLands == Target && ResultingCopies <= 99 &&
            Warning.Length == 0;
        public string Summary => $"Mana-base proposal: {CurrentLands} → " +
            $"{ResultingLands}/{Target} lands · {OriginalCopies} → " +
            $"{ResultingCopies}/99 mainboard cards · {Changes.Count} card-name changes." +
            (Warning.Length > 0 ? "\nReview needed: " + Warning : "") +
            (CanApply ? "\nNothing changes until you approve the exact diff." :
                "\nNo automatic changes are available.");
    }

    public static string Fingerprint(
        IEnumerable<(string Name, int Quantity)> entries) =>
        string.Join("\n", entries
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Name.ToUpperInvariant() + ":" + x.Quantity));

    private static bool IsLand(EdhLabDraftCatalog.Card card) =>
        System.Text.RegularExpressions.Regex.IsMatch(card.Type,
            @"(?:^|[\s—])Land(?:$|[\s—])",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase |
            System.Text.RegularExpressions.RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(50));

    public static Proposal Build(
        IReadOnlyList<EdhLabDraftCatalog.Card> eligibleCards,
        string commanderColors, IReadOnlyList<(string Name, int Quantity)> entries,
        int targetLands,
        IReadOnlyDictionary<string, long>? paperProxies = null,
        bool arena = false)
    {
        ArgumentNullException.ThrowIfNull(eligibleCards);
        ArgumentNullException.ThrowIfNull(entries);
        if (targetLands is < 20 or > 60)
            throw new ArgumentOutOfRangeException(nameof(targetLands));
        string color = commanderColors ?? "";
        string fingerprint = Fingerprint(entries);
        var catalog = eligibleCards.ToDictionary(x => x.Name,
            StringComparer.OrdinalIgnoreCase);
        var draft = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var nonverified = new List<string>();
        int total = 0, lands = 0;
        foreach (var entry in entries)
        {
            if (entry.Quantity is < 1 or > 250 ||
                string.IsNullOrWhiteSpace(entry.Name) ||
                draft.ContainsKey(entry.Name))
                throw new InvalidDataException("Draft contains an invalid/duplicate card entry.");
            draft.Add(entry.Name, entry.Quantity);
            total = checked(total + entry.Quantity);
            if (!catalog.TryGetValue(entry.Name, out var card))
                nonverified.Add(entry.Name);
            else if (IsLand(card))
                lands = checked(lands + entry.Quantity);
        }
        string warning = "";
        var changes = new List<Change>();
        if (nonverified.Count > 0)
            warning = "Unverified card names: " +
                string.Join(", ", nonverified.Take(5)) +
                ". Complete local catalog sync and retry.";
        if (total > 99)
            warning = "The mainboard already exceeds 99 cards. Review manually.";
        if (total - lands + targetLands > 99)
            warning = "Too many nonland cards for this land target; " +
                "remove nonland cards manually rather than silently cutting spells.";

        if (warning.Length == 0 && lands > targetLands)
        {
            // Preserve every nonbasic. Only trim surplus explicitly verified
            // basic land copies; never infer a manual nonbasic is disposable.
            int excess = lands - targetLands;
            var basics = entries
                .Where(x => catalog.TryGetValue(x.Name, out var card) &&
                            card.IsBasicLand && IsLand(card))
                .OrderByDescending(x => x.Quantity)
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase);
            foreach (var basic in basics)
            {
                int remove = Math.Min(excess, basic.Quantity);
                if (remove <= 0) continue;
                draft[basic.Name] -= remove;
                lands -= remove;
                total -= remove;
                changes.Add(new Change(basic.Name, -remove,
                    "Excess basic lands; existing nonbasics retained"));
                excess -= remove;
                if (excess == 0) break;
            }
            if (excess > 0)
                warning = "Land target is lower than current nonbasic count. " +
                    "Only surplus basics can be removed automatically.";
        }
        if (warning.Length == 0 && lands < targetLands)
        {
            // Re-evaluate color gaps as the proposed mana base fills; the
            // advisor's cached Oracle parsing avoids repeated local rescans.
            // No candidate may consume a real/proxy copy twice.
            for (int step = 0; step < 60 && lands < targetLands; step++)
            {
                var result = EdhLabLandAdvisor.Recommend(
                    eligibleCards, color, draft, arena ? null : paperProxies,
                    EdhLabLandAdvisor.Availability.OwnedOrProxy,
                    includeBasics: true, limit: 500);
                var next = result.Rows.FirstOrDefault(x =>
                {
                    if (!catalog.TryGetValue(x.Name, out var card)) return false;
                    int already = draft.GetValueOrDefault(x.Name);
                    int max = Math.Min(250, card.SuggestedCopyLimit);
                    long available = checked(x.RealOwned +
                        (arena ? 0 : x.ProxyCopies));
                    return already < max && already < available &&
                        // Avoid colorless utility fillers when a colored
                        // commander currently lacks colored mana access.
                        (color.Length == 0 || x.ProductionSymbols.Any(ch =>
                            "WUBRG".Contains(ch)) || result.Rows.All(y =>
                            y.ProductionSymbols.All(ch => !"WUBRG".Contains(ch))));
                });
                if (next is null)
                {
                    warning = "Not enough recorded real/Proxy land copies " +
                        "to reach the target with verified eligible cards. " +
                        "No unowned copy will be fabricated.";
                    break;
                }
                draft[next.Name] = draft.GetValueOrDefault(next.Name) + 1;
                lands++;
                total++;
                string reason = next.LandCategory + " · " +
                    next.EntryRequirement + " · " +
                    (next.RealOwned > 0 ? "recorded real owned" : "paper Proxy");
                var old = changes.FindIndex(x => x.Name.Equals(next.Name,
                    StringComparison.OrdinalIgnoreCase));
                if (old >= 0)
                    changes[old] = changes[old] with {
                        Delta = changes[old].Delta + 1
                    };
                else changes.Add(new Change(next.Name, 1, reason));
            }
        }
        return new Proposal(fingerprint, color, targetLands,
            entries.Sum(x => catalog.TryGetValue(x.Name, out var c) &&
                IsLand(c) ? x.Quantity : 0),
            lands, entries.Sum(x => x.Quantity), total,
            changes, warning, eligibleCards, arena);
    }
}
