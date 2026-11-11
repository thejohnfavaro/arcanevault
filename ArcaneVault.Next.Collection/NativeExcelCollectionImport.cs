using ArcaneVault.Next.Domain;
using Microsoft.Data.Sqlite;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Linq;

namespace ArcaneVault.Next.Collection;

/// <summary>
/// Read-only Excel (.xlsx) ingestion and an additive schema-2 physical collection
/// transform. Never treats the workbook as a replacement for the official
/// Scryfall printing catalog and never guesses exact paper editions.
/// </summary>
public static class NativeExcelCollectionImport
{
    public sealed record Row(int SpreadsheetRow, string Name, int Quantity,
        string SetCode, string CollectorNumber, string Finish, string ScryfallId);
    public sealed record Item(Row Source, string Status, bool Verified,
        string Name, string SetCode, string CollectorNumber,
        string Finish, string ScryfallId);
    public sealed record Preview(IReadOnlyList<Item> Items)
    {
        public long Copies => Items.Sum(x => (long)x.Source.Quantity);
        public long VerifiedCopies => Items.Where(x => x.Verified).Sum(x => (long)x.Source.Quantity);
        public long UnresolvedCopies => Copies - VerifiedCopies;
        public int VerifiedRows => Items.Count(x => x.Verified);
        public int UnresolvedRows => Items.Count - VerifiedRows;
    }

    private static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace P = "http://schemas.openxmlformats.org/package/2006/relationships";

