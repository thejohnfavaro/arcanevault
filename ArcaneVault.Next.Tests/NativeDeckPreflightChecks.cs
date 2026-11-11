using ArcaneVault.Next.Collection;
using ArcaneVault.Next.Domain;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;

internal static class NativeDeckPreflightChecks
{
    private static void Must(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Native deck preflight: " + message);
    }

    private static bool Contains(NativeDeckPreflight.Report report, string fragment,
        NativeDeckPreflight.Level? level = null) =>
        report.Findings.Any(f =>
            (!level.HasValue || f.Severity == level) &&
            f.Message.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    public static void Run(string work)
    {
        string catalog = Path.Combine(work, "native-deck-preflight-catalog.sqlite");
        using (var db = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = catalog, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false }.ToString()))
        {
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE cards (
                    name TEXT PRIMARY KEY COLLATE NOCASE,
                    type TEXT NOT NULL, oracle TEXT NOT NULL, colors TEXT NOT NULL,
                    legalities TEXT NOT NULL, arena INTEGER NOT NULL
                );
                """;
            cmd.ExecuteNonQuery();
            using var add = db.CreateCommand();
            add.CommandText = """
                INSERT INTO cards (name,type,oracle,colors,legalities,arena)
                VALUES ($name,$type,$oracle,$colors,$legal,$arena)
                """;
            foreach (string n in new[] { "$name","$type","$oracle","$colors","$legal","$arena" })
                add.Parameters.AddWithValue(n, "");
            void Card(string name, string type, string colors, string oracle = "",
                string legality = "legal", long arena = 1)
            {
                add.Parameters["$name"].Value = name;
                add.Parameters["$type"].Value = type;
                add.Parameters["$oracle"].Value = oracle;
                add.Parameters["$colors"].Value = colors;
                add.Parameters["$legal"].Value = "{\"commander\":\"" + legality + "\"}";
                add.Parameters["$arena"].Value = arena;
                add.ExecuteNonQuery();
            }
            Card("Test Leader", "Legendary Creature — Human", "BG");
            Card("Black Spell", "Instant", "B", arena: 1);
            Card("White Spell", "Sorcery", "W", arena: 1);
            Card("Banned Spell", "Instant", "B", legality: "banned", arena: 0);
            Card("Relentless Rats", "Creature — Rat", "B",
                "A deck can have any number of cards named Relentless Rats.");
            Card("Seven Dwarves", "Creature — Dwarf", "R",
                "A deck can have up to seven cards named Seven Dwarves.");
            Card("Forest", "Basic Land — Forest", "G");
            Card("Swamp", "Basic Land — Swamp", "B");
        }

        byte[] catalogBefore = SHA256.HashData(File.ReadAllBytes(catalog));
        var backup = new NativeBackupView(
            new NativePhysicalRow[] {
                new("Test Leader", 1, "", "", "", ""),
                new("Black Spell", 1, "", "", "", ""),
                new("Forest", 100, "", "", "", ""),
                new("Relentless Rats", 10, "", "", "", "")
            },
            new NativeArenaRow[] {
                new("Test Leader", 1), new("Black Spell", 1),
                new("Forest", 100)
            },
            0, 0, 0, 112, 102, 0, 112);

        var edh = NativeDeckPreflight.Check(backup,
            NativeDeckEditing.ParseMainboard(
                "1 Black Spell\n1 White Spell\n2 Relentless Rats\n95 Forest"),
            "Test Leader", "Commander", catalog);
        Must(edh.CatalogChecked && edh.OwnershipPool == "Physical",
            "Verified catalog / physical inventory not used.");
        Must(!Contains(edh, "expected 100"), "99 mainboard + leader should total 100.");
        Must(Contains(edh, "White Spell: color identity", NativeDeckPreflight.Level.Warning),
            "Off-identity white spell was not flagged.");
        Must(Contains(edh, "Physical inventory: White Spell", NativeDeckPreflight.Level.Warning),
            "Missing physical copies not reported.");
        Must(!Contains(edh, "Relentless Rats: 2 copies exceed"),
            "Card-specific unlimited copy exception was ignored.");
        Must(!Contains(edh, "Forest: 95 copies exceed"),
            "Basic land exception was ignored.");
        Must(!Contains(edh, "Black Spell: color identity"),
            "Legal in-identity spell wrongly flagged.");

        var banned = NativeDeckPreflight.Check(backup,
            NativeDeckEditing.ParseMainboard("1 Banned Spell\n98 Forest"),
            "Test Leader", "Commander", catalog);
        Must(Contains(banned, "banned", NativeDeckPreflight.Level.Warning),
            "Offline Commander banned status not reported.");

        var brawl = NativeDeckPreflight.Check(backup,
            NativeDeckEditing.ParseMainboard("1 Banned Spell\n98 Forest"),
            "Test Leader", "Arena Brawl", catalog);
        Must(brawl.CatalogChecked && brawl.OwnershipPool == "Arena",
            "Brawl must use Arena inventory only.");
        Must(Contains(brawl, "no Arena printing", NativeDeckPreflight.Level.Warning),
            "Missing Arena printing not flagged.");
        Must(Contains(brawl, "live ban list", NativeDeckPreflight.Level.Review),
            "Brawl queue legality must remain explicitly unverified.");
        Must(!Contains(brawl, "cached Scryfall Commander legality"),
            "Brawl must never substitute tabletop Commander ban results.");

        var constructed = NativeDeckPreflight.Check(backup,
            NativeDeckEditing.ParseMainboard("5 Black Spell\n6 Seven Dwarves\n50 Forest"),
            "", "60-card", catalog);
        Must(Contains(constructed, "Black Spell: 5 copies exceed", NativeDeckPreflight.Level.Warning),
            "Nonbasic constructed four-of limit not warned.");
        Must(!Contains(constructed, "Seven Dwarves: 6 copies exceed"),
            "Card-specific seven-copy exception was ignored.");
        Must(!Contains(constructed, "expected at least 60"),
            "61-card constructed deck incorrectly flagged.");

        var unknown = NativeDeckPreflight.Check(backup,
            NativeDeckEditing.ParseMainboard("1 Entirely Unknown Card"), "",
            "Commander", null);
        Must(!unknown.CatalogChecked && Contains(unknown, "No readable offline card catalog",
            NativeDeckPreflight.Level.Review),
            "Missing catalog should be review-only, never legal confirmation.");

        var injection = NativeDeckPreflight.Check(backup,
            NativeDeckEditing.ParseMainboard("1 ' OR 1=1 --"), "Test Leader",
            "Commander", catalog);
        Must(Contains(injection, "Card not found", NativeDeckPreflight.Level.Review),
            "Catalog name query must be parameterized, not match injected text.");
        Must(SHA256.HashData(File.ReadAllBytes(catalog)).SequenceEqual(catalogBefore),
            "Preflight must never mutate the local catalog.");
        Must(backup.PhysicalCopies == 112 && backup.ArenaCopies == 102,
            "Review may not alter inventories.");
    }
}
