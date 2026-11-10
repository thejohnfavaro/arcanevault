using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text.Json;

namespace ArcaneVault.Next.Migration;

/// <summary>
/// AVN-005B: opt-in SQLite staging of an exported, complete backup. No live
/// collection database, profile, catalog or WebView state is ever modified.
/// This is NOT a cutover, restore or authoritative ownership store.
/// </summary>
public static class OfflineStaging
{
    private const long MaxBytes = 100L * 1024 * 1024;
    private const int MaxRows = 150_000;

    public sealed record StageReport(bool Verified, bool InstalledDataModified,
        int ArenaRows, long ArenaCopies, int PhysicalRows, long PhysicalCopies,
        int Decks, int DeckRevisions, int ArtworkPreferences, int ValidPrintIdRows);

    private sealed record SourceRow(string Section, int Ordinal, string? Key,
        long? Qty, string? PrintId, string Payload);

    private static void Need(bool ok, string message)
    {
        if (!ok) throw new InvalidDataException(message);
    }

    private static string? OptionalText(JsonElement row, string key)
    {
        if (!row.TryGetProperty(key, out var value) || value.ValueKind == JsonValueKind.Null)
            return null;
        Need(value.ValueKind == JsonValueKind.String, "Invalid backup text field.");
        return value.GetString();
    }

    private static JsonElement Required(JsonElement root, string key, JsonValueKind type)
    {
        Need(root.TryGetProperty(key, out var field) && field.ValueKind == type,
            "Complete full-backup section required.");
        return field;
    }

    private static IEnumerable<SourceRow> Rows(JsonElement root)
    {
        foreach (var (section, key) in new[] {
            ("arena","cards"), ("physical","physicalCards"), ("decks","deckLibrary") })
        {
            var array = Required(root, key, JsonValueKind.Array);
            Need(array.GetArrayLength() <= MaxRows, "Too many rows.");
            int ordinal = 0;
            foreach (var row in array.EnumerateArray())
            {
                Need(row.ValueKind == JsonValueKind.Object, "Invalid inventory row.");
                long? qty = null;
                if (section != "decks")
                {
                    var val = Required(row, "qty", JsonValueKind.Number);
                    Need(val.TryGetInt32(out int n) && n >= (section == "physical" ? 1 : 0)
                        && n <= 99_999, "Invalid inventory quantity.");
                    qty = n;
                }
                yield return new SourceRow(section, ordinal++, OptionalText(row, "name"),
                    qty, section == "physical" ? OptionalText(row, "scryfallId") : null,
                    row.GetRawText());
            }
        }

        var artwork = Required(root, "preferredArtwork", JsonValueKind.Object);
        int index = 0;
        foreach (var item in artwork.EnumerateObject())
        {
            Need(index < MaxRows, "Too many artwork mappings.");
            yield return new SourceRow("artwork", index++, item.Name, null, null,
                item.Value.GetRawText());
        }
    }

