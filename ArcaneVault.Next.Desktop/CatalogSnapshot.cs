using Microsoft.Data.Sqlite;
using System.IO;
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
    private readonly bool ownsTemporaryFile;
    public bool HasFullMetadata { get; }

    public enum ColorMatching { ContainsAll, Exact, ContainsAny }

    /// <summary>All filters are read-only local catalog predicates. ColorMask
    /// uses canonical MTG color-identity symbols WUBRG, not mana cost.</summary>
    public sealed record SearchOptions(
        string SetCode = "", string Rarity = "", string TypeContains = "",
        string ColorMask = "", ColorMatching ColorMatch = ColorMatching.ContainsAll,
        bool ColorlessOnly = false, bool ArenaOnly = false)
    {
        public bool IsDefault =>
            SetCode.Length == 0 && Rarity.Length == 0 && TypeContains.Length == 0 &&
            ColorMask.Length == 0 && !ColorlessOnly && !ArenaOnly;
    }

    private CatalogSnapshot(string path, long total, bool ownsTemporaryFile = true,
        bool hasFullMetadata = false)
    {
        snapshotPath = path;
        TotalPrintings = total;
        this.ownsTemporaryFile = ownsTemporaryFile;
        HasFullMetadata = hasFullMetadata;
    }

    public long TotalPrintings { get; }

    // The native EDH Lab preview loads the exact same isolated SQLite snapshot.
    internal string ReadOnlySnapshotPath => snapshotPath;

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
            query.CommandText = """
                SELECT COUNT(*) FROM sqlite_master
                WHERE type='table' AND name='cards'
                """;
            bool hasFullMetadata = Convert.ToInt64(query.ExecuteScalar()) == 1;
            cancellationToken.ThrowIfCancellationRequested();
            return new CatalogSnapshot(staging, count,
                hasFullMetadata: hasFullMetadata);
        }
        catch
        {
            DeleteSnapshot(staging);
            throw;
        }
    }

    /// <summary>
    /// Open the app-owned, persistent catalog directly without creating another
    /// large temporary database. The original legacy catalog remains separate.
    /// </summary>
    public static CatalogSnapshot OpenPersistent(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new FileNotFoundException("The native Scryfall catalog does not exist.", path);
        using var db = Connect(path, SqliteOpenMode.ReadOnly);
        using var command = db.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*) FROM sqlite_master
            WHERE type='table' AND name IN ('cards','variants')
            """;
        if (Convert.ToInt64(command.ExecuteScalar()) != 2)
            throw new InvalidDataException("The selected native catalog lacks cards or printings.");
        command.CommandText = "SELECT COUNT(*) FROM variants";
        long total = Convert.ToInt64(command.ExecuteScalar());
        if (total < 1)
            throw new InvalidDataException("The native catalog has no printings.");
        return new CatalogSnapshot(Path.GetFullPath(path), total,
            ownsTemporaryFile: false, hasFullMetadata: true);
    }

    /// <summary>
    /// Copy an explicitly selected catalog once into the private ArcaneVaultNext
    /// directory using SQLite's online backup, verify it, then atomically publish.
    /// Never overwrite installed/legacy catalog or an existing native copy.
    /// </summary>
    public static CatalogSnapshot SavePersistentCopy(string sourcePath, string targetPath)
    {
        var source = Path.GetFullPath(sourcePath);
        var target = Path.GetFullPath(targetPath);
        if (!Directory.Exists(Path.GetDirectoryName(target)!) || File.Exists(target) ||
            Directory.Exists(target) ||
            string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Native catalog already exists or destination is invalid.");
        string legacy = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ArcaneVault");
        if (target.StartsWith(Path.GetFullPath(legacy).TrimEnd(Path.DirectorySeparatorChar) +
                    Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Never write a native catalog inside the installed app profile.");

        string partial = target + "." + Guid.NewGuid().ToString("N") + ".partial";
        bool published = false;
        try
        {
            using (var db = Connect(source, SqliteOpenMode.ReadOnly))
            using (var output = Connect(partial, SqliteOpenMode.ReadWriteCreate))
            {
                db.BackupDatabase(output);
            }
            using var verified = OpenPersistent(partial);
            File.Move(partial, target, overwrite: false);
            published = true;
            return OpenPersistent(target);
        }
        finally
        {
            if (!published) DeleteSnapshot(partial);
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

    /// <summary>
    /// Bounded, parameterized printings query with combined set, rarity, type,
    /// mana color identity and Arena availability filters. The underlying
    /// Scryfall catalog is never written to or modified.
    /// </summary>
    public SearchResult SearchFiltered(string input, SearchOptions filters,
        int limit = 200, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filters);
        if (limit is < 1 or > 200)
            throw new ArgumentOutOfRangeException(nameof(limit));
        if (filters.IsDefault)
            return SearchPrefix(input, limit, cancellationToken);
        input = (input ?? "").Trim();
        if (input.Length > 160) input = input[..160];

        string color = filters.ColorMask.Trim().ToUpperInvariant();
        if (color.Length > 5 || color.Any(ch => !"WUBRG".Contains(ch)) ||
            color.Distinct().Count() != color.Length)
            throw new ArgumentException("Invalid color identity selection.");
        color = new string("WUBRG".Where(color.Contains).ToArray());
        if (filters.ColorlessOnly && color.Length > 0)
            throw new ArgumentException("Colorless and colored filters cannot both be selected.");
        string rarity = filters.Rarity.Trim().ToLowerInvariant();
        if (rarity.Length > 0 && rarity is not (
            "common" or "uncommon" or "rare" or "mythic" or "special" or "bonus"))
            throw new ArgumentException("Unknown catalog rarity filter.");
        string set = filters.SetCode.Trim();
        string type = filters.TypeContains.Trim();
        if (set.Length > 20 || type.Length > 80)
            throw new ArgumentException("Catalog filter exceeds its supported length.");
        bool needsCard = filters.ColorlessOnly || color.Length > 0 || type.Length > 0;
        if ((needsCard || filters.ArenaOnly || rarity.Length > 0) && !HasFullMetadata)
            throw new InvalidDataException(
                "This older catalog does not contain the metadata needed for that filter. " +
                "Sync the current native Scryfall catalog.");

        static string EscapeLike(string value) =>
            value.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");

        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            cancellationToken.ThrowIfCancellationRequested();
            using var db = Connect(snapshotPath, SqliteOpenMode.ReadOnly);
            using var cmd = db.CreateCommand();
            var where = new List<string> { @"v.name LIKE $prefix ESCAPE '\'" };
            cmd.Parameters.AddWithValue("$prefix", EscapeLike(input) + "%");
            if (needsCard)
                cmd.CommandText = """
                    SELECT v.name,v.setcode,v.collector FROM variants v
                    INNER JOIN cards c ON c.name = v.name COLLATE NOCASE
                    """;
            else cmd.CommandText =
                "SELECT v.name,v.setcode,v.collector FROM variants v\n";

            if (set.Length > 0)
            {
                where.Add("v.setcode = $set COLLATE NOCASE");
                cmd.Parameters.AddWithValue("$set", set);
            }
            if (rarity.Length > 0)
            {
                where.Add("v.rarity = $rarity COLLATE NOCASE");
                cmd.Parameters.AddWithValue("$rarity", rarity);
            }
            if (filters.ArenaOnly)
                where.Add("v.arena = 1");
            if (type.Length > 0)
            {
                where.Add(@"c.type LIKE $type ESCAPE '\' COLLATE NOCASE");
                cmd.Parameters.AddWithValue("$type", "%" + EscapeLike(type) + "%");
            }
            if (filters.ColorlessOnly)
                where.Add("c.colors = ''");
            else if (color.Length > 0 && filters.ColorMatch == ColorMatching.Exact)
            {
                where.Add("c.colors = $colors COLLATE NOCASE");
                cmd.Parameters.AddWithValue("$colors", color);
            }
            else if (color.Length > 0)
            {
                var selections = new List<string>();
                for (int i = 0; i < color.Length; i++)
                {
                    string param = "$color" + i;
                    selections.Add("instr(c.colors," + param + ") > 0");
                    cmd.Parameters.AddWithValue(param, color[i].ToString());
                }
                where.Add("(" + string.Join(filters.ColorMatch == ColorMatching.ContainsAny
                    ? " OR " : " AND ", selections) + ")");
            }

            cmd.CommandText += "\nWHERE " + string.Join(" AND ", where) +
                "\nORDER BY v.name COLLATE NOCASE,v.setcode,v.collector LIMIT $limit";
            cmd.Parameters.AddWithValue("$limit", limit + 1);
            var rows = new List<PrintingRow>(limit);
            bool hasExtra = false;
            var started = Stopwatch.GetTimestamp();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (rows.Count < limit)
                    rows.Add(new PrintingRow(reader.GetString(0),
                        reader.GetString(1), reader.GetString(2)));
                else { hasExtra = true; break; }
            }
            return new SearchResult(rows,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds, hasExtra);
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            if (ownsTemporaryFile) DeleteSnapshot(snapshotPath);
        }
    }
}
