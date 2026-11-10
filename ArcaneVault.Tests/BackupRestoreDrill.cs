using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ArcaneVault.Tests;

/// <summary>
/// Phase 0: intentionally disposable backup -> SQLite staging -> read-back drill.
/// This is NOT the application's restore implementation. It never opens or
/// modifies the installed WebView2 profile, app SQLite catalog, or user files.
/// </summary>
internal static class BackupRestoreDrill
{
    private static readonly string[] Sections = ["cards", "physicalCards", "deckLibrary"];

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Backup restore drill: " + message);
    }

    private static JsonElement Required(JsonElement parent, string property, JsonValueKind kind)
    {
        Check(parent.TryGetProperty(property, out var value) && value.ValueKind == kind,
            "missing/invalid " + property);
        return value;
    }

    private static string NonemptyName(JsonElement value)
    {
        var name = Required(value, "name", JsonValueKind.String).GetString();
        Check(!string.IsNullOrWhiteSpace(name), "empty record name");
        return name!;
    }

    private static int Quantity(JsonElement value, bool zeroAllowed)
    {
        var quantity = Required(value, "qty", JsonValueKind.Number);
        Check(quantity.TryGetInt32(out var n) && (zeroAllowed ? n >= 0 : n > 0)
            && n <= 99999, "invalid quantity");
        return n;
    }

    private static string StringOrEmpty(JsonElement record, string key)
    {
        if (!record.TryGetProperty(key, out var value) || value.ValueKind == JsonValueKind.Null)
            return "";
        Check(value.ValueKind == JsonValueKind.String, "invalid " + key);
        return value.GetString() ?? "";
    }

    private static void Execute(SqliteConnection connection, SqliteTransaction tx, string sql)
    {
        using var c = connection.CreateCommand();
        c.Transaction = tx;
        c.CommandText = sql;
        c.ExecuteNonQuery();
    }

    private static void Stage(SqliteConnection connection, string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Check(root.ValueKind == JsonValueKind.Object, "expected backup JSON object");
        // The shipped application's full backup exports 'schema:2' with cards,
        // physicalCards, deckLibrary and preferredArtwork.
        Check(root.TryGetProperty("schema", out var schema) && schema.ValueKind == JsonValueKind.Number
            && schema.TryGetInt32(out var schemaVersion) && schemaVersion == 2, "not a schema 2 full backup");
        foreach (var section in Sections) Required(root, section, JsonValueKind.Array);
        var artwork = Required(root, "preferredArtwork", JsonValueKind.Object);

        using var tx = connection.BeginTransaction();
        // Replace staging contents transactionally; a failed restore leaves the
        // previous disposable staged snapshot untouched.
        Execute(connection, tx,
            "CREATE TABLE IF NOT EXISTS audit_document (id INTEGER PRIMARY KEY CHECK(id=1), payload TEXT NOT NULL, sha256 TEXT NOT NULL)");
        Execute(connection, tx,
            "CREATE TABLE IF NOT EXISTS audit_records (section TEXT NOT NULL, ordinal INTEGER NOT NULL," +
            " name TEXT NOT NULL, qty INTEGER, setcode TEXT NOT NULL, collector TEXT NOT NULL," +
            " print_id TEXT NOT NULL, json TEXT NOT NULL, PRIMARY KEY(section,ordinal))");
        Execute(connection, tx, "DELETE FROM audit_document");
        Execute(connection, tx, "DELETE FROM audit_records");

        using var add = connection.CreateCommand();
        add.Transaction = tx;
        add.CommandText = "INSERT INTO audit_records(section,ordinal,name,qty,setcode,collector,print_id,json)" +
                          " VALUES($section,$ordinal,$name,$qty,$set,$collector,$print,$json)";
        foreach (var p in new[] { "$section", "$ordinal", "$name", "$qty", "$set", "$collector", "$print", "$json" })
            add.Parameters.Add(new SqliteParameter(p, ""));

        foreach (var section in Sections)
        {
            var ordinal = 0;
            foreach (var record in root.GetProperty(section).EnumerateArray())
            {
                Check(record.ValueKind == JsonValueKind.Object, "invalid " + section + " record");
                var name = NonemptyName(record);
                int? qty = section == "deckLibrary" ? null : Quantity(record, section == "cards");
                if (section == "deckLibrary") Required(record, "entries", JsonValueKind.Array);
                add.Parameters["$section"].Value = section;
                add.Parameters["$ordinal"].Value = ordinal++;
                add.Parameters["$name"].Value = name;
                add.Parameters["$qty"].Value = qty.HasValue ? qty.Value : DBNull.Value;
                add.Parameters["$set"].Value = section == "physicalCards" ? StringOrEmpty(record, "set") : "";
                add.Parameters["$collector"].Value = section == "physicalCards" ? StringOrEmpty(record, "number") : "";
                add.Parameters["$print"].Value = section == "physicalCards" ? StringOrEmpty(record, "scryfallId") : "";
                add.Parameters["$json"].Value = record.GetRawText();
                add.ExecuteNonQuery();
            }
        }
        var artOrdinal = 0;
        foreach (var item in artwork.EnumerateObject())
        {
            add.Parameters["$section"].Value = "preferredArtwork";
            add.Parameters["$ordinal"].Value = artOrdinal++;
            add.Parameters["$name"].Value = item.Name;
            add.Parameters["$qty"].Value = DBNull.Value;
            add.Parameters["$set"].Value = "";
            add.Parameters["$collector"].Value = "";
            add.Parameters["$print"].Value = "";
            add.Parameters["$json"].Value = item.Value.GetRawText();
            add.ExecuteNonQuery();
        }

        using var store = connection.CreateCommand();
        store.Transaction = tx;
        store.CommandText = "INSERT INTO audit_document(id,payload,sha256) VALUES(1,$json,$hash)";
        store.Parameters.AddWithValue("$json", json);
        store.Parameters.AddWithValue("$hash", Hash(json));
        store.ExecuteNonQuery();

        tx.Commit();
    }

    private static (string payload, string hash) ReadStaged(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT payload,sha256 FROM audit_document WHERE id=1";
        using var reader = cmd.ExecuteReader();
        Check(reader.Read(), "missing staged backup");
        return (reader.GetString(0), reader.GetString(1));
    }

    private static (long count, long total) Count(SqliteConnection connection, string section)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*), COALESCE(SUM(qty),0) FROM audit_records WHERE section=$section";
        cmd.Parameters.AddWithValue("$section", section);
        using var reader = cmd.ExecuteReader();
        Check(reader.Read(), "missing record counts");
        return (reader.GetInt64(0), reader.GetInt64(1));
    }

    private static void VerifyRows(SqliteConnection connection, JsonElement source, string section)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT ordinal,json FROM audit_records WHERE section=$section ORDER BY ordinal";
        cmd.Parameters.AddWithValue("$section", section);
        using var reader = cmd.ExecuteReader();
        var originals = source.GetProperty(section).EnumerateArray().ToArray();
        for (var i = 0; i < originals.Length; i++)
        {
            Check(reader.Read(), section + " row missing after restore");
            Check(reader.GetInt32(0) == i, section + " order/identity changed");
            using var restored = JsonDocument.Parse(reader.GetString(1));
            Check(JsonSerializer.Serialize(originals[i]) == JsonSerializer.Serialize(restored.RootElement),
                section + " record content changed");
        }
        Check(!reader.Read(), section + " has extra restored rows");
    }

    public static void Run(string temporaryDirectory)
    {
        // Only synthetic fixture data. Never pass the live app profile to this function.
        var source = JsonSerializer.Serialize(new
        {
            schema = 2,
            cards = new object[] {
                new { name = "Sample // Other Face", qty = 2, sets = new[]{"SMP"} },
                new { name = "Zero Copies", qty = 0, sets = new[]{"SMP"} }
            },
            physicalCards = new object[] {
                new { name = "Sample // Other Face", qty = 1, set = "smp", number = "13",
                    finish = "foil", scryfallId = "00000000-0000-4000-8000-000000000001" },
                new { name = "Sample // Other Face", qty = 3, set = "alt", number = "77",
                    finish = "nonfoil", scryfallId = "" },
                new { name = "Unknown Printing", qty = 2, set = "", number = "",
                    finish = "nonfoil", scryfallId = "" }
            },
            deckLibrary = new[] {
                new { id = "deck-1", name = "Sample Commander Deck", commander = "Sample // Other Face",
                    entries = new[] { new {name="Sample // Other Face",qty=1} },
                    revisions = new[] { new {name="Previous Revision",entries=new[] { new {name="Older Card",qty=2} } } } }
            },
            preferredArtwork = new Dictionary<string, object> {
                ["sample // other face"] = new { image = "https://cards.scryfall.io/sample.jpg",
                    id = "00000000-0000-4000-8000-000000000001" }
            },
            wildcards = new { rare=2, mythic=1 }
        });
        var path = Path.Combine(temporaryDirectory, "disposable-backup-staging.sqlite");
        Check(!File.Exists(path), "refusing to open an existing staging database");
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path, Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString());
        connection.Open();
        Stage(connection, source);

        var restored = ReadStaged(connection);
        Check(restored.payload == source && restored.hash == Hash(source), "full backup round-trip mismatch");
        using (var sourceDoc = JsonDocument.Parse(source))
        {
            foreach (var section in Sections) VerifyRows(connection, sourceDoc.RootElement, section);
            Check(Count(connection,"cards") == (2,2), "Arena inventory count changed");
            Check(Count(connection,"physicalCards") == (3,6), "physical inventory count changed");
            Check(Count(connection,"deckLibrary") == (1,0), "deck count changed");
            Check(Count(connection,"preferredArtwork") == (1,0), "artwork count changed");
        }

        // A failed (partially processed) restore must preserve prior committed
        // snapshot and rows. Modify a late physical row so SQL has already changed.
        using var malformed = JsonDocument.Parse(source);
        var changed = JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(malformed.RootElement.GetRawText())!;
        var corrupt = JsonSerializer.Serialize(new {
            schema=2,
            cards=new[] { new { name="Transient", qty=55, sets=new[]{"BAD"} } },
            physicalCards=new object[] {new {name="Transient",qty=1},new {name="Invalid",qty=-9}},
            deckLibrary=new object[] {new {name="Transient Deck",entries=new object[] { } }},
            preferredArtwork=new { }
        });
        var rejected = false;
        try { Stage(connection, corrupt); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "corrupt restore was accepted");
        Check(ReadStaged(connection) == restored, "failure changed stored backup document");
        Check(Count(connection,"physicalCards") == (3,6), "failure changed existing staged physical rows");
        Check(Count(connection,"cards") == (2,2), "failure changed existing staged Arena rows");
        Console.WriteLine("PASS: disposable backup SQLite restore preserves printings, quantities, decks and artwork; interrupted/invalid restore rolls back");
    }
}
