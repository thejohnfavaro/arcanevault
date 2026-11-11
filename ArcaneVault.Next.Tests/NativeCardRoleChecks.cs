using ArcaneVault.Next.Desktop;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;

internal static class NativeCardRoleChecks
{
    private static void Must(bool ok, string why)
    {
        if (!ok) throw new InvalidOperationException("Native card art/roles: " + why);
    }

    public static void Run(string work)
    {
        Must(NativeCardRoles.Names.Count == 18 &&
             NativeCardRoles.Valid("Board Wipe") &&
             !NativeCardRoles.Valid("Totally Made Up"),
            "Role names should mirror the original WebView categories.");
        var tags = NativeCardRoles.Derive("Artifact Creature — Human Artificer",
            "When this creature enters, create a Treasure token. Draw a card.");
        Must(tags.Contains("Ramp") && tags.Contains("Card Draw") &&
             tags.Contains("Tokens"),
            "One card may match multiple utilization tags.");
        Must(NativeCardRoles.Derive("Artifact — Equipment", "").Contains("Equipment") &&
             NativeCardRoles.Derive("Sorcery", "Destroy all creatures.")
                .Contains("Board Wipe") &&
             NativeCardRoles.Derive("Instant", "Counter target spell.")
                .Contains("Counterspell") &&
             NativeCardRoles.Derive("Sorcery", "Search your library for a creature card, reveal it.")
                .Contains("Tutor") &&
             !NativeCardRoles.Derive("Sorcery",
                "Search your library for a basic land card.").Contains("Tutor"),
            "Distinct roles need reliable offline type/Oracle heuristics.");
        Must(NativeCardRoles.Derive(null, null).Count == 0,
            "Missing metadata must not invent role tags.");

        string path = Path.Combine(work,"role-image-catalog.sqlite");
        using (var db = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource=path,Mode=SqliteOpenMode.ReadWriteCreate,Pooling=false }.ToString()))
        {
            db.Open();
            using var command = db.CreateCommand();
            command.CommandText = """
                CREATE TABLE cards(
                    name TEXT PRIMARY KEY COLLATE NOCASE, type TEXT, oracle TEXT,
                    colors TEXT,rarity TEXT);
                CREATE TABLE variants(
                    print_id TEXT PRIMARY KEY,name TEXT NOT NULL COLLATE NOCASE,
                    setcode TEXT,collector TEXT,rarity TEXT,arena INTEGER,
                    image TEXT,games TEXT,digital INTEGER);
                INSERT INTO cards VALUES
                    ('Board Smash','Sorcery','Destroy all creatures.','','rare'),
                    ('Countering','Instant','Counter target spell.','U','uncommon'),
                    ('Sol Ring','Artifact','{T}: Add {C}{C}.','','uncommon'),
                    ('Treasure Maker','Artifact Creature',
                     'Create a Treasure token. Draw a card.','R','rare'),
                    ('Grave Raiser','Creature',
                     'Return target creature card from your graveyard to your hand.','B','rare');
                INSERT INTO variants VALUES
                    ('00000000-0000-4000-8000-000000000a01','Board Smash','tst','1',
                     'rare',1,'https://cards.scryfall.io/normal/front/a/a/board.jpg','paper',0),
                    ('00000000-0000-4000-8000-000000000a02','Countering','tst','2',
                     'uncommon',1,'https://cards.scryfall.io/normal/front/a/a/counter.jpg','paper',0),
                    ('00000000-0000-4000-8000-000000000a03','Sol Ring','tst','3',
                     'uncommon',0,'https://cards.scryfall.io/normal/front/a/a/sol.jpg','paper',0),
                    ('00000000-0000-4000-8000-000000000a04','Treasure Maker','tst','4',
                     'rare',0,'https://cards.scryfall.io/normal/front/a/a/treasure.jpg','paper',0),
                    ('00000000-0000-4000-8000-000000000a05','Grave Raiser','tst','5',
                     'rare',0,'','paper',0);
                """;
            command.ExecuteNonQuery();
        }
        string checksum=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        using var catalog = CatalogSnapshot.Open(path);
        Must(catalog.HasRoleMetadata && catalog.HasPrintingArtwork,
            "New Scryfall catalog must expose offline role and printing-art fields.");
        var normal=catalog.SearchPrefix("Sol").Rows.Single();
        Must(normal.RolesLabel.Contains("Ramp") &&
             normal.ImageUrl.EndsWith("sol.jpg"),
            "Original indexed prefix path must display role tags and exact printing image.");
        var all=catalog.SearchFiltered("",new CatalogSnapshot.SearchOptions());
        Must(all.Rows.Count==5 && all.Rows.Any(x=>x.RolesLabel.Contains("Board Wipe")),
            "Default catalog read should add computed roles without re-importing.");
        var draw=catalog.SearchFiltered("",new(Role:"Card Draw"));
        Must(draw.Rows.Count==1 && draw.Rows[0].Name=="Treasure Maker" &&
             draw.Rows[0].RolesLabel.Contains("Tokens"),
            "Combined metadata filtering must match exact utilization category.");
        var removal=catalog.SearchFiltered("",new(Role:"Board Wipe"));
        Must(removal.Rows.Count==1 && removal.Rows[0].Name=="Board Smash",
            "Board wipe must be distinct from ordinary removal.");
        var combined=catalog.SearchFiltered("",new(Role:"Counterspell",
            SetCode:"TST",ArenaOnly:true,ColorMask:"U",
            ColorMatch:CatalogSnapshot.ColorMatching.Exact));
        Must(combined.Rows.Count==1 && combined.Rows[0].Name=="Countering",
            "Role tags must compose with indexed color, set and Arena filters.");
        Must(catalog.SearchFiltered("",new(Role:"Ramp"),limit:1).Rows.Count==1 &&
             !catalog.SearchFiltered("",new(Role:"Ramp"),limit:1).MayHaveMore,
            "Role filter limit should count matching roles, not earlier mismatched rows.");
        try
        {
            catalog.SearchFiltered("",new(Role:"Not A Role"));
            throw new InvalidOperationException("Invalid role accepted.");
        }
        catch (ArgumentException) { }

        Must(checksum==Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))),
            "Image/role search must never modify offline Scryfall catalog.");

        string legacy=Path.Combine(work,"role-legacy.sqlite");
        using(var db=new SqliteConnection(new SqliteConnectionStringBuilder
        {DataSource=legacy,Mode=SqliteOpenMode.ReadWriteCreate,Pooling=false}.ToString()))
        {
            db.Open();
            using var cmd=db.CreateCommand();
            cmd.CommandText="""
                CREATE TABLE variants(
                    print_id TEXT PRIMARY KEY,name TEXT,setcode TEXT,collector TEXT);
                INSERT INTO variants VALUES('legacy-card','Old Card','old','1');
                """;
            cmd.ExecuteNonQuery();
        }
        using var older=CatalogSnapshot.Open(legacy);
        Must(!older.HasRoleMetadata && !older.HasPrintingArtwork &&
             older.SearchPrefix("Old").Rows.Single().ImageUrl=="" &&
             older.SearchPrefix("Old").Rows.Single().RolesLabel=="",
            "Older catalog must browse without inventing artwork or utilization.");
        try
        {
            older.SearchFiltered("",new(Role:"Ramp"));
            throw new InvalidOperationException("Legacy catalog accepted unavailable tags.");
        }
        catch (InvalidDataException) { }
    }
}
