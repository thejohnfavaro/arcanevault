using ArcaneVault.Next.Collection;
using ArcaneVault.Next.Domain;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text.Json;

internal static class CollectionCatalogReviewChecks
{
    private static void Check(bool ok, string description)
    {
        if (!ok) throw new InvalidOperationException("Native verified collection: " + description);
    }

    public static void Run(string work)
    {
        string backupPath = Path.Combine(work, "review-full-backup.json");
        string catalogPath = Path.Combine(work, "review-catalog.sqlite");
        const string knownA = "00000000-0000-4000-8000-000000000111";
        const string knownB = "00000000-0000-4000-8000-000000000222";
        const string unknown = "00000000-0000-4000-8000-000000000999";

        File.WriteAllText(backupPath, JsonSerializer.Serialize(new
        {
            schema = 2,
            cards = new[] { new { name = "Arena Spell", qty = 3 } },
            physicalCards = new object[]
            {
                new { name = "Split // Back", qty = 2, set = "AAA", number = "1", finish = "foil", scryfallId = knownA },
                new { name = "Split // Back", qty = 3, set = "AAA", number = "1", finish = "etched", scryfallId = knownA },
                new { name = "Different Face", qty = 4, set = "BBB", number = "2", finish = "nonfoil", scryfallId = knownB },
                new { name = "Unidentified", qty = 5, set = "AAA", number = "1", finish = "nonfoil", scryfallId = "" },
                new { name = "Malformed", qty = 1, set = "AAA", number = "2", finish = "foil", scryfallId = "not-a-uuid" },
                new { name = "Catalog Missing", qty = 2, set = "AAA", number = "3", finish = "foil", scryfallId = unknown },
                new { name = "A Different Card", qty = 1, set = "AAA", number = "1", finish = "foil", scryfallId = knownA },
                new { name = "Split // Back", qty = 2, set = "CCC", number = "1", finish = "foil", scryfallId = knownA },
                new { name = "Split // Back", qty = 1, set = "AAA", number = "99", finish = "foil", scryfallId = knownA }
            },
            deckLibrary = new[] { new { name = "Paper Deck", entries = Array.Empty<object>() } },
            preferredArtwork = new { a = "image" }
        }));
        using (var db = new SqliteConnection(new SqliteConnectionStringBuilder
               { DataSource = catalogPath, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false }.ToString()))
        {
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE variants(print_id TEXT PRIMARY KEY, name TEXT NOT NULL,
                    setcode TEXT NOT NULL, collector TEXT NOT NULL,
                    games TEXT NOT NULL, digital INTEGER NOT NULL,
                    setname TEXT NOT NULL, release TEXT NOT NULL);
                INSERT INTO variants VALUES('00000000-0000-4000-8000-000000000111',
                    'Split // Back', 'AAA', '1','paper,arena',0,'Alpha','2026-01-01');
                INSERT INTO variants VALUES('00000000-0000-4000-8000-000000000222',
                    'Different Face', 'BBB', '2','paper',0,'Beta','2026-02-01');
                CREATE TABLE cards(name TEXT PRIMARY KEY, type TEXT NOT NULL,
                    oracle TEXT NOT NULL, rarity TEXT NOT NULL, mv REAL NOT NULL,
                    colors TEXT NOT NULL, legalities TEXT NOT NULL, setcode TEXT NOT NULL);
                INSERT INTO cards VALUES('Split // Back', 'Legendary Creature — Test',
                    'When this enters, draw a card.', 'rare', 3, 'UB',
                    '{"commander":"legal","brawl":"legal"}','AAA');
                INSERT INTO cards VALUES('Arena Spell','Sorcery','Draw two cards.',
                    'common',2,'U','{"commander":"legal","brawl":"legal"}','AAA');
                INSERT INTO cards VALUES('Sphinx''s Tutelage','Enchantment',
                    'Whenever you draw a card, target opponent mills two cards.',
                    'uncommon',3,'U','{"commander":"legal","brawl":"legal"}','ORI');
                INSERT INTO cards VALUES('Emry, Lurker of the Loch','Legendary Creature',
                    'Tap: Cast an artifact card from your graveyard.',
                    'rare',3,'U','{"commander":"legal","brawl":"legal"}','ELD');
                INSERT INTO cards VALUES('The One Ring', 'Legendary Artifact',
                    'Protection from everything.', 'mythic',4,'',
                    '{"commander":"legal","brawl":"legal"}','LTR');
                INSERT INTO cards VALUES('Thoughtcast', 'Sorcery',
                    'Draw two cards.', 'common',5,'U',
                    '{"commander":"legal","brawl":"legal"}','MRD');
                INSERT INTO cards VALUES('Tezzeret, Master of the Bridge','Legendary Planeswalker',
                    'Affinity for artifacts.', 'mythic',6,'UB',
                    '{"commander":"legal","brawl":"legal"}','WAR');
                INSERT INTO cards VALUES('Tezzeret, Agent of Bolas','Legendary Planeswalker',
                    'Look at the top five cards.', 'mythic',4,'UB',
                    '{"commander":"legal","brawl":"legal"}','MBS');
                INSERT INTO cards VALUES('Dawnsire, Sunstar Dreadnought','Artifact Creature',
                    'Create artifact creature tokens.', 'rare',6,'',
                    '{"commander":"legal","brawl":"legal"}','TST');
                CREATE TABLE ownership(name TEXT, qty INTEGER);
                INSERT INTO ownership VALUES('Separate installed inventory mirror',37);
                """;
            cmd.ExecuteNonQuery();
        }

        var beforeBackup = SHA256.HashData(File.ReadAllBytes(backupPath));
        var beforeCatalog = SHA256.HashData(File.ReadAllBytes(catalogPath));
        var sourceMtime = File.GetLastWriteTimeUtc(backupPath);
        var catalogMtime = File.GetLastWriteTimeUtc(catalogPath);
        var backup = NativeBackupReader.Read(backupPath);
        var unverified = CollectionCatalogReview.Examine(backup, null);
        Check(!unverified.CatalogVerified && unverified.ConfirmedCopies == 0 &&
              unverified.UnresolvedCopies == 21 && unverified.ConfirmedSets.Count == 0,
            "An absent catalog must not credit any printing.");
        var verified = CollectionCatalogReview.Examine(backup, catalogPath);
        Check(verified.CatalogVerified && verified.TotalCopies == 21 &&
              verified.ConfirmedCopies == 9 && verified.UnresolvedCopies == 12 &&
              verified.QuantitiesConserved, "Verified/unresolved quantity conservation failed.");
        Check(verified.Rows.Count == 9 &&
              verified.ConfirmedDistinctPrintings == 2 && verified.ConfirmedSets.Count == 2,
            "Duplicate copies/finishes collapsed or distinct printing counts changed.");
        Check(verified.ConfirmedSets.Single(s => s.SetCode == "AAA").Copies == 5 &&
              verified.ConfirmedSets.Single(s => s.SetCode == "AAA").DistinctPrintings == 1 &&
              verified.ConfirmedSets.Single(s => s.SetCode == "BBB").Copies == 4,
            "Set totals must credit catalog-confirmed IDs only.");
        Check(verified.Rows[0].Confirmed && verified.Rows[1].Confirmed &&
              verified.Rows[2].Confirmed && verified.Rows.Skip(3).All(r => !r.Confirmed),
            "Only explicit verified matches should count.");
        Check(verified.Rows[3].Status == "Missing printing ID" &&
              verified.Rows[4].Status == "Invalid printing ID" &&
              verified.Rows[5].Status == "Printing ID absent from catalog" &&
              verified.Rows[6].Status == "Card name conflicts with printing ID" &&
              verified.Rows[7].Status == "Set conflicts with printing ID" &&
              verified.Rows[8].Status == "Collector number conflicts with printing ID",
            "Incorrect unresolved printing reasons.");
        var details = CollectionCatalogReview.ReadDetails(catalogPath, "Split // Back");
        Check(details is not null && details.TypeLine.Contains("Creature") &&
              details.OracleText.Contains("draw a card") &&
              details.ColorIdentity == "UB" && details.Rarity == "rare" &&
              details.ManaValue == 3 && details.ManaCost == "",
            "Older Scryfall catalogs without mana_cost must still load without inventing pips.");
        // AI review often receives smart apostrophes even when the catalog
        // stores the Scryfall canonical name with an ASCII apostrophe.
        var aiMatches = CollectionCatalogReview.FindExactNames(catalogPath,
            new[] { "Sphinx’s Tutelage", "Sphinx's Tutelage",
                "Emry, Lurker of the Loch", "Sphinx’s Missing Card" });
        Check(aiMatches.Count == 2 &&
              aiMatches.Contains("Sphinx's Tutelage") &&
              aiMatches.Contains("Emry, Lurker of the Loch") &&
              !aiMatches.Any(name => name.Contains("Missing")),
            "AI card-name verification must accept typographic apostrophes " +
            "while rejecting unverified names and duplicates.");
        // Not all AI card names are bold or in swap tables. Verify names
        // inside ordinary sentences, including an unowned recommended card.
        var shortened = CollectionCatalogReview.FindShortNameLinks(catalogPath,
            "Tezzeret and Dawnsire work well, but Tezzeret's abilities differ.",
            new[] { "Tezzeret, Master of the Bridge", "Dawnsire, Sunstar Dreadnought" });
        Check(shortened.TryGetValue("Tezzeret", out var selectedTezzeret) &&
              selectedTezzeret == "Tezzeret, Master of the Bridge" &&
              shortened.TryGetValue("Dawnsire", out var selectedDawnsire) &&
              selectedDawnsire == "Dawnsire, Sunstar Dreadnought",
            "AI short names must resolve to the single matching deck card.");
        var globallyAmbiguous = CollectionCatalogReview.FindShortNameLinks(catalogPath,
            "Tezzeret and Dawnsire are both options.", Array.Empty<string>());
        Check(!globallyAmbiguous.ContainsKey("Tezzeret") &&
              globallyAmbiguous.TryGetValue("Dawnsire", out var globalDawnsire) &&
              globalDawnsire == "Dawnsire, Sunstar Dreadnought",
            "Ambiguous nicknames without deck context must not guess a card.");
        var twoDeckMatches = CollectionCatalogReview.FindShortNameLinks(catalogPath,
            "Tezzeret is powerful.",
            new[] { "Tezzeret, Master of the Bridge", "Tezzeret, Agent of Bolas" });
        Check(!twoDeckMatches.ContainsKey("Tezzeret"),
            "Two matching cards in a deck cannot silently resolve to one.");
        var partialAlias = CollectionCatalogReview.FindShortNameLinks(catalogPath,
            "Tezzerets and Dawnsirely are not standalone references.",
            new[] { "Tezzeret, Master of the Bridge", "Dawnsire, Sunstar Dreadnought" });
        Check(partialAlias.Count == 0, "Only standalone short references are linkable.");
        var proseMatches = CollectionCatalogReview.FindMentionedNames(catalogPath,
            "Esper Sentinel, Thoughtcast, Thought Monitor, and The One Ring give you more draw. " +
            "The card Sphinx’s Tutelage is also worth verifying. " +
            "This is not a link to Missing Card.", CancellationToken.None);
        Check(proseMatches.Contains("The One Ring") &&
              proseMatches.Contains("Thoughtcast") &&
              proseMatches.Contains("Sphinx's Tutelage") &&
              !proseMatches.Contains("Arena Spell") &&
              !proseMatches.Contains("Missing Card"),
            "AI review should detect catalog cards anywhere in prose, including typographic punctuation.");
        var exactBoundary = CollectionCatalogReview.FindMentionedNames(catalogPath,
            "Thoughtcasting is a word; The One Ringmaster is not a card.");
        Check(!exactBoundary.Contains("Thoughtcast") && !exactBoundary.Contains("The One Ring"),
            "Partial words must not be linked as MTG cards.");
        var reverseMatches = CollectionCatalogReview.FindExactNames(catalogPath,
            new[] { "Sphinx‘s Tutelage" });
        Check(reverseMatches.Count == 1 &&
              reverseMatches[0] == "Sphinx's Tutelage",
            "Opening single quotation mark must resolve to canonical name.");
        // Newer catalog snapshots include exact official mana_cost tokens.
        // The old schema above must remain readable without that column.
        string manaCatalog = Path.Combine(work, "review-mana-cost-catalog.sqlite");
        using (var db = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = manaCatalog, Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString()))
        {
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE cards(name TEXT PRIMARY KEY, type TEXT NOT NULL,
                    oracle TEXT NOT NULL, rarity TEXT NOT NULL, mv REAL NOT NULL,
                    colors TEXT NOT NULL, legalities TEXT NOT NULL,
                    setcode TEXT NOT NULL, mana_cost TEXT NOT NULL);
                INSERT INTO cards VALUES('Precise Mana Test','Sorcery',
                    'Pay {W/U} and {2/R}.','uncommon',3,'WU',
                    '{"commander":"legal"}','TST','{2}{U}');
                """;
            cmd.ExecuteNonQuery();
        }
        var preciseCost = CollectionCatalogReview.ReadDetails(manaCatalog,
            "Precise Mana Test");
        Check(preciseCost is not null &&
              preciseCost.ManaCost == "{2}{U}" &&
              preciseCost.ColorIdentity == "WU",
              "Card details must keep numeric generic mana cost separate from color identity.");

        Check(CollectionCatalogReview.ReadDetails(catalogPath, "' OR 1=1 --") is null,
            "Card metadata lookup must parameterize input.");
        Check(CollectionCatalogReview.ReadDetails(catalogPath, "Unidentified") is null,
            "Do not hallucinate metadata for a missing card.");

        Check(beforeBackup.SequenceEqual(SHA256.HashData(File.ReadAllBytes(backupPath))) &&
              beforeCatalog.SequenceEqual(SHA256.HashData(File.ReadAllBytes(catalogPath))) &&
              sourceMtime == File.GetLastWriteTimeUtc(backupPath) &&
              catalogMtime == File.GetLastWriteTimeUtc(catalogPath),
            "Review modified the original backup or catalog.");
        Console.WriteLine("PASS: native Windows verified collection review conserves 21 physical copies, confirms 9, leaves 12 unresolved, 2 sets, name/set/number guard, offline Card Details, source unchanged");
    }
}
