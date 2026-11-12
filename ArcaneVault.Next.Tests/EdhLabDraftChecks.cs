using ArcaneVault.Next.Domain;
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
            Card("Selected Leader", "WU", type: "Legendary Creature — Human");
            Card("Sol Ring", "", "Add {C}.", type: "Artifact");
            Card("Island", "U", "Add {U}.", type: "Basic Land — Island");
            Card("Brainstorm", "U", "Draw three cards.");
            Card("Counterspell", "U", "Counter target spell.", brawlLegal: "banned");
            Card("Swords to Plowshares", "W", "Exile target creature.", arena: false);
            Card("Arcane Signet", "", "Add one mana of any color.", type: "Artifact");
            Card("Board Clear", "W", "Destroy all creatures.", type: "Sorcery");
            Card("Mystic Tutor", "U", "Search your library for a card.");
            Card("Lightning Bolt", "R", "Deal 3 damage.");
            Card("Three Color", "WUB", "Draw a card.");
            Card("Banned Card", "", "Add {C}.", commanderLegal: "banned", brawlLegal: "banned");
            Card("Not Legal", "W", commanderLegal: "not_legal", brawlLegal: "not_legal");
            Card("Malformed Identity", "X", "Draw a card.");
            Card("Commander Only", "W", "Create a 1/1 token.", brawlLegal: "not_legal");
            tx.Commit();
        }

        var originalHash = SHA256.HashData(File.ReadAllBytes(catalog));
        var ownership = new NativeBackupView(
            new[]
            {
                new NativePhysicalRow("Sol Ring", 1, "", "", "", ""),
                new NativePhysicalRow("Swords to Plowshares", 2, "", "", "", ""),
                new NativePhysicalRow("Counterspell", 1, "", "", "", ""),
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
                .SequenceEqual(new[] { "Counterspell", "Sol Ring", "Swords to Plowshares" }) &&
             arenaOwned.Select(x => x.Name).OrderBy(x => x)
                .SequenceEqual(new[] { "Brainstorm", "Island" }),
             "Physical and Arena inventory names may not be combined.");
        Must(paper.Search("LIGHTNING", false).Count == 0 &&
             paper.Search("sol ring", false).Single().RecordedCopies == 1 &&
             paper.Search("island", false).Single().RecordedCopies == 0 &&
             arena.Search("island", false).Single().RecordedCopies == 4,
             "Unowned cards must appear as missing; ownership is name-only and format-separated.");
        Must(paper.Search("", false, "Ramp").Any(x => x.Name == "Sol Ring") &&
             paper.Search("", false, "Board Wipe").Any(x => x.Name == "Board Clear") &&
             paper.Search("", false, "Tutor").Any(x => x.Name == "Mystic Tutor") &&
             paper.Search("", false, "Removal").Any(x => x.Name == "Swords to Plowshares"),
             "Role hints should support practical collection-aware suggestions.");
        Must(paper.Search("", false, limit: 1).Count == 1 &&
             paper.Search("", false, limit: 1)[0].RecordedCopies > 0,
             "Bounded suggestions should put recorded eligible cards first.");
        Must(originalHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(catalog))),
             "Deck drafting candidate search must never write or alter the Scryfall catalog.");

        Console.WriteLine("PASS: EDH Lab draft candidate filtering (color identity, paper/Arena legality, roles, missing and recorded holdings)");
    }
}
