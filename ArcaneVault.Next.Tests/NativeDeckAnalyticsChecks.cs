using ArcaneVault.Next.Collection;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;

internal static class NativeDeckAnalyticsChecks
{
    private static void Must(bool value, string message)
    {
        if (!value) throw new InvalidOperationException("Native deck analytics: " + message);
    }

    public static void Run(string work)
    {
        string path = Path.Combine(work, "deck-analytics-catalog.sqlite");
        using (var db = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false
        }.ToString()))
        {
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE cards(name TEXT PRIMARY KEY COLLATE NOCASE,
                    type TEXT NOT NULL, mv REAL NOT NULL, colors TEXT NOT NULL);
                INSERT INTO cards VALUES ('Forest','Basic Land — Forest',0,'G');
                INSERT INTO cards VALUES ('Sol Ring','Artifact',1,'');
                INSERT INTO cards VALUES ('Hybrid Test','Artifact Creature — Construct',3,'WU');
                INSERT INTO cards VALUES ('Lightning Bolt','Instant',1,'R');
                INSERT INTO cards VALUES ('Leader','Legendary Creature — Human',4,'W');
                """;
            cmd.ExecuteNonQuery();
        }
        byte[] originalHash = SHA256.HashData(File.ReadAllBytes(path));
        var entries = NativeDeckEditing.ParseMainboard(
            "10 Forest\n2 Sol Ring\n1 Hybrid Test\n3 Lightning Bolt\n2 Missing Spell");
        var result = NativeDeckAnalytics.Analyze(entries, "Leader", "Commander", path);
        Must(result.TotalCards == 19 && result.KnownCards == 17 &&
             result.UnknownCards == 2, "Incomplete catalog coverage not reported.");
        Must(result.Lands == 10 && result.Artifacts == 2 &&
             result.Creatures == 2 && result.Instants == 3,
             "Primary card types must be exclusive and include commander.");
        Must(result.Curve[1] == 5 && result.Curve[3] == 1 &&
             result.Curve[4] == 1 && result.Curve.Sum() == 7,
             "Mana curve must exclude lands and unknown metadata.");
        Must(result.Identity["G"] == 10 && result.Identity["R"] == 3 &&
             result.Identity["Multicolor"] == 1 && result.Identity["Colorless"] == 2 &&
             result.Identity["W"] == 1,
             "Color identity distribution must count copies and multicolor cards once.");
        var standard = NativeDeckAnalytics.Analyze(entries, "Leader", "60-card", path);
        Must(standard.TotalCards == 18 && standard.Creatures == 1,
             "Non-commander deck must not include optional leader.");
        Must(originalHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))),
             "Analytics unexpectedly wrote to the offline catalog.");
        bool rejected = false;
        try { NativeDeckAnalytics.Analyze(entries, "", "60-card", null); }
        catch (InvalidDataException) { rejected = true; }
        Must(rejected, "Missing offline catalog should be reported, not fabricated.");
        Console.WriteLine("PASS: offline native deck analytics (curve, colors, types, unknowns)");
    }
}
