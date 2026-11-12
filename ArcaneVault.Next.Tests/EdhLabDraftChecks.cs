using ArcaneVault.Next.Domain;
using ArcaneVault.Next.Collection;
using ArcaneVault.Next.EDHLab;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;

internal static class EdhLabDraftChecks
{
    private static void Must(bool value, string reason)
    {
        if (!value) throw new InvalidOperationException("EDH Lab drafting: " + reason);
    }

    public static void Run(string work)
    {
        string catalog = Path.Combine(work, "edh-draft-catalog.sqlite");
        using (var db = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = catalog, Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString()))
        {
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE cards(name TEXT PRIMARY KEY COLLATE NOCASE,
                    type TEXT, oracle TEXT, colors TEXT, legalities TEXT,
                    arena INTEGER);
                """;
            cmd.ExecuteNonQuery();
            using var tx = db.BeginTransaction();
            using var add = db.CreateCommand();
            add.Transaction = tx;
            add.CommandText = """
                INSERT INTO cards(name,type,oracle,colors,legalities,arena)
                VALUES($name,$type,$oracle,$colors,$legal,$arena);
                """;
            foreach (string key in new[] { "$name", "$type", "$oracle",
                "$colors", "$legal", "$arena" })
                add.Parameters.Add(new SqliteParameter(key, ""));
            void Card(string name, string colors, string oracle = "",
                string type = "Instant", string commanderLegal = "legal",
                string brawlLegal = "legal", bool arena = true)
            {
                add.Parameters["$name"].Value = name;
                add.Parameters["$type"].Value = type;
                add.Parameters["$oracle"].Value = oracle;
                add.Parameters["$colors"].Value = colors;
                add.Parameters["$legal"].Value =
                    "{\"commander\":\"" + commanderLegal +
                    "\",\"brawl\":\"" + brawlLegal + "\"}";
                add.Parameters["$arena"].Value = arena ? 1 : 0;
                add.ExecuteNonQuery();
            }
            Card("Selected Leader", "WU",
                oracle: "Whenever an artifact enters under your control, create a 1/1 colorless Thopter artifact creature token.",
                type: "Legendary Artifact Creature — Human");
            Card("Sol Ring", "", "Add {C}.", type: "Artifact");
            Card("Island", "U", "Add {U}.", type: "Basic Land — Island");
            Card("Plains", "W", "Add {W}.", type: "Basic Land — Plains");
            Card("Azorius Guildgate", "WU", "{T}: Add {W} or {U}.",
                type: "Land — Gate");
            Card("Command Tower", "", "{T}: Add one mana of any color in your commander's color identity.",
                type: "Land");
            Card("Reliquary Tower", "", "{T}: Add {C}.", type: "Land");
            Card("Snow-Covered Island", "U", "", type: "Basic Snow Land — Island");
            Card("Brainstorm", "U", "Draw three cards.");
            Card("Counterspell", "U", "Counter target spell.", brawlLegal: "banned");
            Card("Swords to Plowshares", "W", "Exile target creature.", arena: false);
            Card("Arcane Signet", "", "Add one mana of any color.", type: "Artifact");
            Card("Thopter Foundry", "U", "Create a 1/1 Thopter artifact creature token.", type: "Artifact");
            Card("Artifact Hater", "W", "Destroy target artifact.", type: "Instant");
            Card("Non-Arena Thopters", "U", "Create a Thopter artifact creature token.",
                type: "Artifact", arena: false);
            Card("Paper Banned Thopter", "U", "Create a Thopter artifact creature token.",
                type: "Artifact", commanderLegal: "banned");
            Card("Board Clear", "W", "Destroy all creatures.", type: "Sorcery");
            Card("Heavy Insight", "U", "Draw two cards.", type: "Sorcery");
            Card("Tiny Insight", "U", "Draw a card.", type: "Instant");
            Card("Heavy Generic", "W", "Vanilla finisher.", type: "Creature — Giant");
            Card("Artifact Engine", "U", "Create a 1/1 Thopter artifact creature token.",
                type: "Artifact");
            Card("Clockwork Maker", "U",
                "Create a 1/1 colorless Thopter artifact creature token.",
                type: "Artifact Creature — Artificer");
            Card("Artifact Arrival", "U",
                "Whenever an artifact enters the battlefield under your control, draw a card.",
                type: "Creature — Artificer");
            Card("Token Arrival", "W",
                "Whenever a creature enters the battlefield under your control, gain 1 life.",
                type: "Creature — Cleric");
            Card("Off Color Arrival", "R",
                "Whenever an artifact enters the battlefield under your control, draw a card.",
                type: "Creature — Human");
            Card("Banned Arrival", "U",
                "Whenever an artifact enters the battlefield under your control, draw a card.",
                commanderLegal: "banned", brawlLegal: "banned");
            Card("Mystic Tutor", "U", "Search your library for a card.");
            Card("Lightning Bolt", "R", "Deal 3 damage.");
            Card("Three Color", "WUB", "Draw a card.");
            Card("Banned Card", "", "Add {C}.", commanderLegal: "banned", brawlLegal: "banned");
            Card("Not Legal", "W", commanderLegal: "not_legal", brawlLegal: "not_legal");
            Card("Malformed Identity", "X", "Draw a card.");
            Card("Commander Only", "W", "Create a 1/1 token.", brawlLegal: "not_legal");
            Card("Special Scholar", "W", "A deck can have up to seven cards named Special Scholar.");
            Card("Unlimited Scholar", "U", "A deck can have any number of cards named Unlimited Scholar.");
            tx.Commit();
        }

        var originalHash = SHA256.HashData(File.ReadAllBytes(catalog));
        var ownership = new NativeBackupView(
            new[]
            {
                new NativePhysicalRow("Sol Ring", 1, "", "", "", ""),
                new NativePhysicalRow("Swords to Plowshares", 2, "", "", "", ""),
                new NativePhysicalRow("Counterspell", 1, "", "", "", ""),
                new NativePhysicalRow("Tiny Insight", 1, "", "", "", ""),
                new NativePhysicalRow("Lightning Bolt", 20, "", "", "", ""),
                new NativePhysicalRow("Island", 0, "", "", "", "")
            },
            new[]
            {
                new NativeArenaRow("Island", 4),
                new NativeArenaRow("Brainstorm", 1),
                new NativeArenaRow("Counterspell", 1)
            }, 0, 0, 0, 24, 6, 0, 24);

        var paper = EdhLabDraftCatalog.OpenReadOnly(catalog, ownership,
            "Selected Leader", "WU", EdhLabFormat.PaperCommander);
        var arena = EdhLabDraftCatalog.OpenReadOnly(catalog, ownership,
            "Selected Leader", "WU", EdhLabFormat.ArenaBrawl);

        // Package views share the existing offline commander rules and legal
        // snapshot. They must not mistake exhausted cards for available copies.
        var packageDraft = new (string Name, int Quantity)[]
        {
            ("Sol Ring", 1), ("Brainstorm", 1)
        };
        var paperPackages = EdhLabSynergyPackages.Analyze(paper, packageDraft);
        var artifactPackage = paperPackages.Packages.Single(x => x.Theme == "Artifacts");
        var tokenPackage = paperPackages.Packages.Single(x => x.Theme == "Tokens");
        Must(paperPackages.HasRecognizedThemes && paperPackages.Packages.Count >= 2 &&
             artifactPackage.InDeck.Any(x => x.Name == "Sol Ring") &&
             !artifactPackage.OwnedOptions.Any(x => x.Name == "Sol Ring") &&
             artifactPackage.MissingOptions.Any(x => x.Name == "Thopter Foundry") &&
             tokenPackage.MissingOptions.Any(x => x.Name == "Thopter Foundry"),
             "Packages must separate existing, exhausted, and missing synergy cards by recognized mechanic.");
        Must(artifactPackage.Coverage.Contains("0 potential enablers") &&
             artifactPackage.Coverage.Contains("1 supporting matches") &&
             artifactPackage.NextStep.Contains("No complete enabler/payoff") &&
             tokenPackage.Coverage.Contains("0 potential enablers"),
             "Mechanic coverage must describe actual full draft signals, not missing suggestions.");
        var seededPackages = EdhLabSynergyPackages.Analyze(paper,
            new[] { (Name: "Thopter Foundry", Quantity: 1),
                    (Name: "Artifact Engine", Quantity: 1) });
        Must(seededPackages.Packages.Single(x => x.Theme == "Tokens")
                .Coverage.Contains("2 potential enablers") &&
             seededPackages.Packages.Single(x => x.Theme == "Tokens")
                .NextStep.Contains("No complete enabler/payoff"),
             "Multiple token makers alone must not be reported as a verified combo.");
        Must(!paperPackages.Packages.SelectMany(x => x.OwnedOptions.Concat(x.MissingOptions))
                 .Any(x => x.Name is "Lightning Bolt" or "Paper Banned Thopter" or "Selected Leader"),
             "Package lists must preserve identity, bans and commander exclusion.");
        var emptyPackageDraft = EdhLabSynergyPackages.Analyze(paper,
            Array.Empty<(string Name, int Quantity)>());
        Must(emptyPackageDraft.Packages.Single(x => x.Theme == "Artifacts")
                .OwnedOptions.Any(x => x.Name == "Sol Ring") &&
             emptyPackageDraft.Packages.Single(x => x.Theme == "Artifacts")
                .MissingOptions.Any(x => x.Name == "Thopter Foundry"),
             "Empty decks must show owned versus not recorded candidates distinctly.");
        var arenaPackages = EdhLabSynergyPackages.Analyze(arena,
            Array.Empty<(string Name, int Quantity)>());
        Must(!arenaPackages.Packages.SelectMany(x => x.OwnedOptions.Concat(x.MissingOptions))
                .Any(x => x.Name is "Non-Arena Thopters" or "Counterspell" or "Swords to Plowshares") &&
             arenaPackages.Packages.SelectMany(x => x.InDeck)
                .All(x => x.Name != "Sol Ring") &&
             arenaPackages.UnknownDeckCopies == 0 &&
             !arenaPackages.Packages.SelectMany(x => x.OwnedOptions)
                .Any(x => x.Name == "Sol Ring"),
             "Arena suggestions must not borrow paper-owned cards or unavailable cards.");
        // Keep the existing shared paper/Arena regression inventory stable:
        // the owned-payoff fixture must not change earlier Swap Lab rankings.
        var payoffInventory = new NativeBackupView(
            new[] { new NativePhysicalRow("Artifact Arrival", 1, "", "", "", "") },
            Array.Empty<NativeArenaRow>(), 0, 0, 0, 1, 0, 0, 1);
        var payoffPaper = EdhLabDraftCatalog.OpenReadOnly(catalog,
            payoffInventory, "Selected Leader", "WU", EdhLabFormat.PaperCommander);
        var ownedLink = EdhLabSynergyChains.Analyze(payoffPaper,
            new[] { (Name: "Clockwork Maker", Quantity: 1) });
        Must(ownedLink.Chains.Any(x =>
                x.Enabler == "Clockwork Maker" &&
                x.Payoff == "Artifact Arrival" &&
                x.Status == "Owned missing link" &&
                x.SuggestedCard == "Artifact Arrival" &&
                x.SuggestedRecordedCopies == 1),
             "Deck enabler must find recorded owned payoff as an unsaved suggestion.");
        Must(ownedLink.Chains.All(x =>
                x.Enabler != x.Payoff &&
                x.Payoff != "Off Color Arrival" &&
                x.Payoff != "Banned Arrival"),
             "Chains must exclude self-links, off-identity and banned cards.");
        var completeLink = EdhLabSynergyChains.Analyze(paper,
            new[] { (Name: "Clockwork Maker", Quantity: 1),
                    (Name: "Artifact Arrival", Quantity: 1) });
        Must(completeLink.Chains.Any(x =>
                x.Enabler == "Clockwork Maker" &&
                x.Payoff == "Artifact Arrival" &&
                x.Status == "Both in deck"),
             "Already-drafted interaction pair must take priority over missing links.");
        var missingLink = EdhLabSynergyChains.Analyze(paper,
            new[] { (Name: "Artifact Arrival", Quantity: 1) });
        Must(missingLink.Chains.Any(x =>
                x.Enabler == "Clockwork Maker" &&
                x.Payoff == "Artifact Arrival" &&
                x.Status == "Not recorded" &&
                x.SuggestedRecordedCopies == 0),
             "Missing physically unrecorded enabler must never be described as owned.");
        var arenaLink = EdhLabSynergyChains.Analyze(arena,
            new[] { (Name: "Clockwork Maker", Quantity: 1) });
        Must(arenaLink.Chains.All(x =>
                x.Status != "Owned missing link" &&
                x.Payoff != "Banned Arrival" &&
                x.Payoff != "Off Color Arrival"),
             "Arena chain hints cannot borrow paper-owned inventory or invalid cards.");
        var none = EdhLabSynergyChains.Analyze(paper,
            Array.Empty<(string Name, int Quantity)>());
        Must(none.Chains.Count == 0 &&
             EdhLabSynergyChains.Analyze(paper,
                new[] { (Name: "Missing Metadata Card", Quantity: 1) })
                .UnknownCopies == 1,
             "No chain without a card in the current deck; unknown draft cards are not analyzed.");
        bool invalidChainQuantity = false;
        try
        {
            EdhLabSynergyChains.Analyze(paper,
                new[] { (Name: "Clockwork Maker", Quantity: 0) });
        }
        catch (ArgumentOutOfRangeException) { invalidChainQuantity = true; }
        Must(invalidChainQuantity,
            "Invalid quantities must be refused by the synergy chain analyzer.");
        bool badPackageQuantity = false;
        try
        {
            EdhLabSynergyPackages.Analyze(paper, new[] { (Name: "Sol Ring", Quantity: 0) });
        }
        catch (ArgumentOutOfRangeException) { badPackageQuantity = true; }
        Must(badPackageQuantity, "Package analysis must reject zero quantities.");
        Must(paper.Synergy.EvaluateTheme("Artifacts", "Artifact", "Add {C}.").Matches &&
             !paper.Synergy.EvaluateTheme("Tokens", "Artifact", "Add {C}.").Matches &&
             !paper.Synergy.EvaluateTheme("Invented", "Artifact", "Add {C}.").Matches,
             "Package scoring must not copy fit scores across unrelated commander mechanics.");
        Must(paper.Contains("Sol Ring") && paper.Contains("Swords to Plowshares") &&
             paper.Contains("Counterspell") && paper.Contains("Commander Only") &&
             !paper.Contains("Lightning Bolt") && !paper.Contains("Three Color") &&
             !paper.Contains("Malformed Identity") &&
             !paper.Contains("Banned Card") && !paper.Contains("Not Legal") &&
             !paper.Contains("Selected Leader"),
             "Paper candidates must be legal, within commander identity and exclude commander itself.");
        Must(arena.Contains("Island") && arena.Contains("Brainstorm") &&
             !arena.Contains("Counterspell") &&
             !arena.Contains("Swords to Plowshares") &&
             !arena.Contains("Commander Only"),
             "Arena must enforce both digital printing availability and its own legality.");
        var paperOwned = paper.Search("", recordedOnly: true);
        var arenaOwned = arena.Search("", recordedOnly: true);
        Must(paperOwned.Select(x => x.Name).OrderBy(x => x)
                .SequenceEqual(new[] { "Counterspell", "Sol Ring", "Swords to Plowshares", "Tiny Insight" }) &&
             arenaOwned.Select(x => x.Name).OrderBy(x => x)
                .SequenceEqual(new[] { "Brainstorm", "Island" }),
             "Physical and Arena inventory names may not be combined.");
        Must(paper.Search("LIGHTNING", false).Count == 0 &&
             paper.Search("sol ring", false).Single().RecordedCopies == 1 &&
             paper.Search("island", false)
                 .Single(x => x.Name == "Island").RecordedCopies == 0 &&
             arena.Search("island", false)
                 .Single(x => x.Name == "Island").RecordedCopies == 4,
             "Unowned cards must appear as missing; ownership is name-only and format-separated.");
        Must(paper.Search("", false, "Ramp").Any(x => x.Name == "Sol Ring") &&
             !paper.Search("", false, "Ramp").Any(x => x.Name == "Island") &&
             paper.Search("", false, "Board Wipe").Any(x => x.Name == "Board Clear") &&
             paper.Search("", false, "Tutor").Any(x => x.Name == "Mystic Tutor") &&
             paper.Search("", false, "Removal").Any(x => x.Name == "Swords to Plowshares"),
             "Role hints should support practical collection-aware suggestions.");
        Must(paper.Find("Sol Ring")?.SuggestedCopyLimit == 1 &&
             paper.Find("Island")?.SuggestedCopyLimit == int.MaxValue &&
             paper.Find("Special Scholar")?.SuggestedCopyLimit == 7 &&
             paper.Find("Unlimited Scholar")?.SuggestedCopyLimit == int.MaxValue,
             "EDH draft additions must respect singleton, basic land and printed copy exceptions.");
        Must(paper.Search("", false, limit: 1).Count == 1 &&
             paper.Search("", false, limit: 1)[0].RecordedCopies > 0,
             "Bounded suggestions should put recorded eligible cards first.");
        Must(paper.Synergy.ThemeCount >= 2 &&
             paper.Synergy.ThemeSummary.Contains("Artifacts") &&
             paper.Synergy.ThemeSummary.Contains("Tokens") &&
             paper.SynergyEligibleCount > 0,
             "Commander text must activate transparent artifact/token mechanic signals.");
        var match = paper.SynergyFor("Thopter Foundry");
        Must(match is { Matches: true } &&
             match.Score > paper.SynergyFor("Sol Ring")!.Score &&
             match.Why.Contains("Artifacts") && match.Why.Contains("Tokens") &&
             paper.SynergyFor("Artifact Hater") is { Matches: false } &&
             paper.SynergyFor("Not In Catalog") is null,
             "Only positive artifact/token signals should match, with a specific explanation.");
        Must(paper.Search("Thopter Foundry", false, synergyOnly: true).Count == 1 &&
             paper.Search("Artifact Hater", false, synergyOnly: true).Count == 0 &&
             paper.Search("", false, synergyOnly: true, prioritizeSynergy: true)
                 .First().Name == "Sol Ring" &&
             paper.Search("", true, synergyOnly: true, prioritizeSynergy: true)
                 .All(x => x.RecordedCopies > 0),
             "Synergy-only search must remain recorded-first and cannot disguise missing cards as owned.");
        Must(!paper.Contains("Paper Banned Thopter") &&
             !arena.Contains("Non-Arena Thopters") &&
             !paper.Search("", false, synergyOnly: true)
                 .Any(x => x.Name == "Paper Banned Thopter") &&
             !arena.Search("", false, synergyOnly: true)
                 .Any(x => x.Name == "Non-Arena Thopters"),
             "Synergy ranking must never bypass the paper/Arena format filters.");
        var noSignal = EdhLabSynergyAdvisor.FromCommander(
            "Legendary Creature — Human", "Draw a card.");
        Must(noSignal.ThemeCount == 0 &&
             !noSignal.Evaluate("Artifact", "Create a treasure token.").Matches,
             "A commander with no identified strategy must not receive invented recommendations.");
        var typal = EdhLabSynergyAdvisor.FromCommander(
            "Legendary Artifact Creature — Borg", "Other Borg you control get +1/+1.");
        Must(typal.ThemeSummary.Contains("Borg typal") &&
             typal.Evaluate("Artifact Creature — Borg", "").Matches &&
             !typal.Evaluate("Instant", "Destroy target Borg creature.").Matches,
             "Typal matching must require commander rules-text evidence and favor actual type membership.");
        var emptyBlueprint = EdhLabDeckBlueprint.Analyze(paper,
            Array.Empty<(string Name, int Quantity)>());
        Must(emptyBlueprint.MainboardCopies == 0 &&
             emptyBlueprint.NextGap?.Role == "Land",
             "An empty 99-card draft should suggest a land foundation.");

        var blueprint = EdhLabDeckBlueprint.Analyze(paper,
            new (string Name, int Quantity)[]
            {
                ("Island", 12), ("Sol Ring", 1), ("Arcane Signet", 1),
                ("Brainstorm", 2), ("Board Clear", 1), ("Lightning Bolt", 1)
            });
        int CountRole(string role) =>
            blueprint.Roles.Single(x => x.Role == role).Copies;
        Must(blueprint.MainboardCopies == 18 &&
             blueprint.UnverifiedCopies == 1 &&
             CountRole("Land") == 12 && CountRole("Ramp") == 2 &&
             CountRole("Card Draw") == 2 && CountRole("Board Wipe") == 1 &&
             CountRole("Removal") == 0 &&
             blueprint.NextGap?.Role == "Removal",
             "Blueprint should count quantities, overlapping roles and unknown catalog entries.");
        Must(blueprint.Display.Contains("Mainboard 18/99") &&
             blueprint.Display.Contains("Suggested targets only"),
             "Deck role estimates must be explicitly advisory, not deck legality rules.");
        bool badBlueprintRejected = false;
        try
        {
            EdhLabDeckBlueprint.Analyze(paper,
                new[] { (Name: "Island", Quantity: 0) });
        }
        catch (ArgumentOutOfRangeException) { badBlueprintRejected = true; }
        Must(badBlueprintRejected, "Blueprint must reject nonpositive quantities.");

        static (string Name, int Quantity)[] ManaRows(string text) =>
            NativeDeckEditing.ParseMainboard(text)
                .Select(x => (x.Name, x.Quantity)).ToArray();

        // Arena-style new deck: even baseline basics; then dual/flexible
        // lands consume land slots and change the effective color spread.
        var blankPlan = EdhLabManaPlanner.Analyze(paper,
            Array.Empty<(string Name, int Quantity)>(), "WU", 36);
        Must(blankPlan.CanApply && blankPlan.DesiredBasics == 36 &&
             blankPlan.TotalAfter == 36 && blankPlan.CopiesAfter == 36 &&
             blankPlan.Basics.Single(x => x.Name == "Plains").Quantity == 18 &&
             blankPlan.Basics.Single(x => x.Name == "Island").Quantity == 18 &&
             EdhLabManaPlanner.Apply("", "WU", blankPlan) ==
                 "18 Plains\n18 Island",
             "Fresh two-color Commander drafts should start with an even 36-land basic split.");
        var dualEntries = ManaRows(
            "1 Azorius Guildgate\n18 Plains\n18 Island\n1 Brainstorm");
        var dualPlan = EdhLabManaPlanner.Analyze(paper, dualEntries, "WU", 36);
        string dualAdjusted = EdhLabManaPlanner.Apply(
            "1 Azorius Guildgate\n18 Plains\n18 Island\n1 Brainstorm",
            "WU", dualPlan);
        Must(dualPlan.CurrentLands == 37 && dualPlan.FixedLands == 1 &&
             dualPlan.FlexibleLands == 1 && dualPlan.DesiredBasics == 35 &&
             dualPlan.TotalAfter == 36 &&
             dualPlan.Basics.Sum(x => x.Quantity) == 35 &&
             dualAdjusted.Contains("1 Azorius Guildgate") &&
             dualAdjusted.Contains("1 Brainstorm") &&
             NativeDeckEditing.ParseMainboard(dualAdjusted)
                 .Sum(x => x.Quantity) == 37,
             "Adding a two-color land must consume one basic slot, not add two land slots.");
        var utilityEntries = ManaRows(
            "1 Azorius Guildgate\n1 Command Tower\n1 Reliquary Tower\n" +
            "1 Snow-Covered Island\n12 Plains\n12 Island");
        var utilityPlan = EdhLabManaPlanner.Analyze(paper, utilityEntries, "WU", 36);
        string utilityAdjusted = EdhLabManaPlanner.Apply(
            "1 Azorius Guildgate\n1 Command Tower\n1 Reliquary Tower\n" +
            "1 Snow-Covered Island\n12 Plains\n12 Island", "WU", utilityPlan);
        Must(utilityPlan.FixedLands == 4 && utilityPlan.FlexibleLands == 2 &&
             utilityPlan.UnclassifiedLands == 1 &&
             utilityPlan.DesiredBasics == 32 && utilityPlan.TotalAfter == 36 &&
             utilityAdjusted.Contains("1 Snow-Covered Island") &&
             utilityAdjusted.Contains("1 Reliquary Tower") &&
             Math.Abs(utilityPlan.ApproximateColoredSources["W"] -
                      utilityPlan.ApproximateColoredSources["U"]) <= 1.01,
             "Multicolor and rainbow lands should add fractional access; utility/snow lands stay intact.");
        var unknownPlan = EdhLabManaPlanner.Analyze(paper,
            ManaRows("1 Mystery Land"), "WU", 36);
        bool unknownRejected = false;
        try { EdhLabManaPlanner.Apply("1 Mystery Land", "WU", unknownPlan); }
        catch (InvalidDataException) { unknownRejected = true; }
        Must(!unknownPlan.CanApply && unknownRejected &&
             unknownPlan.UnverifiedCards.Contains("Mystery Land"),
             "Unknown card names must never trigger destructive land rewriting.");
        var fullPlan = EdhLabManaPlanner.Analyze(paper,
            ManaRows("70 Brainstorm\n25 Counterspell"),
            "WU", 36);
        Must(fullPlan.CopiesAfter == 99 && fullPlan.TotalAfter == 4 &&
             fullPlan.Summary.Contains("Not enough space"),
             "Auto basics must cap at 99 cards when the user adds many nonland copies.");
        var colorlessPlan = EdhLabManaPlanner.Analyze(paper,
            Array.Empty<(string Name, int Quantity)>(), "", 36);
        Must(colorlessPlan.Basics.Single().Name == "Wastes" &&
             colorlessPlan.Basics.Single().Quantity == 36,
             "Colorless commander land guidance must use Wastes, not off-identity colored basics.");
        var shiftedBlueprint = EdhLabDeckBlueprint.Analyze(paper,
            new[] { (Name: "Island", Quantity: 12) }, landTarget: 40);
        Must(shiftedBlueprint.Roles.Single(x => x.Role == "Land").SuggestedMinimum == 40,
             "Changing the land target must also update the live blueprint guidance.");

        // v0.9.36 catalogs without mana_cost continue to be readable and
        // retain the proven even-color split until Full Sync rebuilds them.
        var legacyPips = EdhLabManaPlanner.Analyze(paper,
            ManaRows("6 Brainstorm\n2 Swords to Plowshares"), "WU",
            36, weightSpellPips: true);
        Must(!paper.HasManaCostMetadata && !legacyPips.UsingSpellPips &&
             legacyPips.Basics.Single(x => x.Name == "Plains").Quantity == 18 &&
             legacyPips.Basics.Single(x => x.Name == "Island").Quantity == 18 &&
             legacyPips.Summary.Contains("Full Sync"),
             "Older catalogs must fall back safely and disclose that a refresh is needed.");

        // Create a separate *new-schema* fixture; original read-only catalog
        // must remain byte-identical after recommendation and balancing.
        string costCatalog = Path.Combine(work, "edh-pip-catalog.sqlite");
        File.Copy(catalog, costCatalog);
        using (var db = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = costCatalog, Mode = SqliteOpenMode.ReadWrite,
            Pooling = false
        }.ToString()))
        {
            db.Open();
            using var change = db.CreateCommand();
            change.CommandText = """
                ALTER TABLE cards ADD COLUMN mana_cost TEXT NOT NULL DEFAULT '';
                ALTER TABLE cards ADD COLUMN mv REAL NOT NULL DEFAULT 0;
                UPDATE cards SET mv = CASE name
                    WHEN 'Brainstorm' THEN 1
                    WHEN 'Swords to Plowshares' THEN 1
                    WHEN 'Counterspell' THEN 2
                    WHEN 'Sol Ring' THEN 1
                    WHEN 'Arcane Signet' THEN 2
                    WHEN 'Thopter Foundry' THEN 2
                    WHEN 'Board Clear' THEN 7
                    WHEN 'Heavy Insight' THEN 7
                    WHEN 'Tiny Insight' THEN 2
                    WHEN 'Heavy Generic' THEN 7
                    WHEN 'Artifact Engine' THEN 7
                    ELSE 0 END;
                UPDATE cards SET mana_cost = CASE name
                    WHEN 'Brainstorm' THEN '{U}'
                    WHEN 'Swords to Plowshares' THEN '{W}'
                    WHEN 'Counterspell' THEN '{U}{U}'
                    WHEN 'Thopter Foundry' THEN '{1}{U}'
                    WHEN 'Heavy Insight' THEN '{5}{U}{U}'
                    WHEN 'Tiny Insight' THEN '{1}{U}'
                    WHEN 'Heavy Generic' THEN '{6}{W}'
                    WHEN 'Artifact Engine' THEN '{5}{U}{U}'
                    ELSE '' END;
                """;
            change.ExecuteNonQuery();
        }
        var pipPaper = EdhLabDraftCatalog.OpenReadOnly(costCatalog, ownership,
            "Selected Leader", "WU", EdhLabFormat.PaperCommander);
        var spells = ManaRows("6 Brainstorm\n2 Swords to Plowshares");
        var even = EdhLabManaPlanner.Analyze(pipPaper, spells, "WU", 36);
        var weighted = EdhLabManaPlanner.Analyze(pipPaper, spells, "WU",
            36, weightSpellPips: true);
        Must(pipPaper.HasManaCostMetadata && !even.UsingSpellPips &&
             weighted.UsingSpellPips && weighted.ParsedManaCostCopies == 8 &&
             weighted.MissingManaCostCopies == 0 &&
             weighted.ColoredPipDemand["U"] == 6 &&
             weighted.ColoredPipDemand["W"] == 2 &&
             even.Basics.Single(x => x.Name == "Plains").Quantity == 18 &&
             weighted.Basics.Single(x => x.Name == "Plains").Quantity <
                weighted.Basics.Single(x => x.Name == "Island").Quantity &&
             weighted.TotalAfter == 36 &&
             weighted.Summary.Contains("Spell-pip mode"),
             "A blue-heavy draft must shift basics toward blue without changing land target.");
        var weightedWithDual = EdhLabManaPlanner.Analyze(pipPaper,
            ManaRows("6 Brainstorm\n2 Swords to Plowshares\n1 Azorius Guildgate"),
            "WU", 36, weightSpellPips: true);
        Must(weightedWithDual.TotalAfter == 36 &&
             weightedWithDual.FlexibleLands == 1 &&
             weightedWithDual.DesiredBasics == 35 &&
             weightedWithDual.Basics.Single(x => x.Name == "Island").Quantity >
                weightedWithDual.Basics.Single(x => x.Name == "Plains").Quantity,
             "Blue-heavy color demand must survive adding a dual as exactly one land.");
        var hybrid = EdhLabColoredPips.Count("{W/U}{2/W}{U/P}{3}{X}{C}");
        Must(Math.Abs(hybrid[0] - 1.0) < .00001 &&
             Math.Abs(hybrid[1] - 1.0) < .00001 &&
             hybrid.Skip(2).All(x => x == 0) &&
             EdhLabColoredPips.Count("{2}{W}{W} // {4}{U}")[0] == 2 &&
             EdhLabColoredPips.Count("{2}{W}{W} // {4}{U}")[1] == 0,
             "Hybrid, phyrexian, generic, split and colorless cost symbols must be approximated explicitly.");
        var onlyGeneric = EdhLabColoredPips.Count("{X}{C}{4}{S}");
        Must(onlyGeneric.All(x => x == 0) &&
             !EdhLabManaPlanner.Analyze(pipPaper,
                 Array.Empty<(string Name,int Quantity)>(), "WU", 36,
                 weightSpellPips: true).UsingSpellPips,
             "Empty decks must retain the even split even when weighting is enabled.");

        // Land-drop probabilities are exact hypergeometric estimates for
        // the explicitly described sampling assumption: a shuffled 99-card
        // mainboard, 7-card opening hand and a draw from turn two onward.
        Must(Math.Abs(EdhLabCurveAnalysis.ProbabilityAtLeast(36, 8, 2) - .8609) < .002 &&
             Math.Abs(EdhLabCurveAnalysis.ProbabilityAtLeast(36, 9, 3) - .7037) < .002 &&
             EdhLabCurveAnalysis.ProbabilityAtLeast(0, 10, 1) == 0 &&
             EdhLabCurveAnalysis.ProbabilityAtLeast(99, 10, 5) == 1 &&
             EdhLabCurveAnalysis.ProbabilityAtLeast(36, 0, 0) == 1 &&
             EdhLabCurveAnalysis.ProbabilityAtLeast(36, 9, 3) >
                 EdhLabCurveAnalysis.ProbabilityAtLeast(30, 9, 3),
             "Land-drop estimator must match known 99-card hypergeometric results.");

        var curveEntries = ManaRows(
            "6 Brainstorm\n2 Swords to Plowshares\n1 Counterspell");
        var curvePlan = EdhLabManaPlanner.Analyze(pipPaper,
            curveEntries, "WU", 36, weightSpellPips: true);
        var curve = EdhLabCurveAnalysis.Analyze(pipPaper, curveEntries, curvePlan);
        Must(curve.MainboardCopies == 9 &&
             curve.VerifiedNonlandCopies == 9 &&
             curve.Buckets[1] == 8 && curve.Buckets[2] == 1 &&
             curve.Buckets.Sum() == 9 &&
             Math.Abs(curve.AverageManaValue - (10.0 / 9)) < .001 &&
             curve.ManaCostCopies == 9 && curve.MissingCostCopies == 0 &&
             !curve.UsesActualLands && curve.LandCountForOdds == 36 &&
             curve.LandDrops.Count == 4 &&
             Math.Abs(curve.LandDrops.Single(x => x.Turn == 3).Chance -
                 .7037) < .002 &&
             curve.Display.Contains("Projected land-drop odds") &&
             curve.Display.Contains("Nonland mana curve") &&
             curve.Display.Contains("not simulated", StringComparison.OrdinalIgnoreCase),
             "Projected curve should use cached mana values, live draft counts and explicit odds assumptions.");

        // Two U pips on turn two in a white-heavy deck create a measurable
        // blue-source bottleneck. Marginal odds are NOT cast probabilities.
        var pressureRows = ManaRows("1 Counterspell\n10 Swords to Plowshares");
        var pressurePlan = EdhLabManaPlanner.Analyze(pipPaper,
            pressureRows, "WU", 36, weightSpellPips: true);
        var pressure = EdhLabCurveAnalysis.Analyze(
            pipPaper, pressureRows, pressurePlan);
        Must(pressure.ColorWarnings.Any(x => x.Name == "Counterspell" &&
                x.Color == "U" && x.RequiredSources == 2 &&
                x.ByTurn == 2 && x.MarginalChance < .60) &&
             pressure.Display.Contains("NOT cast odds"),
             "Strict double-colored spells should show marginal source pressure, not invented cast odds.");

        var completeEntries = ManaRows(
            "30 Plains\n22 Island\n47 Brainstorm");
        var completePlan = EdhLabManaPlanner.Analyze(pipPaper,
            completeEntries, "WU", 36, weightSpellPips: true);
        var complete = EdhLabCurveAnalysis.Analyze(
            pipPaper, completeEntries, completePlan);
        Must(complete.UsesActualLands && complete.MainboardCopies == 99 &&
             complete.LandCountForOdds == 52 &&
             completePlan.CurrentColoredSources["W"] == 30 &&
             completePlan.CurrentColoredSources["U"] == 22 &&
             complete.LandDrops.Single(x => x.Turn == 3).Chance >
                curve.LandDrops.Single(x => x.Turn == 3).Chance &&
             complete.Display.Contains("current 99-card mainboard"),
             "Complete saved decks must use actual lands rather than planned targets.");

        var legacyCurve = EdhLabCurveAnalysis.Analyze(paper,
            ManaRows("1 Brainstorm\n1 Counterspell"),
            EdhLabManaPlanner.Analyze(paper,
                ManaRows("1 Brainstorm\n1 Counterspell"), "WU", 36));
        Must(!legacyCurve.HasManaCosts &&
             legacyCurve.ColorWarnings.Count == 0 &&
             legacyCurve.Display.Contains("Full Sync") &&
             legacyCurve.LandDrops.Count == 4,
             "Old catalogs must keep numerical land odds without fabricating pip data.");
        var unknownCurveRows = ManaRows("1 Mystery Land");
        var unknownCurve = EdhLabCurveAnalysis.Analyze(pipPaper,
            unknownCurveRows,
            EdhLabManaPlanner.Analyze(pipPaper, unknownCurveRows, "WU", 36));
        Must(unknownCurve.UnverifiedCopies == 1 &&
             unknownCurve.LandDrops.Count == 0 &&
             unknownCurve.Display.Contains("unavailable"),
             "Unknown card names must halt probability estimates rather than silently assuming their type.");

        // Advisory v0.9.39 optimization: no false high-curve diagnosis
        // from a sparse unfinished draft, and no replacement without consent.
        var earlyPicks = EdhLabDeckOptimizer.Analyze(pipPaper,
            ManaRows("10 Plains\n10 Island\n1 Brainstorm"));
        Must(pipPaper.HasManaValueMetadata &&
             !earlyPicks.IsMatureEnoughToJudge &&
             earlyPicks.Opportunities.Count == 0 &&
             earlyPicks.CanBrowse &&
             earlyPicks.OwnedCandidates.All(x => x.ManaValue <= 2 &&
                 x.UnusedRecordedCopies > 0) &&
             earlyPicks.OwnedCandidates.Any(x => x.Name == "Sol Ring") &&
             earlyPicks.OwnedCandidates.Any(x => x.Name == "Swords to Plowshares") &&
             !earlyPicks.OwnedCandidates.Any(x => x.Name == "Lightning Bolt") &&
             !earlyPicks.OwnedCandidates.Any(x => x.Name == "Board Clear"),
             "Incomplete decks must avoid premature curve diagnoses and suggest only legal recorded early spells.");

        var exhausted = EdhLabDeckOptimizer.Analyze(pipPaper,
            ManaRows("1 Sol Ring\n1 Swords to Plowshares"));
        Must(!exhausted.OwnedCandidates.Any(x => x.Name == "Sol Ring" ||
                x.Name == "Swords to Plowshares") &&
             exhausted.OwnedCandidates.Any(x => x.Name == "Counterspell"),
             "A recorded copy already in the draft must not appear as an unused singleton upgrade.");

        var pipArena = EdhLabDraftCatalog.OpenReadOnly(costCatalog, ownership,
            "Selected Leader", "WU", EdhLabFormat.ArenaBrawl);
        var arenaPicks = EdhLabDeckOptimizer.Analyze(pipArena,
            ManaRows("1 Island"));
        Must(arenaPicks.OwnedCandidates.Any(x => x.Name == "Brainstorm") &&
             !arenaPicks.OwnedCandidates.Any(x =>
                 x.Name == "Counterspell" ||
                 x.Name == "Swords to Plowshares" ||
                 x.Name == "Sol Ring"),
             "Arena suggestions must use Arena quantities and Brawl legality, never physical inventory.");
        var noManaValues = EdhLabDeckOptimizer.Analyze(paper,
            ManaRows("1 Brainstorm"));
        Must(!paper.HasManaValueMetadata &&
             !noManaValues.HasManaValues && !noManaValues.CanBrowse &&
             noManaValues.Display.Contains("Full Sync"),
             "Legacy catalogs without mana value data must not fabricate cheap-card suggestions.");

        // These quantities are purely synthetic deck-shape inputs to the
        // read-only engine; legal singleton/save checks remain independent.
        // A substantial, top-heavy list warrants a prompt to review slots.
        var topHeavy = EdhLabDeckOptimizer.Analyze(pipPaper,
            ManaRows("10 Board Clear\n20 Brainstorm"));
        Must(topHeavy.IsMatureEnoughToJudge &&
             topHeavy.KnownNonlandCopies == 30 &&
             topHeavy.EarlySpellCopies == 20 &&
             topHeavy.ExpensiveSpellCopies == 10 &&
             topHeavy.Opportunities.Any(x => x.Code == "high-end") &&
             !topHeavy.Opportunities.Any(x => x.Code == "early") &&
             topHeavy.ExpensiveCards.Contains("Board Clear") &&
             topHeavy.Display.Contains("not automatic cuts"),
             "Heavy 6+ mana concentration should flag a review, never delete or condemn finishers.");

        var thinEarly = EdhLabDeckOptimizer.Analyze(pipPaper,
            ManaRows("5 Brainstorm\n25 Board Clear"));
        Must(thinEarly.Opportunities.Any(x => x.Code == "early") &&
             thinEarly.Opportunities.Any(x => x.Code == "high-end") &&
             thinEarly.Display.Contains("Thin early spell coverage"),
             "Substantial drafts with few 0–2 MV spells need an actionable low-curve warning.");

        var unknownOptimization = EdhLabDeckOptimizer.Analyze(pipPaper,
            ManaRows("1 Totally Unmatched Card"));
        Must(unknownOptimization.UnknownCopies == 1 &&
             !unknownOptimization.CanBrowse &&
             unknownOptimization.Opportunities.Count == 0 &&
             unknownOptimization.Display.Contains("Advice paused"),
             "Unknown cards must pause concrete improvement advice rather than guessing mana value.");

        var fullOptimization = EdhLabDeckOptimizer.Analyze(pipPaper,
            ManaRows("30 Plains\n34 Island\n35 Brainstorm"));
        Must(fullOptimization.MainboardCopies == 99 &&
             fullOptimization.Display.Contains("Mainboard is full") &&
             fullOptimization.CanBrowse,
             "A full mainboard can suggest review-only swaps but must never add cards automatically.");

        // v0.9.40: propose only explainable, one-for-one, format-legal
        // owned replacements from a sufficiently developed nonland list.
        // High duplicate quantities are synthetic shape fixtures only;
        // native singleton deck legality still belongs to save preflight.
        const string swapDraft = "1 Heavy Insight\n1 Board Clear\n" +
            "1 Artifact Engine\n24 Heavy Generic";
        var swaps = EdhLabSwapAdvisor.Analyze(pipPaper, ManaRows(swapDraft));
        Must(swaps.CanSwap && swaps.KnownNonlandCopies == 27 &&
             swaps.Proposals.Count > 0 &&
             swaps.Proposals.Any(x => x.OutName == "Heavy Insight" &&
                 x.InName == "Tiny Insight" &&
                 x.OutManaValue == 7 && x.InManaValue == 2 &&
                 x.RetainedRoles.Contains("Card Draw")) &&
             !swaps.Proposals.Any(x => x.OutName == "Board Clear") &&
             !swaps.Proposals.Any(x => x.OutName == "Artifact Engine") &&
             swaps.Proposals.All(x => x.AvailableRecordedCopies > 0 &&
                 x.InManaValue <= 3 && x.OutManaValue - x.InManaValue >= 2),
             "Swap Lab should retain scarce draw and wipe roles, protect commander artifact synergy and recommend unused owned low-cost cards.");
        var selectedSwap = swaps.Proposals.Single(x => x.OutName == "Heavy Insight");
        Must(EdhLabSwapAdvisor.IsCurrent(swaps, selectedSwap) &&
             !EdhLabSwapAdvisor.IsCurrent(
                 EdhLabSwapAdvisor.Analyze(pipPaper,
                     ManaRows(swapDraft + "\n1 Tiny Insight")), selectedSwap),
             "A proposed swap must become stale when its final singleton inventory copy is already used.");
        string swappedDraft = NativeDeckOwnedPicker.AddOne(
            NativeDeckOwnedPicker.RemoveOne(swapDraft, selectedSwap.OutName),
            selectedSwap.InName);
        Must(NativeDeckEditing.ParseMainboard(swappedDraft)
                 .Sum(x => x.Quantity) ==
                 NativeDeckEditing.ParseMainboard(swapDraft).Sum(x => x.Quantity) &&
             swappedDraft.Contains("1 Tiny Insight") &&
             !swappedDraft.Contains("Heavy Insight") &&
             swapDraft.Contains("1 Heavy Insight"),
             "One-for-one apply must preserve the draft copy count and leave the source draft intact.");
        var arenaSwap = EdhLabSwapAdvisor.Analyze(pipArena, ManaRows(
            "1 Heavy Insight\n25 Heavy Generic"));
        Must(arenaSwap.Proposals.Any(x => x.OutName == "Heavy Insight" &&
                x.InName == "Brainstorm") &&
             arenaSwap.Proposals.All(x => x.InName != "Tiny Insight" &&
                 x.InName != "Swords to Plowshares" && x.InName != "Sol Ring"),
             "Arena Brawl swaps may use only Arena recorded and legal alternatives, never paper-only holdings.");
        var tooSparse = EdhLabSwapAdvisor.Analyze(pipPaper,
            ManaRows("1 Heavy Insight\n1 Heavy Generic"));
        Must(!tooSparse.CanSwap && tooSparse.Display.Contains("incomplete draft") &&
             !EdhLabSwapAdvisor.Analyze(paper, ManaRows(swapDraft)).CanSwap &&
             !EdhLabSwapAdvisor.Analyze(pipPaper,
                 ManaRows(swapDraft + "\n1 Unknown Card")).CanSwap &&
             !EdhLabSwapAdvisor.Analyze(pipPaper,
                 ManaRows(swapDraft + "\n100 Plains")).CanSwap,
             "Underdeveloped, old-schema, unknown and oversized decks should not receive unverified cut proposals.");

        // v0.9.44 strategy intent is a transient, read-only ranking lens.
        // The same local catalog, inventory and mainboard produce differently
        // ranked suggestions without changing quantity or legality gates.
        var intentRows = ManaRows("10 Plains\n10 Island\n1 Brainstorm");
        var faster = EdhLabDeckOptimizer.Analyze(pipPaper, intentRows,
            focus: EdhLabStrategyFocus.FastStart);
        var cardAdvantage = EdhLabDeckOptimizer.Analyze(pipPaper, intentRows,
            focus: EdhLabStrategyFocus.CardAdvantage);
        var interaction = EdhLabDeckOptimizer.Analyze(pipPaper, intentRows,
            focus: EdhLabStrategyFocus.Interaction);
        Must(faster.OwnedCandidates.First().Name == "Sol Ring" &&
             cardAdvantage.OwnedCandidates.First().Name == "Tiny Insight" &&
             interaction.OwnedCandidates.First().Name == "Swords to Plowshares" &&
             cardAdvantage.Display.Contains("Card advantage") &&
             faster.OwnedCandidates.All(x => x.UnusedRecordedCopies > 0) &&
             interaction.OwnedCandidates.All(x => x.ManaValue <= 2),
             "Intent selection must re-rank only locally eligible recorded early cards, not alter inventory.");
        var strategyInventory = ownership with
        {
            PhysicalCards = ownership.PhysicalCards.Concat(new[]
            {
                new NativePhysicalRow("Thopter Foundry", 1, "", "", "", "")
            }).ToArray()
        };
        var strategyCatalog = EdhLabDraftCatalog.OpenReadOnly(costCatalog,
            strategyInventory, "Selected Leader", "WU",
            EdhLabFormat.PaperCommander);
        var synergyFirst = EdhLabDeckOptimizer.Analyze(strategyCatalog,
            intentRows, focus: EdhLabStrategyFocus.CommanderSynergy);
        Must(synergyFirst.OwnedCandidates.First().Name == "Thopter Foundry" &&
             synergyFirst.OwnedCandidates.First().SynergyScore > 0 &&
             !synergyFirst.OwnedCandidates.Any(x => x.Name == "Lightning Bolt"),
             "Commander-synergy focus must prioritize actual recognized mechanics without admitting off-color cards.");
        var focusedSwaps = EdhLabSwapAdvisor.Analyze(strategyCatalog,
            ManaRows(swapDraft), focus: EdhLabStrategyFocus.CommanderSynergy);
        var baselineSwaps = EdhLabSwapAdvisor.Analyze(strategyCatalog,
            ManaRows(swapDraft));
        Must(focusedSwaps.CanSwap &&
             focusedSwaps.Proposals.Any(x =>
                 x.OutName == "Heavy Generic" &&
                 x.InName == "Thopter Foundry" &&
                 x.Explanation.Contains("Commander synergy")) &&
             !focusedSwaps.Proposals.Any(x => x.OutName == "Board Clear") &&
             focusedSwaps.Proposals.All(x =>
                 (strategyCatalog.SynergyFor(x.InName)?.Score ?? 0) >=
                 (strategyCatalog.SynergyFor(x.OutName)?.Score ?? 0)) &&
             !EdhLabSwapAdvisor.IsCurrent(baselineSwaps,
                 focusedSwaps.Proposals.First()) &&
             focusedSwaps.Proposals.All(x => x.Focus ==
                 EdhLabStrategyFocus.CommanderSynergy),
             "Swap proposals must honor the chosen priority, preserve guarded roles, and reject stale focus changes.");
        var strategyArena = EdhLabDeckOptimizer.Analyze(pipArena,
            ManaRows("1 Island"), focus: EdhLabStrategyFocus.Interaction);
        Must(strategyArena.OwnedCandidates.All(x =>
                x.Name != "Swords to Plowshares" &&
                x.Name != "Tiny Insight" &&
                x.Name != "Sol Ring"),
             "Strategy preferences must never bypass Arena Brawl format or recorded inventory limits.");

        Must(originalHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(catalog))),
             "Deck drafting candidate search must never write or alter the Scryfall catalog.");

        Console.WriteLine("PASS: EDH Lab draft candidate filtering (color identity, paper/Arena legality, roles, missing and recorded holdings)");
    }
}
