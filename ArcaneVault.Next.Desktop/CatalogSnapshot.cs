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
    public sealed record PrintingRow(string Name, string SetCode, string CollectorNumber,
        int PhysicalOwned = 0, int ArenaOwned = 0, int UnresolvedName = 0,
        string ImageUrl = "", string RolesLabel = "", bool ArenaPresentation = false)
    {
        // Presentation only: preserve the original set code for identity and filters.
        public string DisplaySetCode => ArenaPresentation ? "ARENA CARD" : SetCode.ToUpperInvariant();
        public string DisplayCollectorNumber => ArenaPresentation ? "" : CollectorNumber;

        public string OwnershipLabel =>
            PhysicalOwned == 0 && ArenaOwned == 0 && UnresolvedName == 0
                ? ""
                : string.Join(" · ", new[]
                {
                    
                    UnresolvedName > 0 ? $"Unresolved name {UnresolvedName}" : ""
                }.Where(x => x.Length > 0));
    }
    public sealed record SearchResult(IReadOnlyList<PrintingRow> Rows, double SqlMilliseconds, bool MayHaveMore);

    private readonly string snapshotPath;
    private readonly object gate = new();
    // A card name may occur in dozens of printings. Its rules text/type are
    // identical for role classification, so derive roles once per name per
    // snapshot instead of running 18 regexes for every printing and filter.
    private readonly Dictionary<string, string> cachedRoleLabels =
        new(StringComparer.OrdinalIgnoreCase);
    private bool disposed;
    private readonly bool ownsTemporaryFile;
    public bool HasFullMetadata { get; }
    public bool HasPaperPrintingMetadata { get; }
    public bool HasRoleMetadata { get; }
    public bool HasPrintingArtwork { get; }

    public enum ColorMatching { ContainsAll, Exact, ContainsAny }
    public enum OwnershipMode
    {
        All, PhysicalVerified, PhysicalNotVerified, ArenaOwned, ArenaNotOwned
    }

    /// <summary>All filters are read-only local catalog predicates. ColorMask
    /// uses canonical MTG color-identity symbols WUBRG, not mana cost.</summary>
    public sealed record SearchOptions(
        string SetCode = "", string Rarity = "", string TypeContains = "",
        string ColorMask = "", ColorMatching ColorMatch = ColorMatching.ContainsAll,
        bool ColorlessOnly = false, bool ArenaOnly = false,
        OwnershipMode Ownership = OwnershipMode.All,
        string Role = "")
    {
        public bool IsDefault =>
            SetCode.Length == 0 && Rarity.Length == 0 && TypeContains.Length == 0 &&
            ColorMask.Length == 0 && !ColorlessOnly && !ArenaOnly &&
            Ownership == OwnershipMode.All && Role.Length == 0;
    }

    private CatalogSnapshot(string path, long total, bool ownsTemporaryFile = true,
        bool hasFullMetadata = false, bool hasPaperPrintingMetadata = false,
        bool hasRoleMetadata = false, bool hasPrintingArtwork = false)
    {
        snapshotPath = path;
        TotalPrintings = total;
        this.ownsTemporaryFile = ownsTemporaryFile;
        HasFullMetadata = hasFullMetadata;
        HasPaperPrintingMetadata = hasPaperPrintingMetadata;
        HasRoleMetadata = hasRoleMetadata;
        HasPrintingArtwork = hasPrintingArtwork;
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

    private static bool HasFields(SqliteConnection db, string table, params string[] required)
    {
        if (table is not ("cards" or "variants"))
            throw new ArgumentException("Unsupported catalog table.");
        using var cmd = db.CreateCommand();
        cmd.CommandText = "PRAGMA table_info(" + table + ")";
        var fields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) fields.Add(reader.GetString(1));
        return required.All(fields.Contains);
    }

    private static bool HasPaperFields(SqliteConnection db) =>
        HasFields(db, "variants", "games", "digital");

    private string RoleLabels(SqliteDataReader reader)
    {
        string name = reader.GetString(1);
        if (cachedRoleLabels.TryGetValue(name, out var result))
            return result;
        result = string.Join(" · ", NativeCardRoles.Derive(
            reader.IsDBNull(7) ? "" : reader.GetString(7),
            reader.IsDBNull(8) ? "" : reader.GetString(8)));
        cachedRoleLabels[name] = result;
        return result;
    }

    private PrintingRow ReadRow(SqliteDataReader reader,
        NativeCatalogOwnership? ownership, bool includeRoles, bool arenaPresentation = false)
    {
        string name = reader.GetString(1);
        string set = reader.GetString(2);
        string collector = reader.GetString(3);
        var counts = ownership?.ForPrinting(reader.GetString(0), name, set,
            collector, IsPaper(reader));
        string image = reader.IsDBNull(6) ? "" : reader.GetString(6);
        string roles = includeRoles ? RoleLabels(reader) : "";
        return new PrintingRow(name, set, collector,
            counts?.VerifiedPhysical ?? 0, counts?.Arena ?? 0,
            counts?.UnresolvedName ?? 0, image, roles, arenaPresentation);
    }

    private static bool IsPaper(SqliteDataReader reader)
    {
        if (reader.IsDBNull(4) || reader.IsDBNull(5) || reader.GetInt64(5) != 0)
            return false;
        return reader.GetString(4).Split(',',
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Any(x => x.Equals("paper", StringComparison.OrdinalIgnoreCase));
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
                hasFullMetadata: hasFullMetadata,
                hasPaperPrintingMetadata: HasPaperFields(snapshot),
                hasRoleMetadata: hasFullMetadata &&
                    HasFields(snapshot, "cards", "type", "oracle"),
                hasPrintingArtwork: HasFields(snapshot, "variants", "image"));
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
            ownsTemporaryFile: false, hasFullMetadata: true,
            hasPaperPrintingMetadata: HasPaperFields(db),
            hasRoleMetadata: HasFields(db, "cards", "type", "oracle"),
            hasPrintingArtwork: HasFields(db, "variants", "image"));
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

    public SearchResult SearchPrefix(string input, int limit = 200,
        CancellationToken cancellationToken = default,
        NativeCatalogOwnership? ownership = null)
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
            string paper = HasPaperPrintingMetadata
                ? "v.games,v.digital" : "'missing',1";
            string art = HasPrintingArtwork ? "v.image" : "''";
            string roles = HasRoleMetadata
                ? "c.type,c.oracle" : "'',''";
            string join = HasRoleMetadata
                ? " LEFT JOIN cards c ON c.name = v.name COLLATE NOCASE" : "";
            cmd.CommandText =
                "SELECT v.print_id,v.name,v.setcode,v.collector," +
                paper + "," + art + "," + roles +
                " FROM variants v" + join +
                @" WHERE v.name LIKE $prefix ESCAPE '\' " +
                " ORDER BY v.name COLLATE NOCASE,v.setcode,v.collector LIMIT $limit";
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
                {
                    rows.Add(ReadRow(reader, ownership, HasRoleMetadata));
                }
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
        int limit = 200, CancellationToken cancellationToken = default,
        NativeCatalogOwnership? ownership = null)
    {
        ArgumentNullException.ThrowIfNull(filters);
        if (limit is < 1 or > 200)
            throw new ArgumentOutOfRangeException(nameof(limit));
        if (filters.Ownership != OwnershipMode.All && ownership is null)
            throw new InvalidDataException(
                "Load or create a native collection to filter owned and unverified printings.");
        if ((filters.Ownership is OwnershipMode.PhysicalVerified or
                 OwnershipMode.PhysicalNotVerified) && !HasPaperPrintingMetadata)
            throw new InvalidDataException(
                "This catalog lacks Scryfall paper/digital printing metadata; refresh the native catalog before filtering physical ownership.");
        if (!NativeCardRoles.Valid(filters.Role))
            throw new ArgumentException("Unknown card utilization role.");
        if (!string.IsNullOrWhiteSpace(filters.Role) && !HasRoleMetadata)
            throw new InvalidDataException(
                "This catalog lacks Scryfall Oracle text for utilization tags. " +
                "Run Full Scryfall Sync to enable role filtering.");
        if (filters.IsDefault)
            return SearchPrefix(input, limit, cancellationToken, ownership);
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
        bool needsCard = filters.ColorlessOnly || color.Length > 0 || type.Length > 0 ||
            HasRoleMetadata || filters.Role.Length > 0;
        if ((needsCard || filters.ArenaOnly ||
             filters.Ownership is OwnershipMode.ArenaOwned or OwnershipMode.ArenaNotOwned ||
             rarity.Length > 0) && !HasFullMetadata)
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
            string paperColumns = HasPaperPrintingMetadata
                ? "v.games,v.digital" : "'missing' AS games, 1 AS digital";
            string artColumn = HasPrintingArtwork ? "v.image" : "''";
            string roleColumns = HasRoleMetadata ? "c.type,c.oracle" : "'',''";
            cmd.CommandText = "SELECT v.print_id,v.name,v.setcode,v.collector," +
                paperColumns + "," + artColumn + "," + roleColumns +
                "\nFROM variants v\n" +
                (needsCard
                    ? "INNER JOIN cards c ON c.name = v.name COLLATE NOCASE\n"
                    : "");

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
            // Name-owned Arena collection entries may only be represented by
            // Arena-available printings; paper-exclusive art is never eligible.
            if (filters.ArenaOnly ||
                filters.Ownership is OwnershipMode.ArenaOwned or OwnershipMode.ArenaNotOwned)
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

            // Arena owns CARD NAMES, not every printing of a card. A single
            // representative image is shown in the catalog; the separately
            // scoped printing gallery can still browse all Arena styles.
            // Digital-specific images take priority over shared paper/Arena
            // releases. Apply the result cap AFTER unique names are selected.
            bool arenaScope = filters.ArenaOnly ||
                filters.Ownership is OwnershipMode.ArenaOwned or OwnershipMode.ArenaNotOwned;
            cmd.CommandText += "\nWHERE " + string.Join(" AND ", where) +
                (arenaScope
                    ? "\nORDER BY v.name COLLATE NOCASE," +
                      (HasPaperPrintingMetadata ? "v.digital DESC," : "") +
                      "v.setcode,v.collector"
                    : "\nORDER BY v.name COLLATE NOCASE,v.setcode,v.collector");
            if (!arenaScope && filters.Ownership == OwnershipMode.All &&
                filters.Role.Length == 0)
            {
                cmd.CommandText += " LIMIT $limit";
                cmd.Parameters.AddWithValue("$limit", limit + 1);
            }
            var encounteredArenaNames = arenaScope
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) : null;
            var rows = new List<PrintingRow>(limit);
            bool hasExtra = false;
            var started = Stopwatch.GetTimestamp();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                // Skip nonmatching card names before allocating a printing row
                // or resolving ownership. Each name's role tags are computed
                // at most once, even across multiple filter selections.
                if (filters.Role.Length > 0 &&
                    !RoleLabels(reader).Split(" · ",
                        StringSplitOptions.RemoveEmptyEntries)
                    .Contains(filters.Role, StringComparer.OrdinalIgnoreCase))
                    continue;
                var row = ReadRow(reader, ownership, HasRoleMetadata, arenaScope);
                bool matches = filters.Ownership switch
                {
                    OwnershipMode.PhysicalVerified => row.PhysicalOwned > 0,
                    OwnershipMode.PhysicalNotVerified => row.PhysicalOwned == 0,
                    OwnershipMode.ArenaOwned => row.ArenaOwned > 0,
                    OwnershipMode.ArenaNotOwned => row.ArenaOwned == 0,
                    _ => true
                };
                if (!matches) continue;
                if (encounteredArenaNames is not null &&
                    !encounteredArenaNames.Add(row.Name)) continue;
                if (rows.Count >= limit) { hasExtra = true; break; }
                rows.Add(row);
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
