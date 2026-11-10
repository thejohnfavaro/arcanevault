using Microsoft.Data.Sqlite;
using System.Diagnostics;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// Isolated, read-only catalog snapshot for the native UI spike. The source is
/// opened SQLite ReadOnly and backed up consistently into a temporary database;
/// neither the catalog nor user WebView data is updated by this class.
/// </summary>
public sealed class CatalogSnapshot : IDisposable
{
    public sealed record PrintingRow(string Name, string SetCode, string CollectorNumber);
    public sealed record SearchResult(IReadOnlyList<PrintingRow> Rows, double SqlMilliseconds, bool MayHaveMore);

    private readonly string snapshotPath;
    private readonly object gate = new();
    private bool disposed;

    private CatalogSnapshot(string path, long total)
    {
        snapshotPath = path;
        TotalPrintings = total;
    }

    public long TotalPrintings { get; }

    private static SqliteConnection Connect(string path, SqliteOpenMode mode)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = mode,
            Pooling = false
        }.ToString());
        connection.Open();
        return connection;
    }

    public static CatalogSnapshot Open(string sourcePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            throw new FileNotFoundException("Choose an existing Arcane Vault Scryfall catalog database.");

        var staging = Path.Combine(Path.GetTempPath(),
            "arcane-vault-next-catalog-" + Guid.NewGuid().ToString("N") + ".sqlite");
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using (var source = Connect(sourcePath, SqliteOpenMode.ReadOnly))
            using (var target = Connect(staging, SqliteOpenMode.ReadWriteCreate))
            {
                // SQLite's online backup API gives a transactionally consistent
                // snapshot, including any committed WAL records, without changing
                // the installed application database or its journal.
                source.BackupDatabase(target);
                cancellationToken.ThrowIfCancellationRequested();
            }

            using var snapshot = Connect(staging, SqliteOpenMode.ReadOnly);
            using var query = snapshot.CreateCommand();
            query.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='variants'";
            if (Convert.ToInt32(query.ExecuteScalar()) != 1)
                throw new InvalidDataException("The selected file does not contain an Arcane Vault printing catalog.");
            query.CommandText = "SELECT COUNT(*) FROM variants";
            long count = Convert.ToInt64(query.ExecuteScalar());
            cancellationToken.ThrowIfCancellationRequested();
            return new CatalogSnapshot(staging, count);
        }
        catch
        {
            DeleteSnapshot(staging);
            throw;
        }
    }

    private static void DeleteSnapshot(string filename)
    {
        foreach (var suffix in new[] { "", "-wal", "-shm" })
            try { File.Delete(filename + suffix); } catch (IOException) { }
            catch (UnauthorizedAccessException) { }
    }

    public SearchResult SearchPrefix(string input, int limit = 200, CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 200)
            throw new ArgumentOutOfRangeException(nameof(limit), "Query limit must be 1–200.");
        input = (input ?? "").Trim();
        if (input.Length > 160)
            input = input[..160];

        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            cancellationToken.ThrowIfCancellationRequested();
            using var db = Connect(snapshotPath, SqliteOpenMode.ReadOnly);
            using var cmd = db.CreateCommand();
            // ESCAPE ensures user-entered %, _, and \ are literal, not wildcards.
            // Prefix-only queries can use the existing NOCASE name index.
            cmd.CommandText = """
                SELECT name, setcode, collector FROM variants
                WHERE name LIKE $prefix ESCAPE '\'
                ORDER BY name COLLATE NOCASE, setcode, collector
                LIMIT $limit
                """;
            var escaped = input.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
            cmd.Parameters.AddWithValue("$prefix", escaped + "%");
            cmd.Parameters.AddWithValue("$limit", limit + 1);
            var rows = new List<PrintingRow>(limit);
            bool hasExtra = false;
            var started = Stopwatch.GetTimestamp();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (rows.Count < limit)
                    rows.Add(new PrintingRow(reader.GetString(0), reader.GetString(1), reader.GetString(2)));
                else { hasExtra = true; break; }
            }
            return new SearchResult(rows, Stopwatch.GetElapsedTime(started).TotalMilliseconds, hasExtra);
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            DeleteSnapshot(snapshotPath);
        }
    }
}