    private static XDocument Xml(ZipArchiveEntry entry, int maxCharacters = 20_000_000)
    {
        if (entry.Length > maxCharacters)
            throw new InvalidDataException("Excel XML part exceeds the supported size.");
        using var input = entry.Open();
        using var reader = XmlReader.Create(input, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            MaxCharactersInDocument = maxCharacters,
            XmlResolver = null
        });
        return XDocument.Load(reader);
    }

    private static ZipArchiveEntry Required(ZipArchive zip, string name) =>
        zip.GetEntry(name) ??
        throw new InvalidDataException("Excel workbook is missing " + name + ".");

    private static string CellValue(XElement cell, IReadOnlyList<string> strings)
    {
        string type = (string?)cell.Attribute("t") ?? "";
        if (type == "inlineStr")
            return string.Concat(cell.Descendants(S + "t").Select(x => x.Value));
        string value = (string?)cell.Element(S + "v") ?? "";
        if (type == "s")
        {
            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int n) ||
                n < 0 || n >= strings.Count)
                throw new InvalidDataException("Excel shared string index is invalid.");
            return strings[n];
        }
        if (type == "e")
            throw new InvalidDataException("Excel file contains an error-valued cell.");
        return value.Trim();
    }

    private static int Column(string address)
    {
        int col = 0;
        foreach (char c in address)
        {
            if (!char.IsLetter(c)) break;
            col = checked(col * 26 + (char.ToUpperInvariant(c) - 'A' + 1));
            if (col > 64) throw new InvalidDataException("Excel worksheet exceeds 64 columns.");
        }
        return col - 1;
    }

    private static string NormalizeHeader(string text) =>
        new(text.Trim().ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    private static readonly Dictionary<string, string> Aliases =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["name"]="name", ["cardname"]="name", ["card"]="name",
            ["quantity"]="quantity", ["qty"]="quantity", ["count"]="quantity",
            ["copies"]="quantity", ["owned"]="quantity", ["cardquantity"]="quantity",
            ["set"]="set", ["setcode"]="set", ["edition"]="set", ["setid"]="set",
            ["setabbreviation"]="set",
            ["collectornumber"]="collector", ["collectorno"]="collector",
            ["collector"]="collector", ["cardnumber"]="collector",
            ["number"]="collector", ["collectornum"]="collector",
            ["scryfallid"]="id", ["scryfalluuid"]="id", ["scryfallprintingid"]="id",
            ["uuid"]="id",
            ["finish"]="finish", ["foil"]="finish", ["isfoil"]="finish",
            ["printingfinish"]="finish", ["treatment"]="finish"
        };

    private static Dictionary<string, int> Headers(string[] values)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < values.Length; i++)
            if (Aliases.TryGetValue(NormalizeHeader(values[i]), out var key))
            {
                if (map.ContainsKey(key))
                    throw new InvalidDataException("Excel contains duplicate " + key + " columns.");
                map[key] = i;
            }
        return map;
    }

    private static string Finish(string text)
    {
        return text.Trim().ToLowerInvariant() switch
        {
            "" or "unknown" or "unspecified" or "n/a" => "unspecified",
            "foil" or "yes" or "y" or "true" or "1" => "foil",
            "nonfoil" or "non-foil" or "non foil" or "no" or "n" or "false" or "0" => "nonfoil",
            "etched" or "etched foil" => "etched",
            var other => throw new InvalidDataException("Unsupported Excel finish value: " +
                other[..Math.Min(other.Length, 60)])
        };
    }

    private static int Quantity(string text, int row)
    {
        if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ||
            value != decimal.Truncate(value) || value is < 1 or > 99_999)
            throw new InvalidDataException($"Excel row {row}: quantity must be a whole number (1–99,999).");
        return (int)value;
    }

    /// <summary>Read the first visible Excel worksheet. No formulas are evaluated.</summary>
    public static IReadOnlyList<Row> Read(string file)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(file);
        var info = new FileInfo(file);
        if (!info.Exists || !info.Extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase) ||
            info.Length is < 1 or > 35_000_000)
            throw new InvalidDataException("Choose an Excel .xlsx file under 35 MB.");
        using var zip = ZipFile.OpenRead(file);
        if (zip.Entries.Count > 1500 || zip.Entries.Sum(x => x.Length) > 95_000_000)
            throw new InvalidDataException("Excel workbook has too many or oversized entries.");
        var wb = Xml(Required(zip, "xl/workbook.xml"), 1_000_000);
        var rels = Xml(Required(zip, "xl/_rels/workbook.xml.rels"), 1_000_000);
        var sheet = wb.Descendants(S + "sheet")
            .FirstOrDefault(x => (string?)x.Attribute("state") is not ("hidden" or "veryHidden"))
            ?? throw new InvalidDataException("Excel workbook has no visible worksheets.");
        string id = (string?)sheet.Attribute(R + "id") ??
            throw new InvalidDataException("Excel sheet has no relationship.");
        string target = (string?)rels.Descendants(P + "Relationship")
            .FirstOrDefault(x => (string?)x.Attribute("Id") == id)?.Attribute("Target") ??
            throw new InvalidDataException("Excel worksheet relationship is missing.");
        string path = target.StartsWith('/') ? target.TrimStart('/') :
            "xl/" + target.Replace('\\', '/');
        if (path.Split('/').Any(x => x == "..") || !path.StartsWith("xl/", StringComparison.Ordinal) ||
            path.Contains(':'))
            throw new InvalidDataException("Excel worksheet has an unsafe target path.");
        var xml = Xml(Required(zip, path), 45_000_000);
        string[] shared = Array.Empty<string>();
        var strings = zip.GetEntry("xl/sharedStrings.xml");
        if (strings != null)
        {
            var list = Xml(strings, 15_000_000);
            shared = list.Descendants(S + "si")
                .Select(si => string.Concat(si.Descendants(S + "t").Select(x => x.Value)))
                .ToArray();
        }
        var collected = new List<Row>();
        Dictionary<string, int>? columns = null;
        int physicalRows = 0;
        foreach (var row in xml.Descendants(S + "sheetData").Elements(S + "row"))
        {
            if (++physicalRows > 60_000)
                throw new InvalidDataException("Excel import supports at most 60,000 worksheet rows.");
            int rowIndex = int.TryParse((string?)row.Attribute("r"), out int parsed)
                ? parsed : physicalRows;
            var values = Enumerable.Repeat(string.Empty, 64).ToArray();
            foreach (var cell in row.Elements(S + "c"))
            {
                int col = Column((string?)cell.Attribute("r") ?? "");
                if (col is >= 0 and < 64)
                    values[col] = CellValue(cell, shared).Trim();
            }
            if (columns is null)
            {
                var candidate = Headers(values);
                if (candidate.ContainsKey("name") && candidate.ContainsKey("quantity"))
                {
                    columns = candidate;
                    continue;
                }
                if (physicalRows >= 25)
                    throw new InvalidDataException("Excel needs Name and Quantity headers within its first 25 rows.");
                continue;
            }
            string Value(string key) =>
                columns.TryGetValue(key, out int col) ? values[col] ?? "" : "";
            string name = Value("name").Trim();
            string qtyText = Value("quantity").Trim();
            if (name.Length == 0 && qtyText.Length == 0) continue;
            if (name.Length is < 1 or > 255 || name.Any(char.IsControl))
                throw new InvalidDataException($"Excel row {rowIndex} has an invalid card name.");
            int qty = Quantity(qtyText, rowIndex);
            string set = Value("set"), collector = Value("collector"), idText = Value("id");
            if (set.Length > 32 || collector.Length > 32 || idText.Length > 80)
                throw new InvalidDataException($"Excel row {rowIndex} has oversized printing metadata.");
            if (idText.Length > 0 && (!Guid.TryParse(idText, out var guid) || guid == Guid.Empty))
                throw new InvalidDataException($"Excel row {rowIndex} has an invalid Scryfall ID.");
            collected.Add(new Row(rowIndex, name, qty, set.ToLowerInvariant(), collector,
                Finish(Value("finish")), idText));
        }
        if (columns is null || collected.Count == 0)
            throw new InvalidDataException("Excel contains no valid physical card rows.");
        return collected.AsReadOnly();
    }

    public static Preview Prepare(IReadOnlyList<Row> rows, string? catalogPath)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Count == 0 || rows.Count > 60_000)
            throw new InvalidDataException("Excel import has no rows or is too large.");
        SqliteConnection? db = null;
        if (!string.IsNullOrWhiteSpace(catalogPath))
        {
            db = new SqliteConnection(new SqliteConnectionStringBuilder
            { DataSource = catalogPath, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
            db.Open();
        }
        try
        {
            var result = new List<Item>(rows.Count);
            foreach (var row in rows)
            {
                if (row.Quantity is < 1 or > 99_999 || string.IsNullOrWhiteSpace(row.Name))
                    throw new InvalidDataException($"Excel row {row.SpreadsheetRow} is invalid.");
                string id = row.ScryfallId, name = row.Name, set = row.SetCode,
                    collector = row.CollectorNumber, status = "Unresolved: exact printing not established";
                bool verified = false;
                if (db is not null && (id.Length > 0 || (set.Length > 0 && collector.Length > 0)))
                {
                    using var cmd = db.CreateCommand();
                    if (id.Length > 0)
                    {
                        cmd.CommandText = """
                            SELECT print_id,name,setcode,collector,games,digital FROM variants
                            WHERE print_id=$id COLLATE NOCASE
                            """;
                        cmd.Parameters.AddWithValue("$id", Guid.Parse(id).ToString("D"));
                    }
                    else
                    {
                        cmd.CommandText = """
                            SELECT print_id,name,setcode,collector,games,digital FROM variants
                            WHERE setcode=$set COLLATE NOCASE AND collector=$number COLLATE NOCASE
                              AND name=$name COLLATE NOCASE
                            """;
                        cmd.Parameters.AddWithValue("$set", set);
                        cmd.Parameters.AddWithValue("$number", collector);
                        cmd.Parameters.AddWithValue("$name", name);
                    }
                    using var matches = cmd.ExecuteReader();
                    if (matches.Read())
                    {
                        string foundId = matches.GetString(0), foundName = matches.GetString(1),
                            foundSet = matches.GetString(2), foundNumber = matches.GetString(3),
                            games = matches.GetString(4);
                        bool digital = matches.GetInt64(5) != 0;
                        bool paper = games.Split(',', StringSplitOptions.TrimEntries |
                            StringSplitOptions.RemoveEmptyEntries)
                            .Any(x => x.Equals("paper", StringComparison.OrdinalIgnoreCase));
                        if (matches.Read())
                            status = "Unresolved: ambiguous matching printings";
                        else if (!paper || digital)
                            status = "Unresolved: not a physical paper printing";
                        else if (!name.Equals(foundName, StringComparison.OrdinalIgnoreCase) ||
                            (set.Length > 0 && !set.Equals(foundSet, StringComparison.OrdinalIgnoreCase)) ||
                            (collector.Length > 0 && !collector.Equals(foundNumber, StringComparison.OrdinalIgnoreCase)))
                            status = "Unresolved: supplied name/set/number conflicts with printing";
                        else
                        {
                            verified = true;
                            id = foundId; name = foundName; set = foundSet;
                            collector = foundNumber; status = "Catalog-verified printing";
                        }
                    }
                    else status = "Unresolved: printing not found in local catalog";
                }
                else if (db is null)
                    status = "Unresolved: local Scryfall catalog is not loaded";
                result.Add(new Item(row, status, verified, name, set, collector,
                    row.Finish, id));
            }
            return new Preview(result.AsReadOnly());
        }
        finally { db?.Dispose(); }
    }

    /// <summary>
    /// Additive only. Preserves every unrelated JSON field. Do not combine
    /// verified and unresolved lines by card name alone.
    /// </summary>
    public static byte[] Add(byte[] source, Preview preview)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(preview);
        var before = NativeBackupReader.Read(source);
        var root = JsonNode.Parse(source) as JsonObject ??
            throw new InvalidDataException("Native backup is not a JSON object.");
        var physical = root["physicalCards"] as JsonArray ??
            throw new InvalidDataException("Native backup has no physicalCards.");
        // Index once: importing thousands of cards must not scan an entire
        // existing collection for each row (quadratic behavior).
        static string Part(string input) => input.Length + ":" + input;
        static string Key(string id, string name, string set,
            string collector, string finish)
        {
            if (Guid.TryParse(id, out var guid) && guid != Guid.Empty)
                return "P:" + guid.ToString("D") + ":" + Part(finish);
            return id.Length == 0
                ? "U:" + Part(name) + Part(set) + Part(collector) + Part(finish)
                : "";
        }
        var index = new Dictionary<string, List<JsonObject>>(StringComparer.OrdinalIgnoreCase);
        void AddIndex(JsonObject line)
        {
            string id = line["scryfallId"]?.GetValue<string>() ?? "";
            string key = Key(id, line["name"]?.GetValue<string>() ?? "",
                line["set"]?.GetValue<string>() ?? "",
                line["number"]?.GetValue<string>() ?? "",
                line["finish"]?.GetValue<string>() ?? "");
            if (key.Length == 0) return;
            if (!index.TryGetValue(key, out var matches))
                index[key] = matches = new List<JsonObject>();
            matches.Add(line);
        }
        foreach (var entry in physical)
        {
            if (entry is not JsonObject line)
                throw new InvalidDataException("Invalid existing physical row.");
            AddIndex(line);
        }
        long added = 0;
        foreach (var item in preview.Items)
        {
            string key = Key(item.ScryfallId, item.Name,
                item.SetCode, item.CollectorNumber, item.Finish);
            // Non-catalog-verified IDs must remain separate and unresolved.
            if (!item.Verified && item.ScryfallId.Length > 0) key = "";
            index.TryGetValue(key, out var candidates);
            if (candidates is { Count: > 1 })
                throw new InvalidDataException(
                    "Duplicate existing physical rows conflict; import canceled.");
            if (candidates is { Count: 1 })
            {
                var line = candidates[0];
                if (item.Verified &&
                    (!(line["name"]?.GetValue<string>() ?? "").Equals(
                        item.Name, StringComparison.OrdinalIgnoreCase) ||
                     !(line["set"]?.GetValue<string>() ?? "").Equals(
                        item.SetCode, StringComparison.OrdinalIgnoreCase) ||
                     !(line["number"]?.GetValue<string>() ?? "").Equals(
                        item.CollectorNumber, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidDataException(
                        "Existing verified printing has conflicting metadata.");
                int quantity = line["qty"]!.GetValue<int>();
                if ((long)quantity + item.Source.Quantity > 99_999)
                    throw new InvalidDataException(
                        "Import would exceed a line's quantity limit.");
                line["qty"] = quantity + item.Source.Quantity;
            }
            else
            {
                if (physical.Count >= 150_000)
                    throw new InvalidDataException("Native physical row capacity exceeded.");
                var line = new JsonObject
                {
                    ["name"] = item.Name,
                    ["qty"] = item.Source.Quantity,
                    ["set"] = item.SetCode,
                    ["number"] = item.CollectorNumber,
                    ["finish"] = item.Finish,
                    // Only keep supplied IDs; missing/ambiguous IDs are never invented.
                    ["scryfallId"] = item.ScryfallId
                };
                physical.Add(line);
                if (key.Length > 0) AddIndex(line);
            }
            added = checked(added + item.Source.Quantity);
        }
        var result = JsonSerializer.SerializeToUtf8Bytes(root);
        var after = NativeBackupReader.Read(result);
        if (after.PhysicalCopies != before.PhysicalCopies + added ||
            after.ArenaCopies != before.ArenaCopies ||
            after.ArenaCards.Count != before.ArenaCards.Count ||
            after.Decks != before.Decks ||
            after.DeckRevisions != before.DeckRevisions ||
            after.PreferredArtworks != before.PreferredArtworks)
            throw new InvalidDataException("Excel import failed native collection-conservation checks.");
        return result;
    }
}
