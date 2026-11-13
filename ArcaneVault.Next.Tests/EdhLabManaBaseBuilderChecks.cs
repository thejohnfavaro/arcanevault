using ArcaneVault.Next.EDHLab;

internal static class EdhLabManaBaseBuilderChecks
{
    private static void Must(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException("Mana-base builder: " + message);
    }

    public static void Run()
    {
        static EdhLabDraftCatalog.Card Card(string name, string type,
            string oracle, long owned, bool basic = false, int limit = 1) =>
            new(name, type, oracle, "", "Land", owned, basic, limit);

        var candidates = new[]
        {
            Card("Plains", "Basic Land — Plains", "", 30, true, 250),
            Card("Island", "Basic Land — Island", "", 30, true, 250),
            Card("Command Tower", "Land",
                "{T}: Add one mana of any color in your commander's color identity.", 1),
            Card("Dual", "Land", "{T}: Add {W} or {U}.", 1),
            Card("Tap Dual", "Land",
                "Tap Dual enters tapped. {T}: Add {W} or {U}.", 1),
            Card("Proxy Dual", "Land", "{T}: Add {U}.", 0),
            Card("Unowned Dual", "Land", "{T}: Add {W} or {U}.", 0),
            Card("Spell", "Instant", "", 0, false, 1)
        };
        var baseline = new (string Name, int Quantity)[]
        {
            ("Plains", 12), ("Island", 3), ("Spell", 1)
        };
        var proxies = new Dictionary<string, long>(
            StringComparer.OrdinalIgnoreCase) { ["Proxy Dual"] = 1 };

        var initial = baseline.Select(x => x).ToArray();
        var plan = EdhLabManaBaseBuilder.Build(candidates, "WU",
            baseline, 20, proxies);
        Must(plan.CanApply && plan.CurrentLands == 15 &&
             plan.ResultingLands == 20 && plan.ResultingCopies == 21 &&
             plan.Changes.Sum(x => x.Delta) == 5 &&
             plan.Changes.All(x => x.Delta > 0) &&
             !plan.Changes.Any(x => x.Name == "Unowned Dual") &&
             baseline.SequenceEqual(initial) &&
             plan.SourceFingerprint == EdhLabManaBaseBuilder.Fingerprint(baseline),
             "Incomplete deck must get a five-land additive proposal without inventing ownership.");
        Must(plan.Changes.Any(x => x.Name == "Command Tower") &&
             plan.Changes.Any(x => x.Name == "Dual") &&
             plan.Changes.Any(x => x.Name == "Proxy Dual"),
             "Owned nonbasic and explicitly recorded paper proxy should be eligible.");

        var arena = EdhLabManaBaseBuilder.Build(candidates, "WU",
            baseline, 20, proxies, arena: true);
        Must(arena.CanApply &&
             !arena.Changes.Any(x => x.Name == "Proxy Dual"),
             "Arena plans must never use paper proxy quantities.");

        var full = new (string Name, int Quantity)[]
        {
            ("Plains", 24), ("Island", 3), ("Dual", 1), ("Spell", 1)
        };
        var trimmed = EdhLabManaBaseBuilder.Build(candidates, "WU",
            full, 20, proxies);
        Must(trimmed.CanApply && trimmed.CurrentLands == 28 &&
             trimmed.ResultingLands == 20 &&
             trimmed.Changes.Sum(x => x.Delta) == -8 &&
             trimmed.Changes.All(x => x.Delta < 0 &&
                 (x.Name == "Plains" || x.Name == "Island")) &&
             !trimmed.Changes.Any(x => x.Name == "Dual"),
             "Excess lands may trim verified basics, never nonbasics or spells.");

        var nonbasicOnly = new (string Name, int Quantity)[]
        {
            ("Command Tower", 1), ("Dual", 1), ("Tap Dual", 1), ("Spell", 1)
        };
        var unchanged = EdhLabManaBaseBuilder.Build(candidates, "WU",
            nonbasicOnly, 20, arena: true);
        Must(unchanged.CanApply || unchanged.Warning.Contains("Not enough recorded") ||
             unchanged.Warning.Contains("enough recorded"),
             "Short decks must declare insufficient owned copies instead of inventing lands.");
        var invalid = EdhLabManaBaseBuilder.Build(candidates, "WU",
            new[] { ("Unknown Card", 1) }, 20, proxies);
        Must(!invalid.CanApply && invalid.Warning.Contains("Unverified"),
             "Unknown local legality must refuse a bulk deck edit.");

        var tooManyNonlands = EdhLabManaBaseBuilder.Build(candidates, "WU",
            new[] { ("Spell", 80), ("Plains", 12) }, 20, proxies);
        Must(!tooManyNonlands.CanApply &&
             tooManyNonlands.Warning.Contains("nonland"),
             "Never delete spells to force the requested land target.");

        var unsupported = EdhLabManaBaseBuilder.Build(
            new[] { Card("Single Owned", "Land", "{T}: Add {W}.", 1) },
            "W", Array.Empty<(string Name, int Quantity)>(), 20);
        Must(!unsupported.CanApply &&
             unsupported.Warning.Contains("Not enough recorded"),
             "Never claim a complete plan when recorded legal land inventory is insufficient.");

        Console.WriteLine("PASS: reviewed mana-base diff, target, real/Proxy limits, Arena isolation, conservative basic cuts and no unverified edits");
    }
}
