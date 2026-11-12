using System.Text.RegularExpressions;

namespace ArcaneVault.Next.EDHLab;

/// <summary>
/// Conservative offline two-card interaction hints. Each link requires a
/// specific enabler text cue and a corresponding payoff cue, not merely
/// sharing a commander mechanic tag. These are NOT rules-engine proofs,
/// deterministic combos, or guarantees the effects trigger together.
/// </summary>
public static class EdhLabSynergyChains
{
    private sealed record LinkRule(string Theme, string Mechanism,
        Func<EdhLabDraftCatalog.Card, bool> Enabler,
        Func<EdhLabDraftCatalog.Card, bool> Payoff);

    private static bool Has(string text, string part) =>
        text.Contains(part, StringComparison.OrdinalIgnoreCase);
    private static bool Any(string text, params string[] parts) =>
        parts.Any(p => Has(text, p));

    // Require create and token kind to occur within a single short effect
    // clause. A card saying "create a token. Sacrifice an artifact" must not
    // count as an artifact-token generator.
    private static bool CreatesKind(string oracle, string kind) =>
        Regex.IsMatch(oracle ?? "",
            @"\bcreate\b[^.!?;]{0,115}\b" + Regex.Escape(kind) +
            @"(?: creature)? tokens?\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(30));

    private static string Evidence(string oracle, params string[] signals)
    {
        if (string.IsNullOrWhiteSpace(oracle)) return "Rules text unavailable.";
        string[] clauses = oracle.Split(['.', '\n'], StringSplitOptions.TrimEntries |
            StringSplitOptions.RemoveEmptyEntries);
        string? match = clauses.FirstOrDefault(c =>
            signals.Any(s => Has(c, s))) ?? clauses.FirstOrDefault();
        if (match is null) return "Rules text unavailable.";
        return match.Length > 190 ? match[..190] + "…" : match;
    }

    // Tight pairs only: we intentionally do not classify arbitrary Oracle
    // text as a win condition or infer infinite/cross-zone interactions.
    private static readonly LinkRule[] Rules =
    [
        new("Artifacts", "Creating artifact tokens may trigger artifact entry payoffs",
            c => CreatesKind(c.Oracle, "artifact") || CreatesKind(c.Oracle, "Thopter artifact") ,
            c => Any(c.Oracle, "whenever an artifact enters",
                "whenever another artifact enters",
                "whenever one or more artifacts enter")),
        new("Tokens", "Creating creature tokens may trigger creature entry payoffs",
            c => CreatesKind(c.Oracle, "creature"),
            c => Any(c.Oracle, "whenever a creature enters",
                "whenever another creature enters",
                "whenever one or more creatures enter")),
        new("+1/+1 counters", "Placing +1/+1 counters may feed counter-related payoffs",
            c => Has(c.Oracle, "put") && Has(c.Oracle, "+1/+1 counter"),
            c => Any(c.Oracle, "whenever a +1/+1 counter",
                "whenever one or more +1/+1 counters")),
        new("Lifegain", "Gaining life may trigger lifegain payoffs",
            c => Any(c.Oracle, "you gain life", "gain 1 life", "gain 2 life",
                "gain 3 life", "gain 4 life"),
            c => Any(c.Oracle, "whenever you gain life",
                "whenever you gain one or more life")),
        new("Sacrifice", "Sacrificing creatures may support creature-death payoffs",
            c => Any(c.Oracle, "sacrifice a creature",
                "sacrifice another creature"),
            c => Any(c.Oracle, "whenever a creature dies",
                "whenever another creature dies",
                "whenever one or more creatures die"))
    ];

    public sealed record Chain(string Theme, string Enabler, string Payoff,
        string Status, string Explanation, string SuggestedCard,
        long SuggestedRecordedCopies, string SuggestedRoles,
        string EnablerEvidence = "", string PayoffEvidence = "")
    {
        public string Display => $"[{Theme}] {Enabler} → {Payoff} · {Status}";
    }

    public sealed record Report(IReadOnlyList<Chain> Chains, int UnknownCopies)
    {
        public string Headline => Chains.Count == 0
            ? "No narrow two-card interaction cues found in this eligible deck and collection."
            : $"{Chains.Count} possible two-card interactions to inspect";
        public string Disclaimer =>
            "Cached-text possibilities only; timing, targets, zones, opponent choices, " +
            "replacement effects, and actual game rules are not simulated. " +
            "No infinite combo or guaranteed win is claimed. Nothing is added or saved.";
    }

    public static Report Analyze(EdhLabDraftCatalog catalog,
        IEnumerable<(string Name, int Quantity)> mainboard, int maxChains = 12)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(mainboard);
        if (maxChains is < 1 or > 40)
            throw new ArgumentOutOfRangeException(nameof(maxChains));

        var deck = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
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
            deck[name] = checked(deck.GetValueOrDefault(name) + quantity);
        }

