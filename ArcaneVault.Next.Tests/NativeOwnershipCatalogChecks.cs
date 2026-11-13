using ArcaneVault.Next.Desktop;
using ArcaneVault.Next.Domain;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text.Json;

internal static class NativeOwnershipCatalogChecks
{
    private const string First = "00000000-0000-4000-8000-000000000101";
    private const string Second = "00000000-0000-4000-8000-000000000102";
    private const string Third = "00000000-0000-4000-8000-000000000103";
    private const string Fourth = "00000000-0000-4000-8000-000000000104";
    private const string Fifth = "00000000-0000-4000-8000-000000000105";
    private const string Sixth = "00000000-0000-4000-8000-000000000106";
    private const string Digital = "00000000-0000-4000-8000-000000000108";

    private static void Must(bool ok, string description)
    {
        if (!ok) throw new InvalidOperationException(
            "Native catalog ownership: " + description);
    }

    private static void Reject(Action action)
    {
        try { action(); }
        catch (ArgumentException) { return; }
        throw new InvalidOperationException(
            "Invalid proxy scope was not rejected by the catalog.");
    }

    private static string Hash(string file) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));

    public static void Run(string work)
    {
        byte[] backup = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema = 2,
            cards = new[]
            {
                new { name = "Sol Ring", qty = 3 },
                new { name = "Arena Exclusive", qty = 2 },
                new { name = "Zero Copies", qty = 0 }
            },
            physicalCards = new[]
            {
                new { name = "Sol Ring", qty = 2, set = "cmm", number = "396",
                    finish = "nonfoil", scryfallId = First },
                new { name = "Sol Ring", qty = 1, set = "cmm", number = "396",
                    finish = "foil", scryfallId = First },
                // A physically owned record with no ID is never attributed
                // to either of the Sol Ring editions.
                new { name = "Sol Ring", qty = 7, set = "sld", number = "123",
                    finish = "foil", scryfallId = "" },
                // Even a valid supplied ID is not enough if set metadata conflicts.
                new { name = "Mismatched Card", qty = 1, set = "wrong", number = "1",
                    finish = "nonfoil", scryfallId = Third },
                new { name = "Only Physical", qty = 4, set = "tst", number = "5",
                    finish = "nonfoil", scryfallId = Fourth },
                new { name = "Digital Only", qty = 1, set = "tst", number = "9",
                    finish = "nonfoil", scryfallId = Digital }
            },
            deckLibrary = Array.Empty<object>(),
            preferredArtwork = new Dictionary<string, string>()
        });
        var view = NativeBackupReader.Read(backup);
        var ownership = NativeCatalogOwnership.From(view);
        var proxyOwnership = NativeCatalogOwnership.From(view,
            new ArcaneVault.Next.Collection.NativeProxyEditing.ProxyRow[]
            {
                new("Sol Ring", 2), new("Never Owned", 4),
                new("Digital Only", 1), new("Unknown Third Party Card", 3)
            });
        Must(proxyOwnership.ForPrinting(Second, "Sol Ring", "sld", "123")
                is { VerifiedPhysical: 0, Proxy: 2 } &&
             proxyOwnership.ForPrinting(First, "Sol Ring", "cmm", "396")
                is { VerifiedPhysical: 3, Proxy: 2 } &&
             proxyOwnership.ForPrinting(Sixth, "Never Owned", "tst", "7")
                is { VerifiedPhysical: 0, Proxy: 4 },
             "Proxy name coverage must not change exact verified physical ownership.");


        Must(ownership.ForPrinting(First, "Sol Ring", "cmm", "396").VerifiedPhysical == 3,
            "The same physical printing must merge multiple finishes for owned counts.");
        Must(ownership.ForPrinting(Second, "Sol Ring", "sld", "123").VerifiedPhysical == 0,
            "A different edition with the same card name is NOT physically owned.");
        Must(ownership.ForPrinting(Second, "Sol Ring", "sld", "123").UnresolvedName == 7,
            "Unresolved physical holdings must remain visible by name, not guessed as owned.");
        Must(ownership.ForPrinting(Second, "Sol Ring", "sld", "123").Arena == 3,
            "Arena ownership should count all printings of a known Arena card name.");
        Must(ownership.ForPrinting(Third, "Mismatched Card", "tst", "4").VerifiedPhysical == 0,
            "Conflicting set or collector details must not count as verified paper ownership.");
        Must(ownership.ForPrinting(Fourth, "Only Physical", "tst", "5").VerifiedPhysical == 4,
            "Exact Scryfall printing ID + matching metadata should count as physical ownership.");
        Must(ownership.ForPrinting("", "Sol Ring", "cmm", "396").VerifiedPhysical == 0,
            "A missing catalog ID cannot be treated as a verified printing.");
        Must(ownership.ForPrinting(Fifth, "Zero Copies", "tst", "6").Arena == 0,
            "Zero Arena quantities cannot count as owned.");
        Must(ownership.ForPrinting(First, "Sol Ring", "cmm", "396").Label == "Unresolved name 7",
            "Owned status label should summarize concrete positive quantities.");

        string catalogPath = Path.Combine(work, "catalog-ownership-filter.sqlite");
        using (var db = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = catalogPath, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false
        }.ToString()))
        {
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE cards(
                    name TEXT PRIMARY KEY COLLATE NOCASE,type TEXT NOT NULL,
                    colors TEXT NOT NULL);
                CREATE TABLE variants(
                    print_id TEXT PRIMARY KEY,name TEXT NOT NULL COLLATE NOCASE,
                    setcode TEXT NOT NULL,collector TEXT NOT NULL,
                    rarity TEXT NOT NULL,arena INTEGER NOT NULL,
                    games TEXT NOT NULL,digital INTEGER NOT NULL);
                """;
            cmd.ExecuteNonQuery();
            using var tx = db.BeginTransaction();
            using var insertCard = db.CreateCommand();
            insertCard.Transaction = tx;
            insertCard.CommandText = """
                INSERT INTO cards(name,type,colors) VALUES
                ('Sol Ring','Artifact',''),
                ('Mismatched Card','Artifact',''),
                ('Only Physical','Artifact',''),
                ('Arena Exclusive','Sorcery','R'),
                ('Zero Copies','Sorcery','U'),
                ('Never Owned','Instant','U'),
                ('Digital Only','Artifact','');
                """;
            insertCard.ExecuteNonQuery();
            using var insert = db.CreateCommand();
            insert.Transaction = tx;
            insert.CommandText = """
                INSERT INTO variants(print_id,name,setcode,collector,rarity,arena,games,digital)
                VALUES ($id,$name,$set,$number,$rarity,$arena,$games,$digital)
                """;
            foreach (var (id, name, set, number, rarity, onArena, games, digital) in new[]
            {
                (First, "Sol Ring", "cmm", "396", "uncommon", 0, "paper", 0),
                (Second, "Sol Ring", "sld", "123", "rare", 1, "paper,arena", 0),
                (Third, "Mismatched Card", "tst", "4", "common", 1, "paper", 0),
                (Fourth, "Only Physical", "tst", "5", "common", 0, "paper", 0),
                (Fifth, "Zero Copies", "tst", "6", "common", 1, "paper", 0),
                (Sixth, "Never Owned", "tst", "7", "common", 1, "paper", 0),
                ("00000000-0000-4000-8000-000000000107", "Arena Exclusive",
                    "tst", "8", "rare", 1, "paper", 0),
                (Digital, "Digital Only", "tst", "9", "common", 1, "arena", 1)
            })
            {
                insert.Parameters.Clear();
                insert.Parameters.AddWithValue("$id", id);
                insert.Parameters.AddWithValue("$name", name);
                insert.Parameters.AddWithValue("$set", set);
                insert.Parameters.AddWithValue("$number", number);
                insert.Parameters.AddWithValue("$rarity", rarity);
                insert.Parameters.AddWithValue("$arena", onArena);
                insert.Parameters.AddWithValue("$games", games);
                insert.Parameters.AddWithValue("$digital", digital);
                insert.ExecuteNonQuery();
            }
            tx.Commit();
        }
        string catalogHash = Hash(catalogPath);
        string backupPath = Path.Combine(work, "ownership-source.json");
        File.WriteAllBytes(backupPath, backup);
        string backupHash = Hash(backupPath);

        using var snapshot = CatalogSnapshot.Open(catalogPath);
        var all = snapshot.SearchFiltered("", new(), ownership: ownership);
        Must(all.Rows.Count == 8 &&
             all.Rows.Single(r => r.Name == "Digital Only").PhysicalOwned == 0 &&
             all.Rows.Single(r => r.Name == "Sol Ring" && r.SetCode == "cmm").PhysicalOwned == 3 &&
             all.Rows.Single(r => r.Name == "Sol Ring" && r.SetCode == "sld").PhysicalOwned == 0,
            "Full catalog results should be annotated with true per-printing ownership.");
        var byPrefix = snapshot.SearchPrefix("Sol", ownership: ownership);
        Must(byPrefix.Rows.Count == 2 &&
             byPrefix.Rows.Single(r => r.SetCode == "sld").ArenaOwned == 3 &&
             byPrefix.Rows.Single(r => r.SetCode == "sld").UnresolvedName == 7,
            "Fast name-prefix search must also display ownership without cross-printing credit.");

        var physical = snapshot.SearchFiltered("", new(
            Ownership: CatalogSnapshot.OwnershipMode.PhysicalVerified),
            ownership: ownership);
        Must(physical.Rows.Count == 2 &&
             physical.Rows.Any(r => r.Name == "Only Physical") &&
             physical.Rows.Any(r => r.SetCode == "cmm") &&
             physical.Rows.All(r => r.PhysicalOwned > 0),
            "Physical owned filter must show only individually verified printings.");

        var withProxies = snapshot.SearchFiltered("", new(
            Ownership: CatalogSnapshot.OwnershipMode.PhysicalVerified,
            IncludeProxies: true), ownership: proxyOwnership);
        Must(withProxies.Rows.Count == 4 &&
             withProxies.Rows.Any(x => x.Name == "Never Owned" &&
                 x.ProxyCopies == 4 && x.PhysicalOwned == 0 &&
                 x.OwnershipLabel.Contains("Proxy ×4") &&
                 x.OwnershipLabel.Contains("not owned")) &&
             withProxies.Rows.Count(x => x.Name == "Sol Ring") == 2 &&
             withProxies.Rows.Count(x => x.Name == "Digital Only") == 0 &&
             withProxies.Rows.All(x => x.Name != "Unknown Third Party Card"),
             "Physical + Proxy should show one paper representative for proxy-only names, retain real verified printings, never fabricate digital/uncataloged physical printings.");
        Must(snapshot.SearchFiltered("", new(
            Ownership: CatalogSnapshot.OwnershipMode.PhysicalVerified),
            ownership: proxyOwnership).Rows.Count == 2 &&
             snapshot.SearchFiltered("", new(
                Ownership: CatalogSnapshot.OwnershipMode.PhysicalNotVerified),
                ownership: proxyOwnership).Rows.Any(x => x.Name == "Never Owned"),
             "Disabling the proxy toggle must preserve original exact-printing filters.");
        Reject(() => snapshot.SearchFiltered("", new(
            Ownership: CatalogSnapshot.OwnershipMode.ArenaOwned,
            IncludeProxies: true), ownership: proxyOwnership));

        var unverified = snapshot.SearchFiltered("Sol", new(
            Ownership: CatalogSnapshot.OwnershipMode.PhysicalNotVerified),
            ownership: ownership);
        Must(unverified.Rows.Count == 1 && unverified.Rows[0].SetCode == "sld" &&
             unverified.Rows[0].UnresolvedName == 7,
            "Physical-not-verified must not treat name-only copies as a known edition.");
        var arena = snapshot.SearchFiltered("", new(
            Ownership: CatalogSnapshot.OwnershipMode.ArenaOwned), ownership: ownership);
        Must(arena.Rows.Count == 2 &&
             arena.Rows.Count(r => r.Name == "Sol Ring") == 1 &&
             arena.Rows.Single(r => r.Name == "Sol Ring").SetCode == "sld" &&
             arena.Rows.Any(r => r.Name == "Arena Exclusive"),
            "Arena ownership must show only Arena-available printings for owned card names.");
        Must(arena.Rows.All(r => r.Name != "Only Physical") &&
             arena.Rows.Select(r => r.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() ==
                 arena.Rows.Count,
            "Arena inventory results must be one representative image per owned name.");
        Must(arena.Rows.All(r => r.Name != "Only Physical") &&
             !arena.Rows.Any(r => r.SetCode == "cmm"),
             "Paper-only cards and paper-only reprints must not appear in Arena ownership search.");

        var notArena = snapshot.SearchFiltered("", new(
            Ownership: CatalogSnapshot.OwnershipMode.ArenaNotOwned),
            ownership: ownership);
        Must(notArena.Rows.Count == 4 && notArena.Rows.All(r => r.ArenaOwned == 0 && r.Name != "Sol Ring"),
            "Arena not-owned filter should show only Arena-available unowned names.");

        var combined = snapshot.SearchFiltered("", new(
            SetCode: "TST", ArenaOnly: true,
            Ownership: CatalogSnapshot.OwnershipMode.ArenaNotOwned),
            ownership: ownership);
        Must(combined.Rows.Count == 4 &&
             combined.Rows.All(r => r.SetCode == "tst"),
            "Ownership filters must compose with set and Arena-availability filters.");
        var arenaBrowse = snapshot.SearchFiltered("", new(ArenaOnly: true), ownership: ownership);
        Must(arenaBrowse.Rows.Count == 6 &&
             arenaBrowse.Rows.Select(x => x.Name)
                 .Distinct(StringComparer.OrdinalIgnoreCase).Count() == arenaBrowse.Rows.Count &&
             !arenaBrowse.Rows.Any(x => x.SetCode == "cmm"),
            "On Arena must collapse repeated Arena-eligible printings by card name.");
        var truncated = snapshot.SearchFiltered("", new(
            Ownership: CatalogSnapshot.OwnershipMode.PhysicalNotVerified),
            limit: 1, ownership: ownership);
        Must(truncated.Rows.Count == 1 && truncated.MayHaveMore,
            "Owned/unverified result caps must count filtered matches, not pre-filter SQL rows.");

        try
        {
            snapshot.SearchFiltered("Sol", new(
                Ownership: CatalogSnapshot.OwnershipMode.PhysicalVerified));
            throw new InvalidOperationException(
                "Filtering without an ownership source must fail closed.");
        }
        catch (InvalidDataException) { }

        Must(Hash(catalogPath) == catalogHash && Hash(backupPath) == backupHash,
            "All ownership filtering must leave catalog and collection bytes untouched.");
    }
}
