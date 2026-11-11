using ArcaneVault.Next.Desktop;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;

internal static class NativeCatalogFilterChecks
{
    private static void Must(bool value, string message)
    {
        if (!value) throw new InvalidOperationException("Native catalog filters: " + message);
    }

    private static void Reject(Action action)
    {
        try { action(); }
        catch (ArgumentException) { return; }
        throw new InvalidOperationException("Invalid catalog filter was not rejected.");
    }

    public static void Run(string work)
    {
        string path = Path.Combine(work, "native-filter-catalog.sqlite");
        using (var db = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = path, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false }.ToString()))
        {
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE cards(
                    name TEXT PRIMARY KEY COLLATE NOCASE,
                    type TEXT NOT NULL,colors TEXT NOT NULL);
                CREATE TABLE variants (
                    print_id TEXT PRIMARY KEY,name TEXT NOT NULL COLLATE NOCASE,
                    setcode TEXT NOT NULL,collector TEXT NOT NULL,
                    rarity TEXT NOT NULL,arena INTEGER NOT NULL);
                INSERT INTO cards VALUES
                    ('Sol Ring','Artifact',''),
                    ('Azorius Guildmage','Creature — Vedalken Wizard','WU'),
                    ('Supreme Verdict','Sorcery','WU'),
                    ('Esper Sentinel','Artifact Creature — Human Soldier','W'),
                    ('Clever Impersonator','Creature — Shapeshifter','U'),
                    ('Boros Garrison','Land','WR'),
                    ('Special%Literal','Instant','W'),
                    ('Special_Literal','Instant','R');
                INSERT INTO variants VALUES
                    ('sol-a','Sol Ring','cmm','396','uncommon',0),
                    ('sol-b','Sol Ring','sld','123','rare',0),
                    ('az-a','Azorius Guildmage','rtr','141','uncommon',1),
                    ('sv-a','Supreme Verdict','rtr','201','rare',1),
                    ('es-a','Esper Sentinel','mh2','12','rare',0),
                    ('ci-a','Clever Impersonator','ktk','34','mythic',0),
                    ('bg-a','Boros Garrison','cmm','392','common',0),
                    ('per-a','Special%Literal','tst','1','common',0),
                    ('under-a','Special_Literal','tst','2','common',0);
                """;
            cmd.ExecuteNonQuery();
        }
        string checksum = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        using var catalog = CatalogSnapshot.Open(path);
        Must(catalog.HasFullMetadata && catalog.TotalPrintings == 9,
            "Full metadata catalog should expose combined filters.");

        var empty = new CatalogSnapshot.SearchOptions();
        Must(catalog.SearchFiltered("Sol", empty).Rows.Count == 2 &&
             catalog.SearchPrefix("Sol").Rows.Count == 2,
            "Default filtered query must preserve original indexed prefix search.");
        Must(catalog.SearchFiltered("", new(SetCode: "CMM")).Rows.Count == 2,
            "Set code search should be case-insensitive.");
        Must(catalog.SearchFiltered("", new(Rarity: "rare")).Rows.Count == 3,
            "Rarity must come from each printing rather than card-name metadata.");
        Must(catalog.SearchFiltered("", new(TypeContains: "Creature")).Rows.Count == 3,
            "Type filter must join authoritative card metadata.");
        Must(catalog.SearchFiltered("", new(TypeContains: "Wizard")).Rows.Count == 1,
            "Subtype filter must be able to match an individual creature subtype.");
        Must(catalog.SearchFiltered("", new(ColorlessOnly: true)).Rows.Count == 2,
            "Colorless identity must ignore the land's mana production.");
        Must(catalog.SearchFiltered("", new(ColorMask: "WU",
            ColorMatch: CatalogSnapshot.ColorMatching.Exact)).Rows.Count == 2,
            "Exact WU must match only Azorius card identity.");
        Must(catalog.SearchFiltered("", new(ColorMask: "W",
            ColorMatch: CatalogSnapshot.ColorMatching.ContainsAll)).Rows.Count == 6,
            "Contains-all W must find cards with White in their identity.");
        Must(catalog.SearchFiltered("", new(ColorMask: "WU",
            ColorMatch: CatalogSnapshot.ColorMatching.ContainsAny)).Rows.Count == 7,
            "Contains-any W/U must find cards with either selected color.");
        Must(catalog.SearchFiltered("", new(ArenaOnly: true)).Rows.Count == 2,
            "Arena filter must use actual per-printing availability.");
        Must(catalog.SearchFiltered("", new(SetCode: "RTR", ArenaOnly: true,
            ColorMask: "WU",
            ColorMatch: CatalogSnapshot.ColorMatching.Exact)).Rows.Count == 2,
            "Combined filters must work with parameterized SQL.");
        Must(catalog.SearchFiltered("Special%", empty).Rows.Single().Name == "Special%Literal" &&
             catalog.SearchFiltered("Special_", empty).Rows.Single().Name == "Special_Literal",
            "Literal percent and underscore must not become SQL wildcards.");
        Must(catalog.SearchFiltered("", new(TypeContains: "%")).Rows.Count == 0,
            "User-supplied type wildcards must be treated as literal text.");
        Must(catalog.SearchFiltered("", new(Rarity: "rare"), limit: 1).MayHaveMore,
            "Filtered pagination must report additional rows beyond the cap.");
        Reject(() => catalog.SearchFiltered("", new(ColorMask: "WW")));
        Reject(() => catalog.SearchFiltered("", new(ColorMask: "C")));
        Reject(() => catalog.SearchFiltered("", new(ColorMask: "W", ColorlessOnly: true)));
        Reject(() => catalog.SearchFiltered("", new(Rarity: "invalidrarity")));
        Must(checksum == Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))),
            "Catalog metadata filters may never change original catalog bytes.");

        string legacy = Path.Combine(work, "native-filter-legacy.sqlite");
        using (var db = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = legacy, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false }.ToString()))
        {
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE variants(
                    print_id TEXT PRIMARY KEY,name TEXT,setcode TEXT,collector TEXT);
                INSERT INTO variants VALUES('legacy','Old Card','old','1');
                """;
            cmd.ExecuteNonQuery();
        }
        using var oldCatalog = CatalogSnapshot.Open(legacy);
        Must(!oldCatalog.HasFullMetadata &&
             oldCatalog.SearchPrefix("Old").Rows.Count == 1 &&
             oldCatalog.SearchFiltered("", new(SetCode: "old")).Rows.Count == 1,
            "Legacy printing-only snapshot must preserve simple browsing and set search.");
        try
        {
            oldCatalog.SearchFiltered("", new(TypeContains: "Creature"));
            throw new InvalidOperationException("Old catalog accidentally accepted unavailable metadata.");
        }
        catch (InvalidDataException) { }
        try
        {
            oldCatalog.SearchFiltered("", new(Rarity: "rare"));
            throw new InvalidOperationException("Old catalog accidentally read an absent rarity column.");
        }
        catch (InvalidDataException) { }
    }
}
