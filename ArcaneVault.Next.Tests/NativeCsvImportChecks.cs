using ArcaneVault.Next.Collection;
using ArcaneVault.Next.Domain;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

internal static class NativeCsvImportChecks
{
    private const string SolId = "00000000-0000-4000-8000-000000000501";
    private const string IslandId = "00000000-0000-4000-8000-000000000502";

    private static void Must(bool actual, string why)
    {
        if (!actual) throw new InvalidOperationException("Native CSV importer: " + why);
    }

    private static void Reject(Action act)
    {
        try { act(); }
        catch (InvalidDataException) { return; }
        throw new InvalidOperationException("Native CSV importer accepted unsafe data.");
    }

    private static string Sha(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private static byte[] Backup() => JsonSerializer.SerializeToUtf8Bytes(new
    {
        schema = 2,
        cards = new[] { new { name = "Arena Card", qty = 4 } },
        physicalCards = new[]
        {
            new { name = "Sol Ring", qty = 1, set = "tst", number = "21",
                finish = "foil", scryfallId = SolId }
        },
        deckLibrary = new[] { new { id = "deck_csv_test", name = "Saved Deck",
            entries = Array.Empty<object>(), revisions = Array.Empty<object>() } },
        preferredArtwork = new { pinned = "custom" },
        wildcards = new { mythic = 2 },
        customSetting = "preserve me"
    });

    public static void Run(string work)
    {
        string root = Path.Combine(work, "native-csv-fixtures");
        Directory.CreateDirectory(root);
        string csv = Path.Combine(root, "manabox-style.csv");
        // Actual CSV document includes a BOM, escaped double-quotes, a quoted
        // comma in a card name, and a multiline note that must NOT shift rows.
        string sample = """
            Name,Quantity,Set Code,Collector Number,Finish,Scryfall ID,Notes
            "Sol Ring",2,tst,21,foil,00000000-0000-4000-8000-000000000501,"mint, sleeve"
            Island,3,tst,11,normal,,"note with
            wrapped line"
            "Strange, Card",4,,,,,"some ""quoted"" remarks"
            "Sol Ring",1,tst,21,foil,00000000-0000-4000-8000-000000000501,extra
            Unknown Relic,5,,,,,
            """;
        File.WriteAllText(csv, sample, new UTF8Encoding(true));
        string originalHash = Sha(csv);
        var rows = NativeCsvCollectionImport.Read(csv);
        Must(rows.Count == 5 && rows[0].Quantity == 2 &&
             rows[1].Name == "Island" && rows[1].Finish == "nonfoil" &&
             rows[2].Name == "Strange, Card" &&
             rows[4].SpreadsheetRow == 7 &&
             rows[4].Finish == "unspecified",
            "Quoted values, BOM, multi-line fields, finishes or source line numbers parsed incorrectly.");

        string semi = Path.Combine(root, "semicolon.csv");
        File.WriteAllText(semi, """
            sep=;
            Card Name;Qty;Set;Collector #;Foil;Scryfall ID
            "Sol Ring";2;tst;21;No;00000000-0000-4000-8000-000000000501
            """, new UTF8Encoding(false));
        var semiRows = NativeCsvCollectionImport.Read(semi);
        Must(semiRows.Count == 1 && semiRows[0].Name == "Sol Ring" &&
             semiRows[0].Quantity == 2 && semiRows[0].Finish == "nonfoil",
            "Semicolon and common alternate column aliases failed.");

        string brokenQuote = Path.Combine(root, "broken-quote.csv");
        File.WriteAllText(brokenQuote,
            "Name,Quantity\n\"Unterminated,3\n");
        Reject(() => NativeCsvCollectionImport.Read(brokenQuote));
        string invalidQty = Path.Combine(root, "fraction.csv");
        File.WriteAllText(invalidQty,
            "Name,Quantity\nSol Ring,2.5\n");
        Reject(() => NativeCsvCollectionImport.Read(invalidQty));
        string noHeader = Path.Combine(root, "no-header.csv");
        File.WriteAllText(noHeader,
            "Card,Price\nSol Ring,12\n");
        Reject(() => NativeCsvCollectionImport.Read(noHeader));
        string duplicateHeader = Path.Combine(root, "duplicate-header.csv");
        File.WriteAllText(duplicateHeader,
            "Name,Card Name,Quantity\nSol Ring,Sol Ring,2\n");
        Reject(() => NativeCsvCollectionImport.Read(duplicateHeader));

        string catalog = Path.Combine(root, "catalog.sqlite");
        using (var db = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = catalog, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false }.ToString()))
        {
            db.Open();
            using var ddl = db.CreateCommand();
            ddl.CommandText = """
                CREATE TABLE variants(
                    print_id TEXT PRIMARY KEY,
                    name TEXT, setcode TEXT, collector TEXT, games TEXT,
                    digital INTEGER);
                INSERT INTO variants VALUES
                ('00000000-0000-4000-8000-000000000501','Sol Ring','tst','21','paper',0),
                ('00000000-0000-4000-8000-000000000502','Island','tst','11','paper',0);
                """;
            ddl.ExecuteNonQuery();
        }
        string catalogHash = Sha(catalog);
        var preview = NativeExcelCollectionImport.Prepare(rows, catalog);
        Must(preview.Copies == 15 && preview.Items.Count == 5 &&
             preview.VerifiedRows == 3 && preview.VerifiedCopies == 6 &&
             preview.UnresolvedCopies == 9 &&
             preview.Items[1].ScryfallId == IslandId &&
             preview.Items[2].ScryfallId == "" &&
             preview.Items[4].Finish == "unspecified",
            "CSV preview must verify exact printing identities and keep name-only holdings unresolved.");

        var incoming = Backup();
        var converted = NativeExcelCollectionImport.Add(incoming, preview);
        var before = NativeBackupReader.Read(incoming);
        var after = NativeBackupReader.Read(converted);
        Must(after.PhysicalCopies == before.PhysicalCopies + 15 &&
             after.ArenaCopies == before.ArenaCopies &&
             after.Decks == before.Decks &&
             after.DeckRevisions == before.DeckRevisions &&
             after.PreferredArtworks == before.PreferredArtworks,
            "CSV transform must conserve unrelated Arena/deck/artwork collection data.");
        Must(after.PhysicalCards.Single(x => x.Name == "Sol Ring").Quantity == 4 &&
             after.PhysicalCards.Single(x => x.Name == "Strange, Card").ScryfallId == "",
            "Verified identical printings should add while unresolved names remain unresolved.");

        var workspace = Path.Combine(root, "native.sqlite");
        var first = NativeWorkspace.ImportNewBytes(incoming, "csv fixture", workspace);
        var committed = NativeWorkspaceSafety.AddPhysicalImport(
            workspace, catalog, rows, first.BackupSha256);
        Must(committed.Current.Collection.PhysicalCopies == 16 &&
             committed.Current.Collection.ArenaCopies == 4 &&
             File.Exists(committed.RecoveryPath),
            "CSV import must create a single complete verified recovery point.");
        Must(Sha(csv) == originalHash && Sha(catalog) == catalogHash,
            "CSV import must not mutate the uploaded CSV or offline Scryfall catalog.");
        Must(NativeWorkspace.Open(committed.RecoveryPath).Collection.PhysicalCopies == 1,
            "CSV import rollback did not preserve prior physical holdings.");
        Reject(() => NativeWorkspaceSafety.AddPhysicalImport(
            workspace, catalog, rows, first.BackupSha256));
        Must(NativeWorkspace.Open(workspace).Collection.PhysicalCopies == 16,
            "Stale collection checksum must prevent unintentional double import.");
    }
}
