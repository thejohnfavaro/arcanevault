using ArcaneVault.Next.Collection;
using Microsoft.Data.Sqlite;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

internal static class NativeExcelImportChecks
{
    private const string KnownId = "00000000-0000-4000-8000-000000000501";
    private const string IslandId = "00000000-0000-4000-8000-000000000502";

    private static void Must(bool actual, string message)
    {
        if (!actual) throw new InvalidOperationException("Excel collection import: " + message);
    }

    private static void Reject(Action action)
    {
        try { action(); }
        catch (InvalidDataException) { return; }
        throw new InvalidOperationException("Unsafe Excel collection change was accepted.");
    }

    private static string Hash(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private static void Excel(string path, bool invalidQty = false)
    {
        string q = invalidQty ? "1.75" : "2";
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        void Add(string part, string xml)
        {
            using var writer = new StreamWriter(zip.CreateEntry(part).Open(),
                new UTF8Encoding(false));
            writer.Write(xml);
        }
        Add("xl/workbook.xml", """
            <?xml version="1.0" encoding="UTF-8"?>
            <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"
              xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <sheets><sheet name="Collection" sheetId="1" r:id="rId1"/></sheets>
            </workbook>
            """);
        Add("xl/_rels/workbook.xml.rels", """
            <?xml version="1.0" encoding="UTF-8"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"
                Target="worksheets/sheet1.xml"/>
            </Relationships>
            """);
        string Cell(string addr, string value, bool numeric = false) => numeric
            ? "<c r=\"" + addr + "\"><v>" + value + "</v></c>"
            : "<c r=\"" + addr + "\" t=\"inlineStr\"><is><t>" +
                System.Security.SecurityElement.Escape(value) + "</t></is></c>";
        string Row(int number, params string[] fields)
        {
            var cells = new StringBuilder();
            for (int i = 0; i < fields.Length; i++)
                cells.Append(Cell(((char)('A' + i)).ToString() + number,
                    fields[i], number > 1 && i == 1));
            return "<row r=\"" + number + "\">" + cells + "</row>";
        }
        Add("xl/worksheets/sheet1.xml",
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
            "<sheetData>" +
            Row(1, "Name", "Quantity", "Set Code", "Collector Number", "Finish", "Scryfall ID") +
            Row(2, "Sol Ring", q, "TST", "21", "foil", KnownId) +
            Row(3, "Island", "3", "TST", "11", "nonfoil", "") +
            Row(4, "Unknown Relic", "5", "", "", "", "") +
            Row(5, "Sol Ring", "1", "TST", "21", "foil", KnownId) +
            "</sheetData></worksheet>");
    }

    private static byte[] Collection() => JsonSerializer.SerializeToUtf8Bytes(new
    {
        schema = 2,
        cards = new[] { new { name = "Arena Card", qty = 4 } },
        physicalCards = new[] {
            new { name = "Sol Ring", qty = 1, set = "tst", number = "21",
                finish = "foil", scryfallId = KnownId }
        },
        deckLibrary = new[] { new { id = "deck_x", name = "Existing Deck",
            entries = Array.Empty<object>(), revisions = Array.Empty<object>() } },
        preferredArtwork = new { preferred = new { name = "art1" } },
        wildcards = new { uncommon = 10 }
    });

    public static void Run(string work)
    {
        string prefix = Path.Combine(work, "native-excel-fixture");
        Directory.CreateDirectory(prefix);
        string workbook = Path.Combine(prefix, "collection.xlsx");
        string invalid = Path.Combine(prefix, "invalid.xlsx");
        Excel(workbook);
        Excel(invalid, invalidQty: true);
        var rows = NativeExcelCollectionImport.Read(workbook);
        Must(rows.Count == 4 && rows[0].Name == "Sol Ring" &&
             rows[0].Quantity == 2 && rows[2].Name == "Unknown Relic" &&
             rows[2].Finish == "unspecified", "Excel header, quantity or finish parsing failed.");
        Reject(() => NativeExcelCollectionImport.Read(invalid));

        string catalog = Path.Combine(prefix, "catalog.sqlite");
        using (var db = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = catalog, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false
        }.ToString()))
        {
            db.Open();
            using var ddl = db.CreateCommand();
            ddl.CommandText = """
                CREATE TABLE variants(
                  print_id TEXT PRIMARY KEY,name TEXT,setcode TEXT,collector TEXT,
                  games TEXT,digital INTEGER);
                INSERT INTO variants VALUES
                ('00000000-0000-4000-8000-000000000501','Sol Ring','tst','21','paper',0),
                ('00000000-0000-4000-8000-000000000502','Island','tst','11','paper',0);
                """;
            ddl.ExecuteNonQuery();
        }
        string catalogHash = Hash(catalog), workbookHash = Hash(workbook);
        var preview = NativeExcelCollectionImport.Prepare(rows, catalog);
        Must(preview.Items.Count == 4 && preview.Copies == 11 &&
             preview.VerifiedRows == 3 && preview.VerifiedCopies == 6 &&
             preview.UnresolvedCopies == 5 &&
             preview.Items[1].ScryfallId == IslandId &&
             preview.Items[2].ScryfallId == "",
            "Exact printing matching or unresolved row handling failed.");

