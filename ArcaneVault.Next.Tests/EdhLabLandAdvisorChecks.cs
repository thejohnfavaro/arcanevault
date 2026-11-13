using ArcaneVault.Next.EDHLab;

internal static class EdhLabLandAdvisorChecks
{
    private static void Must(bool yes, string reason)
    {
        if (!yes) throw new InvalidOperationException("EDH land advisor: " + reason);
    }

    private static void Reject(Action work)
    {
        try { work(); }
        catch (ArgumentException) { return; }
        throw new InvalidOperationException("Unsafe/unbounded land request was accepted.");
    }

    public static void Run()
    {
        // These are already locally legal commander/format-filtered candidates.
        // The land advisor must not read an external catalog or invent legality.
        EdhLabDraftCatalog.Card Card(string name, string type, string oracle,
            long real, bool basic = false, int copyLimit = 1) =>
            new(name, type, oracle, "", "Land", real, basic, copyLimit);
        var candidates = new[]
        {
            Card("Hallowed Fountain", "Land — Plains Island",
                "Hallowed Fountain enters tapped unless you pay 2 life.",
                real: 2),
            Card("Command Tower", "Land",
                "{T}: Add one mana of any color in your commander's color identity.",
                real: 0),
            Card("Flooded Strand", "Land",
                "{T}, Pay 1 life, Sacrifice Flooded Strand: Search your library for a Plains or Island card, put it onto the battlefield, then shuffle.",
                real: 1),
            Card("Reflecting Pool", "Land",
                "{T}: Add one mana of any type that a land you control could produce.",
                real: 1),
            Card("Cavern of Souls", "Land",
                "{T}: Add {C}. {T}: Add one mana of any color. Spend this mana only to cast a creature spell of the chosen type.",
                real: 1),
            Card("Ancient Tomb", "Land",
                "{T}: Add {C}{C}. Ancient Tomb deals 2 damage to you.",
                real: 1),
            Card("Port Town", "Land",
                "{T}: Add {W} or {U}.",
                real: 0),
            Card("Island", "Basic Land — Island", "",
                real: 40, basic: true, copyLimit: int.MaxValue),
            Card("Arcane Signet", "Artifact",
                "{T}: Add one mana of any color in your commander's color identity.",
                real: 3),
            Card("Landfall Impostor", "Creature — Landfall",
                "{T}: Add {U}.", real: 1)
        };
        var original = candidates.Select(x => (x.Name, x.RecordedCopies)).ToArray();
        var proxies = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
        {
            ["Command Tower"] = 2,
            ["Port Town"] = 1
        };
        var deck = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Hallowed Fountain"] = 1
        };

        var available = EdhLabLandAdvisor.Recommend(candidates, "WU",
            deck, proxies);
        Must(available.TotalEligibleLands == 8 &&
             available.Matching == 8 - 1 && // all except basic Island
             available.Rows.Count == 7 &&
             available.Rows.All(x => x.Name != "Island") &&
             !available.Rows.Any(x => x.Name == "Arcane Signet" ||
                                     x.Name == "Landfall Impostor"),
             "Only actual land-type cards should appear; normal basics hidden initially.");
        var fountain = available.Rows.Single(x => x.Name == "Hallowed Fountain");
        Must(fountain.ProductionSymbols == "WU" &&
             fountain.CoveredCommanderColors == 2 &&
             fountain.RealOwned == 2 && fountain.ProxyCopies == 0 &&
             fountain.InDeckCopies == 1 && !fountain.CanQuickAdd,
             "Dual basic land subtypes imply access to both commander colors; in-deck cards cannot quick-add.");
        var command = available.Rows.Single(x => x.Name == "Command Tower");
        Must(command.ProductionSymbols == "WU" &&
             command.AnyColor && command.RealOwned == 0 &&
             command.ProxyCopies == 2 && command.CanQuickAdd &&
             command.AvailabilityLabel.Contains("Proxy"),
             "Any-color lands should show only commander colors and separate Proxy availability.");
        var fetch = available.Rows.Single(x => x.Name == "Flooded Strand");
        Must(fetch.FetchesLand && fetch.ProductionSymbols == "" &&
             fetch.CoveredCommanderColors == 0 &&
             fetch.Explanation.Contains("target", StringComparison.OrdinalIgnoreCase),
             "Fetchlands must not pretend to directly produce W or U.");
        var reflecting = available.Rows.Single(x => x.Name == "Reflecting Pool");
        Must(reflecting.ProductionSymbols == "" && reflecting.Conditional &&
             reflecting.AccessNote.Contains("other lands", StringComparison.OrdinalIgnoreCase),
             "Reflecting Pool mana depends on what other lands can produce.");
        var cavern = available.Rows.Single(x => x.Name == "Cavern of Souls");
        Must(cavern.AnyColor && cavern.Conditional &&
             cavern.ProductionSymbols == "WUC" &&
             cavern.CoveredCommanderColors == 2,
             "Restricted rainbow + colorless access must show all detected production without claiming unconditional mana.");
        Must(available.Rows.Single(x => x.Name == "Ancient Tomb")
                 .ProductionSymbols == "C" &&
             available.Rows.Single(x => x.Name == "Port Town")
                 .ProductionSymbols == "WU",
             "Pure colorless and distinct multicolor production must stay separate.");
        Must(available.Rows.First().Name != "Hallowed Fountain",
             "Unused candidate lands should sort before a land already in the draft.");

