using System.Text.RegularExpressions;

namespace ArcaneVault.Next.EDHLab;

/// <summary>
/// Explainable, offline, opt-in synergy hints derived from the commander's
/// cached Oracle text. Not a card-power grade, combo detector, deck validator,
/// or claim that a card actually performs well with the commander.
/// </summary>
public sealed class EdhLabSynergyAdvisor
{
    public sealed record Fit(int Score, string Why)
    {
        public bool Matches => Score > 0;
    }

    private sealed record Rule(string Name, string Explanation,
        Func<string, bool> CommanderMentions,
        Func<string, string, int> CandidateStrength);

    private static bool Has(string text, string phrase) =>
        text.Contains(phrase, StringComparison.OrdinalIgnoreCase);

    private static bool Any(string text, params string[] patterns) =>
        patterns.Any(p => Has(text, p));

    private static readonly Rule[] Rules =
    [
        new("Artifacts", "artifacts or artifact payoffs",
            o => Has(o, "artifact"),
            (t, o) => Any(o, "artifacts you control", "artifact you control",
                "whenever an artifact", "whenever another artifact",
                "artifact spell", "artifact token", "affinity for artifacts",
                "improvise", "metalcraft") ? 3 :
                Has(t, "Artifact") ? 1 : 0),
        new("Tokens", "token creation or token payoffs",
            o => Has(o, "token"),
            (t, o) => (Has(o, "create") && Has(o, "token")) ||
                Any(o, "tokens you control", "whenever a token",
                    "whenever one or more tokens", "populate") ? 3 : 0),
        new("+1/+1 counters", "counter placement or proliferation",
            o => Any(o, "+1/+1 counter", "proliferate",
                "counters on creatures", "counters on it"),
            (t, o) => Any(o, "+1/+1 counter", "proliferate") ? 3 : 0),
        new("Graveyard", "graveyard recursion or filling",
            o => Any(o, "graveyard", "mill ", "milled"),
            (t, o) => Any(o, "from your graveyard", "from a graveyard",
                "into your graveyard", "into a graveyard", "mill ",
                "from graveyards", "cards in your graveyard") ? 3 : 0),
        new("Sacrifice", "sacrifice or death triggers",
            o => Any(o, "sacrifice", "dies", "die this turn"),
            (t, o) => Any(o, "sacrifice", "whenever a creature dies",
                "whenever another creature dies", "when this creature dies",
                "whenever an artifact dies") ? 3 : 0),
        new("Lifegain", "gaining life or lifegain payoffs",
            o => Any(o, "gain life", "gains life", "life you gain",
                "whenever you gain"),
            (t, o) => Any(o, "gain life", "gains life",
                "whenever you gain life", "life you gain") ? 3 : 0),
        new("Spells", "instants, sorceries or spellcasting payoffs",
            o => Any(o, "whenever you cast", "whenever a player casts",
                "instant or sorcery", "noncreature spell"),
            (t, o) => Any(o, "whenever you cast", "copy target instant",
                "copy target sorcery", "instant or sorcery spell",
                "noncreature spell") ? 3 :
                Any(t, "Instant", "Sorcery") ? 1 : 0),
        new("Enchantments", "enchantments or enchantment payoffs",
            o => Has(o, "enchantment"),
            (t, o) => Any(o, "enchantments you control", "enchantment enters",
                "whenever an enchantment", "constellation") ? 3 :
                Has(t, "Enchantment") ? 1 : 0),
        new("Equipment", "equipment or equipped-creature payoffs",
            o => Any(o, "equipment", "equipped creature", "equip "),
            (t, o) => Any(o, "equipped creature", "equipment you control",
                "attach target equipment", "equip ") ? 3 :
                Has(t, "Equipment") ? 1 : 0),
        new("Lands", "landfall or land-entry payoffs",
            o => Any(o, "landfall", "land enters the battlefield",
                "land enters under your control", "play an additional land"),
            (t, o) => Any(o, "landfall", "whenever a land enters",
                "play an additional land", "put a land card from your hand") ? 3 : 0),
        new("Discard", "discarding or discard payoffs",
            o => Has(o, "discard"),
            (t, o) => Any(o, "discard a card", "discards a card",
                "whenever you discard", "whenever an opponent discards") ? 3 : 0)
    ];

    private readonly IReadOnlyList<Rule> active;

    private EdhLabSynergyAdvisor(IReadOnlyList<Rule> active) => this.active = active;

    /// <summary>Recognized commander signals, not an inferred deck archetype.</summary>
    public string ThemeSummary => active.Count == 0
        ? "No recognized mechanic signals in this commander’s cached rules text."
        : "Commander text mentions: " + string.Join(", ", active.Select(x => x.Name)) +
          ". Hints are heuristic, not a power ranking.";

    public int ThemeCount => active.Count;

    public static EdhLabSynergyAdvisor FromCommander(string type, string oracle)
    {
        type ??= "";
        oracle ??= "";
        var active = Rules.Where(x => x.CommanderMentions(oracle)).ToList();

        // Tribal support requires the commander's text to mention one of its
        // actual creature subtypes. Merely being a Human/Wizard/etc. is not
        // enough evidence of a typal deck.
        int separator = type.IndexOf('—');
        if (separator >= 0)
        {
            var tribes = type[(separator + 1)..].Trim().Split(
                ' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (string tribe in tribes.Where(x => x.Length >= 3))
            {
                var word = new Regex(@"\b" + Regex.Escape(tribe) + @"s?\b",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                    TimeSpan.FromMilliseconds(30));
                if (!word.IsMatch(oracle)) continue;
                active.Add(new Rule(tribe + " typal", "matching " + tribe + " cards or payoffs",
                    _ => true,
                    (candidateType, candidateOracle) =>
                        word.IsMatch(candidateType) ||
                        Any(candidateOracle, tribe + " you control",
                            "other " + tribe, tribe + " creatures",
                            tribe + " spells") ? 3 : 0));
            }
        }
        return new EdhLabSynergyAdvisor(active);
    }

    public Fit Evaluate(string candidateType, string candidateOracle)
    {
        string type = candidateType ?? "";
        string oracle = candidateOracle ?? "";
        int score = 0;
        var reasons = new List<string>();
        foreach (var rule in active)
        {
            int strength = rule.CandidateStrength(type, oracle);
            if (strength <= 0) continue;
            score = checked(score + strength);
            reasons.Add(rule.Name + ": " + rule.Explanation);
        }
        string why = reasons.Count == 0
            ? "No direct commander-text synergy recognized; this card may still be useful."
            : string.Join("; ", reasons.Take(3)) +
              (reasons.Count > 3 ? $" (+{reasons.Count - 3} more)" : "") +
              ".";
        return new Fit(score, why);
    }
}
