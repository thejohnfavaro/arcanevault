using ArcaneVault.Next.Domain;
using ArcaneVault.Next.EDHLab;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;

internal static class EdhLabChecks
{
    private static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException("EDH Lab: " + message);
    }

    private static void Reject(Action work)
    {
        try { work(); }
        catch (ArgumentException) { return; }
        throw new InvalidOperationException("EDH Lab accepted an invalid filter.");
    }

    public static void Run(string work)
    {
        string catalog = Path.Combine(work, "edh-lab-catalog.sqlite");
        using (var db = new SqliteConnection(new SqliteConnectionStringBuilder
            { DataSource = catalog, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false }.ToString()))
        {
            db.Open();
            using (var command = db.CreateCommand())
            {
                command.CommandText = """
                    CREATE TABLE cards(
                        name TEXT PRIMARY KEY COLLATE NOCASE,
                        type TEXT NOT NULL,
                        oracle TEXT NOT NULL,
                        colors TEXT NOT NULL,
                        legalities TEXT NOT NULL,
                        arena INTEGER NOT NULL,
                        image TEXT NOT NULL
                    );
                    CREATE TABLE variants(print_id TEXT PRIMARY KEY, name TEXT);
                    """;
                command.ExecuteNonQuery();
            }
            using var tx = db.BeginTransaction();
            using var add = db.CreateCommand();
            add.Transaction = tx;
            add.CommandText = """
                INSERT INTO cards(name,type,oracle,colors,legalities,arena,image)
                VALUES($name,$type,$oracle,$colors,$legal,$arena,$image)
                """;
            foreach (var p in new[] { "$name","$type","$oracle","$colors","$legal","$arena","$image" })
                add.Parameters.Add(new SqliteParameter(p, ""));
            void Insert(string name, string type = "Legendary Creature — Human",
                string oracle = "Test ability", string colors = "W",
                string legal = "{\"commander\":\"legal\",\"brawl\":\"legal\"}", bool arena = true)
            {
                add.Parameters["$name"].Value = name;
                add.Parameters["$type"].Value = type;
                add.Parameters["$oracle"].Value = oracle;
                add.Parameters["$colors"].Value = colors;
                add.Parameters["$legal"].Value = legal;
                add.Parameters["$arena"].Value = arena ? 1 : 0;
                add.Parameters["$image"].Value = "";
                add.ExecuteNonQuery();
            }

            var colors = new[] { "W", "U", "B", "R", "G", "WU", "" };
            for (var i = 0; i < 260; i++)
                Insert($"Test Leader {i:D3}", colors: colors[i % colors.Length], arena: i % 2 == 0);
            Insert("Commander Banned", legal: "{\"commander\":\"banned\",\"brawl\":\"legal\"}");
            Insert("Brawl Banned", legal: "{\"commander\":\"legal\",\"brawl\":\"banned\"}");
            Insert("Only Paper", arena: false);
            Insert("PW No Exception", "Legendary Planeswalker — Test");
            Insert("PW Exception", "Legendary Planeswalker — Test",
                "This planeswalker may be your commander.");
            Insert("Background Alone", "Legendary Enchantment — Background");
            Insert("Vehicle Missing Stats", "Legendary Artifact — Vehicle");
            Insert("Illegal Legend", legal: "{}");
            Insert("Oracle Exception", "Planeswalker — Test", "This card can be your commander");
            Insert("Legacy Brawl", legal: "{\"commander\":\"legal\",\"historicbrawl\":\"legal\"}");
            Insert("Invalid Color", colors: "X");
            tx.Commit();
        }

        var before = SHA256.HashData(File.ReadAllBytes(catalog));
        var mtime = File.GetLastWriteTimeUtc(catalog);
        var index = CommanderCatalog.OpenReadOnly(catalog);
        var paper = new EdhLabQuery(EdhLabFormat.PaperCommander, PageSize: 50);
        var arena = paper with { Format = EdhLabFormat.ArenaBrawl };
        var paperPage = index.Browse(paper);
        Check(paperPage.Total == 265 && paperPage.Cards.Count == 50,
            "Paper Commander should browse every legal candidate beyond 200 results.");
        Check(index.Browse(paper with { Page = 5 }).Cards.Count == 15,
            "Sixth page must include all remaining commanders.");
        Check(index.Browse(paper with { Page = 999_999 }).Cards.Count == 0,
            "Out-of-range pages must be empty, not crash.");
        Check(index.Browse(arena).Total == 135,
            "Arena Brawl must enforce the Arena availability and brawl legality filters.");

        Check(index.Filter(paper).Any(c => c.Name == "PW Exception") &&
              !index.Filter(paper).Any(c => c.Name == "PW No Exception") &&
              index.Filter(arena).Any(c => c.Name == "PW No Exception"),
            "Legendary planeswalkers require a paper exception but are eligible for Brawl.");
        Check(!index.Filter(paper).Any(c => c.Name == "Background Alone") &&
              !index.Filter(arena).Any(c => c.Name == "Vehicle Missing Stats"),
            "Backgrounds cannot lead alone; specialized commanders without required metadata stay unresolved.");
        Check(!index.Filter(arena).Any(c => c.Name == "Brawl Banned") &&
              !index.Filter(paper).Any(c => c.Name == "Commander Banned") &&
              index.Filter(arena).Any(c => c.Name == "Commander Banned"),
            "Format ban lists must stay independent.");
        Check(index.Filter(arena).Any(c => c.Name == "Legacy Brawl") &&
              !index.Filter(arena).Any(c => c.Name == "Only Paper"),
            "Legacy Brawl key fallback and Arena availability must be respected.");
        Check(!index.Filter(paper).Any(c => c.Name == "Invalid Color"),
            "Malformed color identity must not be treated as colorless.");

        var monoWhite = index.Filter(paper with { Colors = "W", Match = EdhLabColorMatch.Exact });
        var includesWhite = index.Filter(paper with { Colors = "W", Match = EdhLabColorMatch.ContainsAll });
        var identityBoros = index.Filter(paper with { Colors = "WR", Match = EdhLabColorMatch.Exact });
        var colorless = index.Filter(paper with { Colors = "C" });
        Check(monoWhite.Count > 0 &&
              monoWhite.All(c => EdhLabCommanderRules.ColorBits(c.ColorIdentity) == 1),
            "Exact white identity filter returned a multicolor commander.");
        Check(includesWhite.Count > monoWhite.Count,
            "Contains-all white must also find multicolor commanders.");
        Check(identityBoros.Count == 0 && colorless.Count > 0 &&
              colorless.All(c => c.ColorIdentity == ""),
            "Colorless and unavailable two-color combinations must behave exactly.");
        Check(index.Filter(paper with { Colors = "WU", Match = EdhLabColorMatch.Exact })
              .All(c => EdhLabCommanderRules.CanonicalColors(c.ColorIdentity) == "WU"),
            "Two-color exact identity result invalid.");
        Check(index.Filter(paper with { Colors = "W,U", Match = EdhLabColorMatch.ContainsAny })
              .Count > includesWhite.Count,
            "Contains-any mode must include blue-only candidates.");
        Reject(() => index.Browse(paper with { Colors = "CX" }));
        Reject(() => index.Browse(paper with { Colors = "C,W" }));

        var picker = new EdhLabRandomPicker();
        var small = paper with { Search = "Test Leader 00" };
        var rolled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < 10; i++)
        {
            var selected = picker.Roll(index, small, avoidRepeats: true, chooseIndex: _ => 0);
            Check(selected is not null && rolled.Add(selected.Name),
                "No-repeat random picker repeated a commander before exhausting pool.");
        }
        Check(picker.Roll(index, small, avoidRepeats: true, chooseIndex: _ => 0)?.Name ==
              "Test Leader 000", "Random picker must reset only after exhausting the pool.");
        Check(picker.Roll(index, paper with { Colors = "WUBRG", Match = EdhLabColorMatch.Exact }) is null,
            "Random picker must return no selection for an empty result.");
        var anyBlue = picker.Roll(index, paper with { Colors = "U", Match = EdhLabColorMatch.Exact },
            avoidRepeats: true, chooseIndex: _ => 0);
        Check(anyBlue is not null && anyBlue.ColorIdentity == "U",
            "Changing filters must reset eligible random pool.");
        Check(before.SequenceEqual(SHA256.HashData(File.ReadAllBytes(catalog))) &&
            File.GetLastWriteTimeUtc(catalog) == mtime,
            "EDH Lab must not modify catalog source data.");

        Console.WriteLine("PASS: EDH Lab 265 paper + 135 Arena Brawl synthetic commanders, paging >200, color identity, legality, random/no-repeat, DB unchanged");
    }
}