        var realOnly = EdhLabLandAdvisor.Recommend(candidates, "WU", deck,
            proxies, EdhLabLandAdvisor.Availability.RealOwned);
        Must(realOnly.Rows.All(x => x.RealOwned > 0) &&
             !realOnly.Rows.Any(x => x.Name == "Command Tower") &&
             !realOnly.Rows.Any(x => x.Name == "Port Town"),
             "Owned-only excludes proxy-only suggestions.");
        var proxyOnly = EdhLabLandAdvisor.Recommend(candidates, "WU", deck,
            proxies, EdhLabLandAdvisor.Availability.Proxy);
        Must(proxyOnly.Rows.Select(x => x.Name)
                 .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                 .SequenceEqual(new[] { "Command Tower", "Port Town" }),
             "Proxy-only recommendations must not become real owned cards.");
        var arena = EdhLabLandAdvisor.Recommend(candidates, "WU", deck,
            paperProxies: null);
        Must(!arena.Rows.Any(x => x.Name == "Command Tower" ||
                                 x.Name == "Port Town"),
             "Arena suggestions must not inherit physical proxies.");

        var named = EdhLabLandAdvisor.Recommend(candidates, "WU", deck, proxies,
            EdhLabLandAdvisor.Availability.All, includeBasics: true, search: "  bAsIc lAnD  ");
        Must(named.Rows.Count == 1 && named.Rows[0].Name == "Island" &&
             named.Rows[0].ProductionSymbols == "U" &&
             named.Rows[0].CopyLimit == int.MaxValue,
             "Search and optional basics must preserve native basic land production and copy limit.");
        var limited = EdhLabLandAdvisor.Recommend(candidates, "WU", deck, proxies,
            EdhLabLandAdvisor.Availability.All, limit: 1);
        Must(limited.Rows.Count == 1 && limited.Matching > 1 &&
             limited.Truncated && limited.TotalEligibleLands == 8,
             "Search/filter must happen before the bounded list limit.");

        var colorless = EdhLabLandAdvisor.Recommend(
            new[] { Card("Wastes", "Basic Land", "{T}: Add {C}.",
                real: 10, basic: true, copyLimit: int.MaxValue) },
            "", includeBasics: true);
        Must(colorless.Rows.Single().ProductionSymbols == "C" &&
             colorless.Rows.Single().CoveredCommanderColors == 0,
             "Colorless decks may suggest Wastes without inventing a WUBRG identity.");

        // A mostly-white mainboard should prioritize additional blue sources.
        // The ordering is about actual draft sources, not mana symbols in costs.
        var coverageCards = new[]
        {
            Card("Many Plains", "Basic Land — Plains", "", real: 12,
                basic: true, copyLimit: int.MaxValue),
            Card("Needed Blue", "Land", "{T}: Add {U}.", real: 1),
            Card("Redundant White", "Land", "{T}: Add {W}.", real: 1),
            Card("Slow Needed Blue", "Land",
                "Slow Needed Blue enters the battlefield tapped. {T}: Add {U}.", real: 1),
            Card("Sometimes Blue", "Land",
                "Sometimes Blue enters tapped unless you control a Plains. {T}: Add {U}.", real: 1),
            Card("Creature-Only Blue", "Land",
                "{T}: Add {U}. Spend this mana only to cast creature spells.", real: 1),
            Card("Colorless With White Cost", "Land", "{W}, {T}: Add {C}.", real: 1),
            Card("Flexible Mana", "Land",
                "{T}: Add two mana in any combination of colors.", real: 1)
        };
        var draft = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Many Plains"] = 10
        };
        var gaps = EdhLabLandAdvisor.Recommend(coverageCards, "WU", draft,
            filter: EdhLabLandAdvisor.Availability.All, includeBasics: true);
        var byName = gaps.Rows.ToDictionary(x => x.Name,
            StringComparer.OrdinalIgnoreCase);
        Must(byName["Needed Blue"].GapPriority >
             byName["Redundant White"].GapPriority &&
             byName["Needed Blue"].GapPriority >
             byName["Slow Needed Blue"].GapPriority &&
             byName["Needed Blue"].GapPriority >
             byName["Creature-Only Blue"].GapPriority,
             "Missing-color and untapped/restricted mana must impact ranked choices.");
        Must(byName["Slow Needed Blue"].Tempo == "Enters tapped" &&
             byName["Sometimes Blue"].Tempo == "May enter tapped" &&
             byName["Creature-Only Blue"].Restricted &&
             byName["Colorless With White Cost"].ProductionSymbols == "C" &&
             byName["Flexible Mana"].AnyColor &&
             byName["Flexible Mana"].ProductionSymbols == "WU",
             "Land entry restrictions and mana-cost symbols are not interchangeable.");
        Must(gaps.CoverageNote.Contains("U 0", StringComparison.Ordinal) &&
             gaps.CoverageNote.Contains("Lowest coverage: U", StringComparison.Ordinal) &&
             draft["Many Plains"] == 10,
             "Draft source coverage must be visible and strictly read-only.");

        Reject(() => EdhLabLandAdvisor.Recommend(candidates, "WU",
            search: new string('X', 256)));
        Reject(() => EdhLabLandAdvisor.Recommend(candidates, "WU", limit: 0));
        Reject(() => EdhLabLandAdvisor.Recommend(candidates, "WU",
            filter: (EdhLabLandAdvisor.Availability)999));
        Must(candidates.Select(x => (x.Name, x.RecordedCopies))
                 .SequenceEqual(original) &&
             EdhLabLandAdvisor.Recommend(candidates, "WU", deck, proxies)
                 .Rows.Single(x => x.Name == "Command Tower").ProxyCopies == 2,
             "Repeated recommendations and filtering never alter catalog or proxy inventory.");
    }
}