        var activeThemes = catalog.Synergy.Themes.ToHashSet(
            StringComparer.OrdinalIgnoreCase);
        if (activeThemes.Count == 0)
            return new Report(Array.Empty<Chain>(), unknown);

        // AllEligibleCards has already passed commander identity, format bans
        // and Arena printing availability. Only a link touching a deck card
        // can be suggested; no global catalog-to-catalog pairings.
        var all = catalog.AllEligibleCards;
        var inDeck = all.Where(c => deck.GetValueOrDefault(c.Name) > 0).ToArray();
        var output = new List<Chain>();
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rule in Rules.Where(r => activeThemes.Contains(r.Theme)))
        {
            var deckEnablers = inDeck.Where(rule.Enabler).ToArray();
            var deckPayoffs = inDeck.Where(rule.Payoff).ToArray();
            if (deckEnablers.Length == 0 && deckPayoffs.Length == 0) continue;

            var enablers = all.Where(rule.Enabler)
                .OrderByDescending(c => deck.ContainsKey(c.Name))
                .ThenByDescending(c => c.RecordedCopies > 0)
                .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToArray();
            var payoffs = all.Where(rule.Payoff)
                .OrderByDescending(c => deck.ContainsKey(c.Name))
                .ThenByDescending(c => c.RecordedCopies > 0)
                .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToArray();

            // Capped cross-product bounds processing cost on large catalogs.
            // Prioritize actual deck-to-deck connections, then owned missing
            // links, then unrecorded ideas. Avoid exhausted singleton offers.
            foreach (var left in enablers.Take(35))
            foreach (var right in payoffs.Take(35))
            {
                if (left.Name.Equals(right.Name, StringComparison.OrdinalIgnoreCase))
                    continue;
                bool leftIn = deck.ContainsKey(left.Name);
                bool rightIn = deck.ContainsKey(right.Name);
                if (!leftIn && !rightIn) continue;
                var missing = leftIn ? right : left;
                long available = Math.Max(0, Math.Min(
                    missing.RecordedCopies - deck.GetValueOrDefault(missing.Name),
                    (long)missing.SuggestedCopyLimit - deck.GetValueOrDefault(missing.Name)));
                if (!(leftIn && rightIn) && available == 0 && missing.RecordedCopies > 0)
                    continue;
                string status = leftIn && rightIn ? "Both in deck"
                    : available > 0 ? "Owned missing link" : "Not recorded";
                string key = rule.Theme + "\u001f" + left.Name + "\u001f" + right.Name;
                if (!keys.Add(key)) continue;
                string explanation = rule.Mechanism + ". " +
                    "Check both Oracle texts and actual timing/target requirements.";
                output.Add(new Chain(rule.Theme, left.Name, right.Name, status,
                    explanation, leftIn ? right.Name : left.Name,
                    leftIn && rightIn ? 0 : available,
                    leftIn && rightIn ? "" : missing.RoleHints,
                    Evidence(left.Oracle, "create", "put", "gain", "sacrifice"),
                    Evidence(right.Oracle, "whenever", "for each")));
            }
        }

        var ordered = output
            .OrderBy(c => c.Status == "Both in deck" ? 0 :
                c.Status == "Owned missing link" ? 1 : 2)
            .ThenBy(c => c.Theme, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Enabler, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Payoff, StringComparer.OrdinalIgnoreCase)
            .Take(maxChains).ToArray();
        return new Report(ordered, unknown);
    }
}