        byte[] source = Collection();
        var updated = NativeExcelCollectionImport.Add(source, preview);
        var before = ArcaneVault.Next.Domain.NativeBackupReader.Read(source);
        var after = ArcaneVault.Next.Domain.NativeBackupReader.Read(updated);
        Must(after.PhysicalCopies == before.PhysicalCopies + 11 &&
             after.ArenaCopies == before.ArenaCopies &&
             after.Decks == before.Decks &&
             after.DeckRevisions == before.DeckRevisions &&
             after.PreferredArtworks == before.PreferredArtworks &&
             after.PhysicalCards.Count == 3,
            "Additive Excel changes modified unrelated native sections.");
        var ring = after.PhysicalCards.Single(x => x.Name == "Sol Ring");
        Must(ring.Quantity == 4 && ring.ScryfallId == KnownId,
            "Verified duplicate printing/finish rows were not merged additively.");
        var unresolved = after.PhysicalCards.Single(x => x.Name == "Unknown Relic");
        Must(unresolved.Quantity == 5 && unresolved.ScryfallId == "" &&
             unresolved.Finish == "unspecified",
            "Unresolved rows must be preserved without inventing a printing or finish.");

        string native = Path.Combine(prefix, "native.sqlite");
        var original = NativeWorkspace.ImportNewBytes(source, "excel-test", native);
        var saved = NativeWorkspaceSafety.AddExcelPhysical(native, catalog, rows,
            original.BackupSha256);
        Must(saved.Current.Collection.PhysicalCopies == 12 &&
             saved.Current.Collection.ArenaCopies == 4 &&
             File.Exists(saved.RecoveryPath),
            "Native Excel import must commit once and retain complete rollback.");
        Must(Hash(workbook) == workbookHash && Hash(catalog) == catalogHash,
            "Import may not alter source Excel or official Scryfall catalog.");
        var recovery = NativeWorkspace.Open(saved.RecoveryPath);
        Must(recovery.Collection.PhysicalCopies == 1 &&
             recovery.Collection.ArenaCopies == 4,
            "Import recovery must preserve full original inventory.");
        Reject(() => NativeWorkspaceSafety.AddExcelPhysical(native, catalog, rows,
            original.BackupSha256));
        Must(NativeWorkspace.Open(native).Collection.PhysicalCopies == 12,
            "Stale-SHA import should not double-increment quantities.");
        // The same XLSX source can now be treated as a target snapshot.
        // Existing additive APIs are retained only for older integrations.
        string reconcilePath = Path.Combine(prefix, "excel-reconciled.sqlite");
        var origin = NativeWorkspace.ImportNewBytes(
            Collection(), "xlsx reconcile test", reconcilePath);
        var difference = NativeWorkspaceSafety.PreviewPhysicalReconciliation(
            reconcilePath, catalog, rows, origin.BackupSha256);
        Must(difference.HasChanges && difference.AddedRows == 2 &&
             difference.AdjustedRows == 1 &&
             difference.Lines.Single(x => x.Card == "Sol Ring").Imported == 3 &&
             difference.NetCopies == 10,
             "Excel reconciliation must aggregate duplicate printings, compare current to imported values and show net changes.");
        var applied = NativeWorkspaceSafety.ReconcilePhysicalImport(
            reconcilePath, catalog, rows, origin.BackupSha256,
            difference.CandidateSha256);
        Must(applied.Current.Collection.PhysicalCopies == 11 &&
             applied.Current.Collection.PhysicalCards.Single(x =>
                 x.Name == "Sol Ring").Quantity == 3 &&
             applied.Current.Collection.ArenaCopies == origin.Collection.ArenaCopies &&
             applied.Current.Collection.Decks == origin.Collection.Decks,
             "Excel reconciliation must apply desired print quantities without touching unrelated native data.");
        var idempotent = NativeWorkspaceSafety.PreviewPhysicalReconciliation(
            reconcilePath, catalog, rows, applied.Current.BackupSha256);
        Must(!idempotent.HasChanges && idempotent.UnchangedRows == 3 &&
             idempotent.NetCopies == 0 &&
             NativeWorkspace.Open(applied.RecoveryPath).Collection.PhysicalCopies == 1,
             "Excel reimport should become a no-op and preserve a recoverable original.");
        Reject(() => NativeWorkspaceSafety.ReconcilePhysicalImport(
            reconcilePath, catalog, rows, applied.Current.BackupSha256,
            difference.CandidateSha256 + "stale"));

    }
}
