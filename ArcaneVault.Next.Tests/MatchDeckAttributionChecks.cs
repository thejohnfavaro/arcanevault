using ArcaneVault.Next.Desktop;

internal static class MatchDeckAttributionChecks
{
    public static void Run()
    {
        var decks = new [] {
            new MatchDeckAttribution.Deck("borg-native", "Borg", ""),
            new MatchDeckAttribution.Deck("other-native", "Other", "arena-other")
        };
        var maps = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase) {
            ["arena-confirmed"] = "borg-native",
            ["arena-other"] = "other-native"
        };
        string? Resolve(string matchId, string name, string arena, string native,
            string note, IReadOnlySet<string>? blocked = null) =>
            MatchDeckAttribution.Resolve(
                new MatchDeckAttribution.Match(matchId,name,arena,native,note),
                decks,maps,blocked);
        Assert(Resolve("one","Borg","arena-confirmed","","") == "borg-native",
            "Exact persisted Arena mapping must count");
        Assert(Resolve("two","Borg","unmapped-arena","","Deck assignment: manually confirmed in Arcane Vault Match History") == "borg-native",
            "A manually confirmed name must not be discarded for an unmapped Arena ID");
        Assert(Resolve("three","Borg","unmapped-arena","","Deck assignment: recent_arena_deck_submission") == "borg-native",
            "A named Arena queue submission must contribute to displayed deck totals");
        Assert(Resolve("four","Borg","unmapped-arena","","Passive inventory label") is null,
            "Unverified inventory names must not be credited");
        Assert(Resolve("five","Borg","arena-other","","Deck assignment: manually confirmed in Arcane Vault Match History") == "other-native",
            "An existing exact Arena mapping takes priority over conflicting names");
        Assert(Resolve("six","Borg","ambiguous","","Deck assignment: manually confirmed in Arcane Vault Match History",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase){"ambiguous"}) is null,
            "Ambiguous Arena IDs must remain blocked");
        Assert(Resolve("seven","Borg","","","") == "borg-native",
            "Name-only legacy record must count when name is unique");
        Assert(Resolve("eight","Borg","","missing-native","Deck assignment: manually confirmed in Arcane Vault Match History") is null,
            "An invalid explicit native deck ID must not fall back to a name");
    }
    private static void Assert(bool ok,string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }
}
