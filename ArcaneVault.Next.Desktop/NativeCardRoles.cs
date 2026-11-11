using System.Text.RegularExpressions;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// Advisory card-utilization tags derived locally from cached Scryfall
/// Oracle text/type, adapted from the legacy WebView deriveRoles heuristic.
/// These are NOT Scryfall's official tags, deck legality or human review.
/// </summary>
public static class NativeCardRoles
{
    public static readonly IReadOnlyList<string> Names = new[]
    {
        "Ramp", "Removal", "Board Wipe", "Discard", "Counterspell",
        "Card Draw", "Card Selection", "Lifegain", "Tutor", "Stax",
        "Haste", "Protection", "Recursion", "Sacrifice", "Tokens",
        "Graveyard Hate", "Finisher", "Equipment"
    };

    private static bool Matches(string text, string regex) =>
        Regex.IsMatch(text, regex, RegexOptions.IgnoreCase |
            RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(50));

    public static IReadOnlyList<string> Derive(string? type, string? oracle)
    {
        if (string.IsNullOrWhiteSpace(type) &&
            string.IsNullOrWhiteSpace(oracle)) return Array.Empty<string>();
        string t = oracle ?? "", ty = type ?? "";
        var tags = new List<string>(8);
        void Add(string name, bool matches)
        {
            if (matches) tags.Add(name);
        }

        Add("Ramp",
            (Matches(t, @"add (?:\{|one mana|two mana|[a-z ]*mana)|create a treasure token|search your library for (?:a|up to [a-z]+) (?:basic )?land card|costs? \{[0-9]\} less to cast") &&
             !Matches(t, @"rather than pay|whenever you tap a land for mana, add one")));
        Add("Removal", Matches(t,
            @"destroy target|exile target|deals? [\w\d]+ damage to target|target creature gets -|sacrifice a creature|return target (?:creature|permanent) (?:to its owner|to their owner)"));
        Add("Board Wipe", Matches(t,
            @"(?:destroy|exile) (?:all|each) (?:nonland |nonartifact |nontoken |nonlegendary |other )?(?:creatures|permanents|artifacts|enchantments)|(?:all|each) (?:nonland |nonartifact |nontoken |nonlegendary |other )?creatures? gets? -|deals? [\w\d{}]+ damage to each creature|deals? [\w\d{}]+ damage to all creatures|each creature takes [\w\d{}]+ damage"));
        Add("Discard", Matches(t,
            @"target (?:player|opponent) discards|each (?:player|opponent) discards|opponent discards|opponents discard|that player discards"));
        Add("Counterspell", Matches(t,
            @"counter target (?:spell|noncreature spell|creature spell|instant|sorcery|activated or triggered ability)"));
        Add("Card Draw", Matches(t,
            @"draw (?:a|one|two|three|four|x|that many) cards?|you may draw|draws? cards? equal"));
        Add("Card Selection", Matches(t,
            @"scry [0-9x]|surveil [0-9x]|look at the top [\w]+ cards|reveal the top [\w]+ cards"));
        Add("Lifegain", Matches(t, @"gain [\wx{} ]+ life|lifelink"));
        Add("Tutor", Matches(t,
            @"search your library for (?:a|an|up to one|two|any) (?!basic land card)[^.]+card"));
        Add("Stax", Matches(t,
            @"can't cast|can't attack|can.t activate|spells your opponents cast cost|enters? the battlefield tapped|more than one spell each turn|unless (?:that player|its controller) pays|can.t draw more than|players can.t"));
        Add("Haste", Matches(t, @"\bhaste\b"));
        Add("Protection", Matches(t,
            @"hexproof|ward \{|indestructible|phase out|returns? (?:it|that card|target creature) to the battlefield when|regenerate|protection from"));
        Add("Recursion", Matches(t,
            @"return target [^.]* from your graveyard|cast [^.]* from your graveyard|put [^.]* from a graveyard onto the battlefield"));
        Add("Sacrifice", Matches(t,
            @"sacrifice (?:a|another|any number of|x|two|three) (?:creature|artifact|permanent)|whenever you sacrifice"));
        Add("Tokens", Matches(t,
            @"create (?:a|two|three|x|that many|one or more) [^.]*token"));
        Add("Graveyard Hate", Matches(t,
            @"exile (?:target|all cards from|each opponent.s|a player.s) graveyard|exile all (?:cards|creature cards) from"));
        Add("Finisher", Matches(t,
            @"wins the game|loses the game|each opponent loses [\wx0-9]|opponent loses half|\bdouble (?:that|the) damage\b"));
        Add("Equipment", ty.Contains("Equipment", StringComparison.OrdinalIgnoreCase));
        return tags.AsReadOnly();
    }

    public static bool Valid(string? name) =>
        string.IsNullOrWhiteSpace(name) ||
        Names.Contains(name, StringComparer.OrdinalIgnoreCase);
}