    private static SqliteConnection Open(string path, SqliteOpenMode mode)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder {
            DataSource = path, Mode = mode, Pooling = false
        }.ToString());
        connection.Open();
        return connection;
    }

    private static void Exec(SqliteConnection db, SqliteTransaction? tx, string sql)
    {
        using var cmd = db.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static void Cleanup(string file)
    {
        foreach (var ext in new[] { "", "-journal", "-wal", "-shm" })
            try { File.Delete(file + ext); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
    }

    public static StageReport Create(string backupPath, string outputPath) =>
        CreateCore(backupPath, outputPath, null);

    // Only native synthetic tests inject a fault partway through a transaction.
    internal static StageReport CreateCore(string backupPath, string outputPath, Action<int>? afterRecord)
    {
        Need(!string.IsNullOrWhiteSpace(backupPath) && !string.IsNullOrWhiteSpace(outputPath),
            "Explicit backup and destination required.");
        var source = Path.GetFullPath(backupPath);
        var target = Path.GetFullPath(outputPath);
        Need(target.EndsWith(".sqlite", StringComparison.OrdinalIgnoreCase),
            "Output must be a new .sqlite filename.");
        Need(!string.Equals(source, target, StringComparison.OrdinalIgnoreCase),
            "Cannot overwrite the backup.");
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(appData))
        {
            var protectedRoot = Path.GetFullPath(Path.Combine(appData, "ArcaneVault"))
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Need(!target.StartsWith(protectedRoot, StringComparison.OrdinalIgnoreCase),
                "Cannot create staging under the installed Arcane Vault profile.");
        }
        Need(!File.Exists(target) && !Directory.Exists(target), "Output already exists.");
        Need(Directory.Exists(Path.GetDirectoryName(target)!), "Output parent directory must exist.");

        // On Windows FileShare.Read prevents concurrent writes during the entire
        // preflight, copy and validation; input is never opened for writing.
        using var locked = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
        Need(locked.Length <= MaxBytes, "Backup exceeds supported size.");
        var preview = MigrationPreview.Analyze(source);
        Need(preview.Format == "full-backup-v2" && preview.ArenaRows.HasValue &&
            preview.ArenaCopies.HasValue && preview.PhysicalRows.HasValue &&
            preview.PhysicalCopies.HasValue && preview.Decks.HasValue &&
            preview.DeckRevisions.HasValue && preview.PreferredArtwork.HasValue &&
            preview.RowsWithValidPrintId.HasValue, "Incomplete or unsupported full backup.");
        var expected = new StageReport(true, false, preview.ArenaRows.Value, preview.ArenaCopies.Value,
            preview.PhysicalRows.Value, preview.PhysicalCopies.Value, preview.Decks.Value,
            preview.DeckRevisions.Value, preview.PreferredArtwork.Value, preview.RowsWithValidPrintId.Value);

        byte[] bytes = new byte[checked((int)locked.Length)];
        locked.ReadExactly(bytes);
        using var doc = JsonDocument.Parse(bytes);
        var root = doc.RootElement;
        Need(root.ValueKind == JsonValueKind.Object &&
             Required(root, "schema", JsonValueKind.Number).GetInt32() == 2,
             "Only schema 2 full backups are supported.");
        string sha = Convert.ToHexString(SHA256.HashData(bytes));
        string partial = target + "." + Guid.NewGuid().ToString("N") + ".partial";
        bool published = false;
        try
        {
            using (var db = Open(partial, SqliteOpenMode.ReadWriteCreate))
            {
                Exec(db, null, "PRAGMA journal_mode=DELETE;");
                Exec(db, null, "PRAGMA synchronous=FULL;");
                using var tx = db.BeginTransaction();
                Exec(db, tx, """
                    CREATE TABLE stage_document(
                        id INTEGER PRIMARY KEY CHECK(id=1),
                        version INTEGER NOT NULL CHECK(version=1),
                        sha256 TEXT NOT NULL,
                        backup BLOB NOT NULL
                    );
                    CREATE TABLE stage_records(
                        section TEXT NOT NULL CHECK(section IN ('arena','physical','decks','artwork')),
                        ordinal INTEGER NOT NULL CHECK(ordinal>=0),
                        source_key TEXT,
                        qty INTEGER,
                        print_id TEXT,
                        payload_json TEXT NOT NULL,
                        PRIMARY KEY(section,ordinal)
                    );
                    """);
                using (var insert = db.CreateCommand())
                {
                    insert.Transaction = tx;
                    insert.CommandText = """
                        INSERT INTO stage_records(section,ordinal,source_key,qty,print_id,payload_json)
                        VALUES($section,$ordinal,$key,$qty,$print,$json)
                        """;
                    foreach (var key in new[] { "$section","$ordinal","$key","$qty","$print","$json" })
                        insert.Parameters.Add(new SqliteParameter(key, DBNull.Value));
                    int count = 0;
                    foreach (var row in Rows(root))
                    {
                        insert.Parameters["$section"].Value = row.Section;
                        insert.Parameters["$ordinal"].Value = row.Ordinal;
                        insert.Parameters["$key"].Value = (object?)row.Key ?? DBNull.Value;
                        insert.Parameters["$qty"].Value = (object?)row.Qty ?? DBNull.Value;
                        insert.Parameters["$print"].Value = (object?)row.PrintId ?? DBNull.Value;
                        insert.Parameters["$json"].Value = row.Payload;
                        insert.ExecuteNonQuery();
                        afterRecord?.Invoke(++count);
                    }
                }
                using (var insert = db.CreateCommand())
                {
                    insert.Transaction = tx;
                    insert.CommandText =
                        "INSERT INTO stage_document(id,version,sha256,backup) VALUES(1,1,$sha,$backup)";
                    insert.Parameters.AddWithValue("$sha", sha);
                    insert.Parameters.AddWithValue("$backup", bytes);
                    insert.ExecuteNonQuery();
                }
                tx.Commit();
            }
            var verified = Verify(partial);
            Need(verified == expected, "Staged quantities do not match preflight.");
            // No overwrite: visible completed output only after full verification.
            File.Move(partial, target, overwrite: false);
            published = true;
            return verified;
        }
        finally
        {
            if (!published) Cleanup(partial);
        }
    }

    /// <summary>Checks exact byte hash and row-by-row readback in read-only mode.</summary>
    public static StageReport Verify(string stagedPath)
    {
        using var db = Open(Path.GetFullPath(stagedPath), SqliteOpenMode.ReadOnly);
        using (var quick = db.CreateCommand())
        {
            quick.CommandText = "PRAGMA quick_check";
            Need((string?)quick.ExecuteScalar() == "ok", "Staged SQLite integrity check failed.");
        }
        byte[] original;
        string digest;
        using (var command = db.CreateCommand())
        {
            command.CommandText = "SELECT version,sha256,backup FROM stage_document WHERE id=1";
            using var reader = command.ExecuteReader();
            Need(reader.Read() && reader.GetInt64(0) == 1L, "Unknown staging schema.");
            digest = reader.GetString(1);
            original = reader.GetFieldValue<byte[]>(2);
            Need(!reader.Read(), "Duplicate staging documents.");
        }
        Need(Convert.ToHexString(SHA256.HashData(original)) == digest,
            "Original source-document checksum mismatch.");
        using var doc = JsonDocument.Parse(original);
        var root = doc.RootElement;
        Need(root.ValueKind == JsonValueKind.Object &&
            Required(root, "schema", JsonValueKind.Number).GetInt32() == 2,
            "Invalid staged backup schema.");
        var sections = Rows(root).GroupBy(row => row.Section)
            .ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.Ordinal);
        int arenaRows=0, physicalRows=0, decks=0, revisions=0, art=0, withIds=0;
        long arenaCopies=0, physicalCopies=0;
        foreach (var section in new[] { "arena","physical","decks","artwork" })
        {
            var expected = sections.GetValueOrDefault(section) ?? [];
            using var query = db.CreateCommand();
            query.CommandText = """
                SELECT ordinal,source_key,qty,print_id,payload_json
                FROM stage_records WHERE section=$section ORDER BY ordinal
                """;
            query.Parameters.AddWithValue("$section", section);
            using var reader = query.ExecuteReader();
            foreach (var row in expected)
            {
                Need(reader.Read() && reader.GetInt32(0) == row.Ordinal &&
                    (reader.IsDBNull(1) ? null : reader.GetString(1)) == row.Key &&
                    (reader.IsDBNull(2) ? null : reader.GetInt64(2)) == row.Qty &&
                    (reader.IsDBNull(3) ? null : reader.GetString(3)) == row.PrintId &&
                    reader.GetString(4) == row.Payload, "Staged record differs from source.");
                switch (section)
                {
                    case "arena":
                        arenaRows++;
                        arenaCopies = checked(arenaCopies + row.Qty!.Value);
                        break;
                    case "physical":
                        physicalRows++;
                        physicalCopies = checked(physicalCopies + row.Qty!.Value);
                        if (Guid.TryParse(row.PrintId, out var id) && id != Guid.Empty) withIds++;
                        break;
                    case "decks":
                        decks++;
                        using (var deck = JsonDocument.Parse(row.Payload))
                            if (deck.RootElement.TryGetProperty("revisions", out var rev) &&
                                rev.ValueKind == JsonValueKind.Array)
                                revisions = checked(revisions + rev.GetArrayLength());
                        break;
                    case "artwork": art++; break;
                }
            }
            Need(!reader.Read(), "Unexpected extra staging records.");
        }
        using (var query = db.CreateCommand())
        {
            query.CommandText = "SELECT COUNT(*) FROM stage_records";
            Need(Convert.ToInt64(query.ExecuteScalar()) == (long)arenaRows+physicalRows+decks+art,
                "Staged record count mismatch.");
        }
        return new StageReport(true, false, arenaRows, arenaCopies, physicalRows, physicalCopies,
            decks, revisions, art, withIds);
    }
}
