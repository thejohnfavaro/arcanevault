using ArcaneVault.Next.Domain;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text.Json;

namespace ArcaneVault.Next.Collection;

/// <summary>
/// AVN-020: separate, persistent native Windows inventory database.
/// A complete, explicit backup is imported exactly once; no installed
/// ArcaneVault WebView profile, SQLite, source export or user decks are edited.
/// This initial slice is READ-ONLY after import. Never auto-overwrite.
/// </summary>
public static class NativeWorkspace
{
    public const int SchemaVersion = 1;
    public static string RootDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ArcaneVaultNext");
    public static string DefaultPath => Path.Combine(RootDirectory, "native-workspace.sqlite");
    public static string CatalogPath => Path.Combine(RootDirectory, "native-catalog.sqlite");

    public sealed record Workspace(
        NativeBackupView Collection,
        string ImportedAtUtc,
        string BackupSha256,
        string OriginalFileName,
        string DatabasePath);

    private static void Need(bool valid, string reason)
    {
        if (!valid) throw new InvalidDataException(reason);
    }

    private static SqliteConnection OpenDb(string path, SqliteOpenMode mode)
    {
        var db = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path, Mode = mode, Pooling = false
        }.ToString());
        db.Open();
        return db;
    }

    private static void Exec(SqliteConnection db, SqliteTransaction? tx, string sql)
    {
        using var cmd = db.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static void DeleteScratch(string path)
    {
        foreach (string ext in new[] { "", "-journal", "-wal", "-shm" })
        {
            try { File.Delete(path + ext); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static bool IsChildOf(string candidate, string directory)
    {
        var root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        return candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    public static Workspace ImportNew(string backupPath, string targetPath,
        CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        string source = Path.GetFullPath(backupPath);
        string target = Path.GetFullPath(targetPath);
        Need(!string.Equals(source, target, StringComparison.OrdinalIgnoreCase),
            "Workspace may not overwrite its backup.");
        // Keep the explicit user-export source locked while reading it.
        using var input = new FileStream(source, FileMode.Open,
            FileAccess.Read, FileShare.Read);
        Need(input.Length is > 0 and <= 100L * 1024 * 1024,
            "Full backup exceeds the 100 MiB supported limit.");
        cancellation.ThrowIfCancellationRequested();
        var bytes = new byte[checked((int)input.Length)];
        input.ReadExactly(bytes);
        return ImportNewBytes(bytes, Path.GetFileName(source), target, cancellation);
    }

    /// <summary>
    /// Direct native onboarding: import a validated IN-MEMORY extraction from
    /// an isolated legacy WebView2 profile snapshot. No export JSON file
    /// is required, and no plaintext backup is staged on disk.
    /// </summary>
    public static Workspace ImportNewBytes(byte[] bytes, string sourceDescription,
        string targetPath, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDescription);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        string target = Path.GetFullPath(targetPath);
        Need(target.EndsWith(".sqlite", StringComparison.OrdinalIgnoreCase),
            "Native workspace must end with .sqlite.");
        Need(!File.Exists(target) && !Directory.Exists(target),
            "Native workspace already exists. It was not replaced.");
        string legacy = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ArcaneVault");
        Need(!IsChildOf(target, legacy),
            "Do not create native workspace in the installed legacy app profile.");
        Need(Directory.Exists(Path.GetDirectoryName(target)!),
            "Choose an existing directory for the native workspace.");
        Need(bytes.Length is > 0 and <= 100 * 1024 * 1024,
            "Full backup exceeds the 100 MiB supported limit.");
        cancellation.ThrowIfCancellationRequested();
        var expected = NativeBackupReader.Read(bytes);
        var digest = Convert.ToHexString(SHA256.HashData(bytes));
        var sourceName = Path.GetFileName(sourceDescription);
        var importedAt = DateTimeOffset.UtcNow.ToString("O");
        string partial = target + "." + Guid.NewGuid().ToString("N") + ".partial";
        bool published = false;

        try
        {
            using (var db = OpenDb(partial, SqliteOpenMode.ReadWriteCreate))
            {
                Exec(db, null, "PRAGMA journal_mode=DELETE");
                Exec(db, null, "PRAGMA synchronous=FULL");
                using var tx = db.BeginTransaction();
                Exec(db, tx, """
                    CREATE TABLE manifest(
                        id INTEGER PRIMARY KEY CHECK(id=1),
                        version INTEGER NOT NULL CHECK(version=1),
                        imported_utc TEXT NOT NULL,
                        source_file TEXT NOT NULL,
                        backup_sha256 TEXT NOT NULL,
                        backup BLOB NOT NULL,
                        arena_copies INTEGER NOT NULL,
                        physical_copies INTEGER NOT NULL,
                        decks INTEGER NOT NULL,
                        revisions INTEGER NOT NULL,
                        artworks INTEGER NOT NULL
                    );
                    CREATE TABLE arena_lines(
                        ordinal INTEGER PRIMARY KEY CHECK(ordinal >= 0),
                        name TEXT NOT NULL, qty INTEGER NOT NULL,
                        payload_json TEXT NOT NULL
                    );
                    CREATE TABLE physical_lines(
                        ordinal INTEGER PRIMARY KEY CHECK(ordinal >= 0),
                        name TEXT NOT NULL, qty INTEGER NOT NULL,
                        setcode TEXT NOT NULL, collector TEXT NOT NULL,
                        finish TEXT NOT NULL, print_id TEXT NOT NULL,
                        payload_json TEXT NOT NULL
                    );
                    CREATE TABLE decks(
                        ordinal INTEGER PRIMARY KEY CHECK(ordinal >= 0),
                        payload_json TEXT NOT NULL
                    );
                    CREATE TABLE preferred_art(
                        ordinal INTEGER PRIMARY KEY CHECK(ordinal >= 0),
                        source_key TEXT NOT NULL,
                        payload_json TEXT NOT NULL
                    );
                    CREATE INDEX idx_native_physical_name ON physical_lines(name COLLATE NOCASE);
                    CREATE INDEX idx_native_arena_name ON arena_lines(name COLLATE NOCASE);
                    """);

                using var json = JsonDocument.Parse(bytes);
                var root = json.RootElement;
                var arena = root.GetProperty("cards");
                var physical = root.GetProperty("physicalCards");
                var decks = root.GetProperty("deckLibrary");
                var artwork = root.GetProperty("preferredArtwork");

                using (var insert = db.CreateCommand())
                {
                    insert.Transaction = tx;
                    insert.CommandText = """
                        INSERT INTO arena_lines(ordinal,name,qty,payload_json)
                        VALUES($i,$n,$q,$json)
                        """;
                    foreach (var param in new[] { "$i","$n","$q","$json" })
                        insert.Parameters.Add(new SqliteParameter(param, DBNull.Value));
                    int i = 0;
                    foreach (var row in arena.EnumerateArray())
                    {
                        cancellation.ThrowIfCancellationRequested();
                        insert.Parameters["$i"].Value = i;
                        insert.Parameters["$n"].Value = expected.ArenaCards[i].Name;
                        insert.Parameters["$q"].Value = expected.ArenaCards[i].Quantity;
                        insert.Parameters["$json"].Value = row.GetRawText();
                        insert.ExecuteNonQuery();
                        i++;
                    }
                }

                using (var insert = db.CreateCommand())
                {
                    insert.Transaction = tx;
                    insert.CommandText = """
                        INSERT INTO physical_lines(
                            ordinal,name,qty,setcode,collector,finish,print_id,payload_json)
                        VALUES($i,$n,$q,$set,$collector,$finish,$print,$json)
                        """;
                    foreach (var param in new[] {
                        "$i","$n","$q","$set","$collector","$finish","$print","$json" })
                        insert.Parameters.Add(new SqliteParameter(param, DBNull.Value));
                    int i = 0;
                    foreach (var row in physical.EnumerateArray())
                    {
                        cancellation.ThrowIfCancellationRequested();
                        var info = expected.PhysicalCards[i];
                        insert.Parameters["$i"].Value = i;
                        insert.Parameters["$n"].Value = info.Name;
                        insert.Parameters["$q"].Value = info.Quantity;
                        insert.Parameters["$set"].Value = info.SetCode;
                        insert.Parameters["$collector"].Value = info.CollectorNumber;
                        insert.Parameters["$finish"].Value = info.Finish;
                        insert.Parameters["$print"].Value = info.ScryfallId;
                        insert.Parameters["$json"].Value = row.GetRawText();
                        insert.ExecuteNonQuery();
                        i++;
                    }
                }

                void InsertPreserved(string table, string keyColumn,
                    IEnumerable<(string Key, string Json)> payloads)
                {
                    using var insert = db.CreateCommand();
                    insert.Transaction = tx;
                    insert.CommandText = keyColumn.Length == 0
                        ? $"INSERT INTO {table}(ordinal,payload_json) VALUES($i,$json)"
                        : $"INSERT INTO {table}(ordinal,source_key,payload_json) VALUES($i,$key,$json)";
                    insert.Parameters.AddWithValue("$i", 0);
                    insert.Parameters.AddWithValue("$json", "");
                    if (keyColumn.Length > 0) insert.Parameters.AddWithValue("$key", "");
                    int i = 0;
                    foreach (var (key, payload) in payloads)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        insert.Parameters["$i"].Value = i++;
                        insert.Parameters["$json"].Value = payload;
                        if (keyColumn.Length > 0) insert.Parameters["$key"].Value = key;
                        insert.ExecuteNonQuery();
                    }
                }

                InsertPreserved("decks", "",
                    decks.EnumerateArray().Select(x => ("", x.GetRawText())));
                InsertPreserved("preferred_art", "source_key",
                    artwork.EnumerateObject().Select(x => (x.Name, x.Value.GetRawText())));

                using (var manifest = db.CreateCommand())
                {
                    manifest.Transaction = tx;
                    manifest.CommandText = """
                        INSERT INTO manifest(id,version,imported_utc,source_file,
                           backup_sha256,backup,arena_copies,physical_copies,decks,revisions,artworks)
                        VALUES(1,1,$imported,$source,$digest,$bytes,$ac,$pc,$decks,$rev,$art)
                        """;
                    manifest.Parameters.AddWithValue("$imported", importedAt);
                    manifest.Parameters.AddWithValue("$source", sourceName);
                    manifest.Parameters.AddWithValue("$digest", digest);
                    manifest.Parameters.AddWithValue("$bytes", bytes);
                    manifest.Parameters.AddWithValue("$ac", expected.ArenaCopies);
                    manifest.Parameters.AddWithValue("$pc", expected.PhysicalCopies);
                    manifest.Parameters.AddWithValue("$decks", expected.Decks);
                    manifest.Parameters.AddWithValue("$rev", expected.DeckRevisions);
                    manifest.Parameters.AddWithValue("$art", expected.PreferredArtworks);
                    manifest.ExecuteNonQuery();
                }
                tx.Commit();
            }
            var tested = Open(partial, cancellation);
            Need(tested.BackupSha256 == digest &&
                tested.Collection.PhysicalCopies == expected.PhysicalCopies &&
                tested.Collection.ArenaCopies == expected.ArenaCopies,
                "Native workspace did not verify after import.");
            cancellation.ThrowIfCancellationRequested();
            File.Move(partial, target, overwrite: false);
            published = true;
            return tested with { DatabasePath = target };
        }
        finally
        {
            if (!published) DeleteScratch(partial);
        }
    }

    /// <summary>
    /// Reopen persisted collection state, check its source SHA, SQLite integrity
    /// and exact native row projection against the preserved full backup.
    /// Corruption never triggers an automatic overwrite or recovery.
    /// </summary>
    public static Workspace Open(string workspacePath, CancellationToken cancellation = default)
    {
        var path = Path.GetFullPath(workspacePath);
        Need(File.Exists(path), "No native workspace found.");
        using var db = OpenDb(path, SqliteOpenMode.ReadOnly);
        using (var quick = db.CreateCommand())
        {
            quick.CommandText = "PRAGMA quick_check";
            Need(string.Equals(quick.ExecuteScalar()?.ToString(), "ok", StringComparison.Ordinal),
                "Native workspace failed SQLite integrity check.");
        }

        byte[] bytes;
        string digest, importedAt, source;
        long arenaCopies, physicalCopies, decks, revisions, art;
        using (var command = db.CreateCommand())
        {
            command.CommandText = """
                SELECT version,imported_utc,source_file,backup_sha256,backup,
                    arena_copies,physical_copies,decks,revisions,artworks
                FROM manifest WHERE id=1
                """;
            using var reader = command.ExecuteReader();
            Need(reader.Read() && reader.GetInt64(0) == SchemaVersion,
                "Unsupported native workspace schema.");
            importedAt = reader.GetString(1);
            source = reader.GetString(2);
            digest = reader.GetString(3);
            bytes = reader.GetFieldValue<byte[]>(4);
            arenaCopies = reader.GetInt64(5);
            physicalCopies = reader.GetInt64(6);
            decks = reader.GetInt64(7);
            revisions = reader.GetInt64(8);
            art = reader.GetInt64(9);
            Need(!reader.Read(), "Duplicate native workspace manifest.");
        }
        Need(string.Equals(Convert.ToHexString(SHA256.HashData(bytes)),
            digest, StringComparison.Ordinal),
            "Native workspace's preserved backup checksum failed.");
        var expected = NativeBackupReader.Read(bytes);
        Need(expected.ArenaCopies == arenaCopies &&
             expected.PhysicalCopies == physicalCopies &&
             expected.Decks == decks &&
             expected.DeckRevisions == revisions &&
             expected.PreferredArtworks == art,
             "Native workspace manifest totals do not match its backup.");

        using var doc = JsonDocument.Parse(bytes);
        var root = doc.RootElement;
        var physical = new List<NativePhysicalRow>(expected.PhysicalCards.Count);
        var arena = new List<NativeArenaRow>(expected.ArenaCards.Count);

        using (var command = db.CreateCommand())
        {
            command.CommandText = """
                SELECT ordinal,name,qty,setcode,collector,finish,print_id,payload_json
                FROM physical_lines ORDER BY ordinal
                """;
            using var reader = command.ExecuteReader();
            int index = 0;
            var original = root.GetProperty("physicalCards").EnumerateArray().ToArray();
            while (reader.Read())
            {
                cancellation.ThrowIfCancellationRequested();
                Need(index < original.Length && reader.GetInt32(0) == index,
                    "Native physical line order or count differs from imported backup.");
                var line = new NativePhysicalRow(reader.GetString(1),
                    reader.GetInt32(2), reader.GetString(3), reader.GetString(4),
                    reader.GetString(5), reader.GetString(6));
                Need(line == expected.PhysicalCards[index] &&
                    reader.GetString(7) == original[index].GetRawText(),
                    "Native physical line differs from imported backup.");
                physical.Add(line);
                index++;
            }
            Need(index == original.Length, "Native physical line count differs from backup.");
        }
        using (var command = db.CreateCommand())
        {
            command.CommandText = """
                SELECT ordinal,name,qty,payload_json FROM arena_lines ORDER BY ordinal
                """;
            using var reader = command.ExecuteReader();
            int index = 0;
            var original = root.GetProperty("cards").EnumerateArray().ToArray();
            while (reader.Read())
            {
                cancellation.ThrowIfCancellationRequested();
                Need(index < original.Length && reader.GetInt32(0) == index,
                    "Native Arena line order or count differs from imported backup.");
                var line = new NativeArenaRow(reader.GetString(1), reader.GetInt32(2));
                Need(line == expected.ArenaCards[index] &&
                    reader.GetString(3) == original[index].GetRawText(),
                    "Native Arena line differs from imported backup.");
                arena.Add(line);
                index++;
            }
            Need(index == original.Length, "Native Arena line count differs from backup.");
        }
        void VerifySection(string table, string keyField,
            IEnumerable<(string Key, string Payload)> sourceRecords)
        {
            using var command = db.CreateCommand();
            command.CommandText = keyField.Length == 0
                ? $"SELECT ordinal,payload_json FROM {table} ORDER BY ordinal"
                : $"SELECT ordinal,source_key,payload_json FROM {table} ORDER BY ordinal";
            using var reader = command.ExecuteReader();
            int index = 0;
            foreach (var row in sourceRecords)
            {
                cancellation.ThrowIfCancellationRequested();
                Need(reader.Read() && reader.GetInt32(0) == index++,
                    $"Native {table} record count or order differs.");
                if (keyField.Length != 0)
                    Need(reader.GetString(1) == row.Key && reader.GetString(2) == row.Payload,
                        $"Native {table} record differs from backup.");
                else
                    Need(reader.GetString(1) == row.Payload,
                        $"Native {table} record differs from backup.");
            }
            Need(!reader.Read(), $"Native {table} contains unexpected extra records.");
        }
        VerifySection("decks", "",
            root.GetProperty("deckLibrary").EnumerateArray()
                .Select(x => ("", x.GetRawText())));
        VerifySection("preferred_art", "source_key",
            root.GetProperty("preferredArtwork").EnumerateObject()
                .Select(x => (x.Name, x.Value.GetRawText())));

        long pCopies = physical.Sum(x => (long)x.Quantity);
        long aCopies = arena.Sum(x => (long)x.Quantity);
        long valid = physical.Where(x => x.HasValidPrintId).Sum(x => (long)x.Quantity);
        Need(pCopies == physicalCopies && aCopies == arenaCopies,
            "Native imported inventory copies do not match preserved backup.");
        var snapshot = new NativeBackupView(physical.AsReadOnly(), arena.AsReadOnly(),
            expected.Decks, expected.DeckRevisions, expected.PreferredArtworks,
            pCopies, aCopies, valid, pCopies - valid);
        return new Workspace(snapshot, importedAt, digest, source, path);
    }
}
