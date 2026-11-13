using System.Text.RegularExpressions;

namespace ArcaneVault.Next.EDHLab;

/// <summary>
/// Read-only land discovery over the already legality- and identity-filtered
/// local EDH catalog. Never interprets a land's color identity as the colors
/// it can produce and never edits inventory, saved decks, or printings.
/// </summary>
public static class EdhLabLandRecommendations
{
    public sealed record Land(
        EdhLabDraftCatalog.Card Card,
        string ProducedColors,
        bool AnyColorText,
        bool ConditionalOrUnknown,
        long ProxyCopies,
        int DraftCopies,
        int Score)
    {
        public bool RealOwned => Card.RecordedCopies > 0;
        public bool HasProxy => ProxyCopies > 0;
        public string Availability => Card.RecordedCopies > 0
            ? $"{Card.RecordedCopies} owned" + (ProxyCopies > 0 ? $" · {ProxyCopies} Proxy" : "")
            : ProxyCopies > 0 ? $"{ProxyCopies} Proxy (not owned)" : "Unowned";
    }

    public enum AvailabilityFilter { All, Owned, Proxy, Unowned }

    // Explicit mana production only; land name and color identity alone
    // cannot establish which colors its activated abilities produce.
    private static readonly Regex ManaSymbols = new(
        @"\{([WUBRGC])\}", RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    public static IReadOnlyList<Land> Suggest(
        EdhLabDraftCatalog catalog,
        IReadOnlyDictionary<string, int>? mainboard = null,
        IReadOnlyDictionary<string, long>? proxies = null,
        AvailabilityFilter availability = AvailabilityFilter.All,
        int limit = 200)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (!Enum.IsDefined(availability))
            throw new ArgumentOutOfRangeException(nameof(availability));
        if (limit is < 1 or > 500)
            throw new ArgumentOutOfRangeException(nameof(limit));

        var candidates = new List<Land>();
        foreach (var card in catalog.AllEligibleCards)
        {
            // The catalog has already applied format, legality, Arena and
            // commander identity restrictions; do not reimplement them.
            if (!Regex.IsMatch(card.Type, @"\bLand\b",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                    TimeSpan.FromMilliseconds(100)))
                continue;

            var draft = mainboard?.GetValueOrDefault(card.Name) ?? 0;
            if (draft >= card.SuggestedCopyLimit) continue;
            var proxy = proxies?.GetValueOrDefault(card.Name) ?? 0;
            if (availability == AvailabilityFilter.Owned && card.RecordedCopies <= 0) continue;
            if (availability == AvailabilityFilter.Proxy && proxy <= 0) continue;
            if (availability == AvailabilityFilter.Unowned &&
                (card.RecordedCopies > 0 || proxy > 0)) continue;

            var oracle = card.Oracle ?? "";
            // Oracle may contain reminder text or a second face. These are
            // hints, not a guarantee of unconditional colored mana.
            bool anyColor = oracle.Contains("add one mana of any color",
                    StringComparison.OrdinalIgnoreCase) ||
                oracle.Contains("add one mana of any type",
                    StringComparison.OrdinalIgnoreCase) ||
                oracle.Contains("add two mana in any combination of colors",
                    StringComparison.OrdinalIgnoreCase) ||
                oracle.Contains("add one mana of any color in your commander's color identity",
                    StringComparison.OrdinalIgnoreCase);
            var produced = ManaSymbols.Matches(oracle)
                .Select(x => x.Groups[1].Value.ToUpperInvariant())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => "WUBRGC".IndexOf(x, StringComparison.Ordinal))
                .ToArray();
            bool conditional = anyColor ||
                oracle.Contains("only to cast", StringComparison.OrdinalIgnoreCase) ||
                oracle.Contains("spend this mana only", StringComparison.OrdinalIgnoreCase) ||
                oracle.Contains("enters the battlefield tapped", StringComparison.OrdinalIgnoreCase) ||
                oracle.Contains("unless", StringComparison.OrdinalIgnoreCase) ||
                oracle.Contains("if you control", StringComparison.OrdinalIgnoreCase) ||
                oracle.Contains("choose a color", StringComparison.OrdinalIgnoreCase) ||
                (produced.Length == 0);

            int score = (card.RecordedCopies > 0 ? 100 : 0)
                + (proxy > 0 ? 25 : 0)
                + (anyColor ? 15 : 0)
                + produced.Count(x => x != "C") * 7
                - (conditional ? 3 : 0)
                - (draft > 0 ? 10 : 0);
            candidates.Add(new Land(card, string.Join("", produced), anyColor,
                conditional, proxy, draft, score));
        }
        return candidates.OrderByDescending(x => x.Score)
            .ThenBy(x => x.Card.Name, StringComparer.OrdinalIgnoreCase)
            .Take(limit).ToArray();
    }
}
