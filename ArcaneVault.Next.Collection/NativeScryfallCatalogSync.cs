using Microsoft.Data.Sqlite;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ArcaneVault.Next.Collection;

/// <summary>
/// Independent native Scryfall bulk catalog (not the owned collection).
/// Downloads one official bulk file, streams it into a brand-new SQLite
/// database and publishes only after full import + integrity checks.
/// A refresh retains the previous complete catalog as a recovery file.
/// </summary>
public static class NativeScryfallCatalogSync
{
    public const string ManifestUrl = "https://api.scryfall.com/bulk-data";
    public const int ProductionMinimumPrintings = 10_000;
    private const long MaxDownloadBytes = 2_000_000_000L;
    private static readonly SemaphoreSlim ProcessGate = new(1, 1);

    public sealed record Progress(string Phase, long Completed, long? Total)
    {
        public string Message => Total.HasValue && Total > 0
            ? Phase + " — " + Completed.ToString("N0") + " / " + Total.Value.ToString("N0")
            : Phase + " — " + Completed.ToString("N0");
    }

    public sealed record Result(bool Updated, long Printings, long CardNames,
        string BulkUpdatedAt, string? RecoveryPath);

    public sealed record Dataset(Uri DownloadUrl, string UpdatedAt);

    private static void Check(bool value, string reason)
    {
        if (!value) throw new InvalidDataException(reason);
    }

    private static SqliteConnection Connect(string path, SqliteOpenMode mode)
    {
        var db = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path, Mode = mode, Pooling = false
        }.ToString());
        db.Open();
        return db;
    }

    private static string NewSibling(string target, string suffix) =>
        target + "." + Guid.NewGuid().ToString("N") + "." + suffix;

    private static string BackupPath(string target) =>
        Path.Combine(Path.GetDirectoryName(target)!,
            Path.GetFileNameWithoutExtension(target) +
            ".catalog-rollback-" + DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss") +
            "-" + Guid.NewGuid().ToString("N")[..8] + ".sqlite");

    private static void EraseScratch(string path)
    {
        foreach (var suffix in new[] { "", "-journal", "-wal", "-shm" })
            try { File.Delete(path + suffix); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
    }

    private static void CheckTarget(string targetPath)
    {
        var target = Path.GetFullPath(targetPath);
        Check(target.EndsWith(".sqlite", StringComparison.OrdinalIgnoreCase),
            "Catalog destination must end with .sqlite.");
        var legacy = Path.GetFullPath(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ArcaneVault")).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        Check(!target.StartsWith(legacy, StringComparison.OrdinalIgnoreCase),
            "Never replace a catalog in the installed legacy Arcane Vault profile.");
        Check(Directory.Exists(Path.GetDirectoryName(target)!),
            "Native catalog directory must exist.");
    }

    public static Dataset ParseManifest(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("data", out var datasets) ||
            datasets.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Scryfall bulk manifest has no dataset list.");
        foreach (var candidate in datasets.EnumerateArray())
        {
            if (candidate.ValueKind != JsonValueKind.Object ||
                !candidate.TryGetProperty("type", out var type) ||
                type.ValueKind != JsonValueKind.String ||
                type.GetString() != "default_cards") continue;

            string? stamp = candidate.TryGetProperty("updated_at", out var updated)
                && updated.ValueKind == JsonValueKind.String
                ? updated.GetString() : null;
            Check(!string.IsNullOrWhiteSpace(stamp) &&
                DateTimeOffset.TryParse(stamp, out _),
                "Scryfall default_cards dataset has no valid update date.");

            string? url = candidate.TryGetProperty("jsonl_download_uri", out var jsonl)
                && jsonl.ValueKind == JsonValueKind.String ? jsonl.GetString() : null;
            if (string.IsNullOrWhiteSpace(url) &&
                candidate.TryGetProperty("download_uri", out var legacy) &&
                legacy.ValueKind == JsonValueKind.String)
                url = legacy.GetString();

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                uri.Scheme != Uri.UriSchemeHttps ||
                uri.Port != 443 ||
                !(uri.Host.Equals("data.scryfall.io", StringComparison.OrdinalIgnoreCase) ||
                  uri.Host.Equals("data.scryfall.com", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("Unexpected Scryfall bulk download host.");
            return new Dataset(uri, stamp!);
        }
        throw new InvalidDataException(
            "Scryfall bulk manifest did not list the English-first default_cards dataset.");
    }

    public static (long Printings, long CardNames, string UpdatedAt) Verify(string path,
        int minimumPrintings = ProductionMinimumPrintings)
    {
        Check(File.Exists(path), "Native Scryfall catalog is missing.");
        using var db = Connect(path, SqliteOpenMode.ReadOnly);
        using var cmd = db.CreateCommand();
        cmd.CommandText = "PRAGMA quick_check";
        Check(string.Equals(cmd.ExecuteScalar()?.ToString(), "ok", StringComparison.Ordinal),
            "Scryfall catalog failed its SQLite integrity check.");
        cmd.CommandText = """
            SELECT COUNT(*) FROM sqlite_master WHERE type='table'
              AND name IN ('cards','variants','state')
            """;
        Check(Convert.ToInt64(cmd.ExecuteScalar()) == 3,
            "Scryfall catalog is missing a required table.");
        cmd.CommandText = "SELECT COUNT(*) FROM variants";
        long prints = Convert.ToInt64(cmd.ExecuteScalar());
        cmd.CommandText = "SELECT COUNT(*) FROM cards";
        long names = Convert.ToInt64(cmd.ExecuteScalar());
        Check(prints >= minimumPrintings &&
              names >= Math.Max(1, minimumPrintings / 4),
            "Scryfall catalog is incomplete; previous catalog was preserved.");
        cmd.CommandText = "SELECT value FROM state WHERE key='bulk_updated_utc'";
        string stamp = cmd.ExecuteScalar()?.ToString() ?? "";
        Check(DateTimeOffset.TryParse(stamp, out _),
            "Scryfall catalog is missing its source timestamp.");
        return (prints, names, stamp);
    }

    private static SqliteCommand Prepare(SqliteConnection db, SqliteTransaction tx,
        string sql, params string[] names)
    {
        var cmd = db.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        foreach (string name in names)
            cmd.Parameters.AddWithValue(name, "");
        return cmd;
    }

    private static string Text(JsonElement element, string key)
        => element.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? "" : "";

    private static bool Flag(JsonElement element, string key) =>
        element.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.True;

    private static string TextList(JsonElement element, string key, string join)
        => element.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Array
            ? string.Join(join, value.EnumerateArray()
                .Where(v => v.ValueKind == JsonValueKind.String)
                .Select(v => v.GetString()))
            : "";

    private static void Set(SqliteCommand command, string name, object value) =>
        command.Parameters[name].Value = value;

    /// <summary>
    /// Older standalone catalogs remain valid for browsing; only new bulk
    /// imports carry spell mana costs. Use the schema check to force a new
    /// bulk snapshot even when its Scryfall source timestamp is unchanged.
    /// </summary>
    public static bool HasManaCostColumn(string catalogPath)
    {
        if (!File.Exists(catalogPath)) return false;
        using var db = Connect(catalogPath, SqliteOpenMode.ReadOnly);
        using var cmd = db.CreateCommand();
        cmd.CommandText = "PRAGMA table_info(cards)";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            if (reader.GetString(1).Equals("mana_cost",
                StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    private static void Record(JsonElement element, SqliteCommand card, SqliteCommand print,
        ref long inserted)
    {
        if (element.ValueKind != JsonValueKind.Object) return;
        string name = Text(element, "name");
        string id = Text(element, "id");
        if (name.Length is < 1 or > 300 ||
            !Guid.TryParse(id, out var guid) || guid == Guid.Empty)
            return;
        string set = Text(element, "set");
        string collector = Text(element, "collector_number");
        if (set.Length == 0 || collector.Length == 0)
            return;

        string games = TextList(element, "games", ",");
        bool arena = games.Split(',').Contains("arena", StringComparer.OrdinalIgnoreCase);
        string illustration = Text(element, "illustration_id");
        string oracle = Text(element, "oracle_text");
        string image = "", back = "";
        if (element.TryGetProperty("image_uris", out var images) &&
            images.ValueKind == JsonValueKind.Object)
            image = Text(images, "normal");

        if (element.TryGetProperty("card_faces", out var faces) &&
            faces.ValueKind == JsonValueKind.Array)
        {
            if (oracle.Length == 0)
                oracle = string.Join("\n//\n", faces.EnumerateArray()
                    .Where(f => f.ValueKind == JsonValueKind.Object)
                    .Select(f => Text(f, "oracle_text")));
            foreach (var face in faces.EnumerateArray())
            {
                if (face.ValueKind != JsonValueKind.Object) continue;
                if (face.TryGetProperty("image_uris", out var uri) &&
                    uri.ValueKind == JsonValueKind.Object)
                {
                    string faceImage = Text(uri, "normal");
                    if (image.Length == 0) image = faceImage;
                    else if (back.Length == 0 && faceImage.Length > 0 && faceImage != image)
                        back = faceImage;
                }
                if (illustration.Length == 0)
                    illustration = Text(face, "illustration_id");
            }
        }

        // Prefer the card-level cost; for multiface cards that store costs
        // only on faces, use the FRONT face as an explicit approximation.
        // Never concatenate both sides: those are alternate casts, not
        // simultaneous mana requirements.
        string manaCost = Text(element, "mana_cost");
        if (manaCost.Length == 0 &&
            element.TryGetProperty("card_faces", out var costFaces) &&
            costFaces.ValueKind == JsonValueKind.Array)
        {
            foreach (var face in costFaces.EnumerateArray())
            {
                if (face.ValueKind != JsonValueKind.Object) continue;
                manaCost = Text(face, "mana_cost");
                break;
            }
        }
        if (manaCost.Length > 512) manaCost = "";
        double mv = element.TryGetProperty("cmc", out var cmc) &&
            cmc.ValueKind == JsonValueKind.Number && cmc.TryGetDouble(out var number)
            ? number : 0;
        string rarity = Text(element, "rarity");
        string url = Text(element, "scryfall_uri");
        string colors = TextList(element, "color_identity", "");
        string legalities = element.TryGetProperty("legalities", out var leg)
            ? leg.GetRawText() : "{}";

        Set(card, "$n", name);
        Set(card, "$t", Text(element, "type_line"));
        Set(card, "$o", oracle);
        Set(card, "$r", rarity);
        Set(card, "$mv", mv);
        Set(card, "$mc", manaCost);
        Set(card, "$c", colors);
        Set(card, "$i", image);
        Set(card, "$u", url);
        Set(card, "$l", legalities);
        Set(card, "$ly", Text(element, "layout"));
        Set(card, "$s", set);
        Set(card, "$a", arena ? 1 : 0);
        card.ExecuteNonQuery();

        Set(print, "$id", guid.ToString("D"));
        Set(print, "$n", name);
        Set(print, "$s", set);
        Set(print, "$sn", Text(element, "set_name"));
        Set(print, "$cn", collector);
        Set(print, "$r", rarity);
        Set(print, "$i", image);
        Set(print, "$ib", back);
        Set(print, "$ai", illustration);
        Set(print, "$g", games);
        Set(print, "$a", arena ? 1 : 0);
        Set(print, "$d", Flag(element, "digital") ? 1 : 0);
        Set(print, "$p", Flag(element, "promo") ? 1 : 0);
        Set(print, "$f", Flag(element, "full_art") ? 1 : 0);
        Set(print, "$b", Text(element, "border_color"));
        Set(print, "$rel", Text(element, "released_at"));
        Set(print, "$u", url);
        // Scryfall daily bulk USD prices are snapshot estimates, not live quotes.
        // Store raw decimal strings to avoid binary floating-point rounding.
        string usd = "", foil = "", etched = "";
        if (element.TryGetProperty("prices", out var prices) &&
            prices.ValueKind == JsonValueKind.Object)
        {
            usd = Text(prices, "usd");
            foil = Text(prices, "usd_foil");
            etched = Text(prices, "usd_etched");
        }
        Set(print, "$usd", usd);
        Set(print, "$foil", foil);
        Set(print, "$etched", etched);
        print.ExecuteNonQuery();
        inserted++;
    }

    private sealed class PrefixStream(byte prefix, Stream inner) : Stream
    {
        private bool pending = true;
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (count == 0) return 0;
            if (pending) { pending = false; buffer[offset] = prefix; return 1; }
            return inner.Read(buffer, offset, count);
        }
        public override int Read(Span<byte> buffer)
        {
            if (buffer.Length == 0) return 0;
            if (pending) { pending = false; buffer[0] = prefix; return 1; }
            return inner.Read(buffer);
        }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (buffer.Length > 0 && pending)
            {
                pending = false;
                buffer.Span[0] = prefix;
                return ValueTask.FromResult(1);
            }
            return inner.ReadAsync(buffer, cancellationToken);
        }
        public override bool CanRead => true;
        public override bool CanWrite => false;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException();
            set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
    }

    /// <summary>
    /// Input supports Scryfall's gzipped JSONL and older JSON array; never
    /// reads a full bulk dataset into process memory.
    /// </summary>
    private static async Task<long> ReadBulkAsync(string file,
        SqliteCommand card, SqliteCommand printing,
        IProgress<Progress>? progress, CancellationToken cancellation)
    {
        await using var raw = File.OpenRead(file);
        int b0 = raw.ReadByte(), b1 = raw.ReadByte();
        raw.Position = 0;
        await using Stream decoded = b0 == 0x1f && b1 == 0x8b
            ? new GZipStream(raw, CompressionMode.Decompress)
            : raw;
        using var buffered = new BufferedStream(decoded, 64 * 1024);
        int first;
        do { first = buffered.ReadByte(); }
        while (first >= 0 && char.IsWhiteSpace((char)first));
        Check(first is '[' or '{', "Scryfall bulk data is empty or not JSON/JSONL.");

        using var input = new PrefixStream((byte)first, buffered);
        long inserted = 0;
        if (first == '[')
        {
            await foreach (var item in JsonSerializer.DeserializeAsyncEnumerable<JsonElement>(
                input, cancellationToken: cancellation))
            {
                cancellation.ThrowIfCancellationRequested();
                Record(item, card, printing, ref inserted);
                if (inserted % 10_000 == 0 && inserted != 0)
                    progress?.Report(new Progress("Indexing Scryfall printings", inserted, null));
            }
        }
        else
        {
            using var reader = new StreamReader(input, Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false, 64 * 1024, leaveOpen: true);
            while (await reader.ReadLineAsync(cancellation) is string line)
            {
                cancellation.ThrowIfCancellationRequested();
                if (line.Length == 0) continue;
                Check(line.Length <= 8_000_000, "Scryfall record is larger than expected.");
                using var doc = JsonDocument.Parse(line);
                Record(doc.RootElement, card, printing, ref inserted);
                if (inserted % 10_000 == 0 && inserted != 0)
                    progress?.Report(new Progress("Indexing Scryfall printings", inserted, null));
            }
        }
        return inserted;
    }

    private static async Task<Result> BuildAndPublishAsync(string bulkFile,
        string targetPath, string updatedAt, int minimumPrintings,
        IProgress<Progress>? progress, CancellationToken cancellation)
    {
        string target = Path.GetFullPath(targetPath);
        CheckTarget(target);
        Check(DateTimeOffset.TryParse(updatedAt, out _),
            "Scryfall dataset timestamp is invalid.");
        string stage = NewSibling(target, "stage.sqlite");
        string? rollback = null;
        try
        {
            progress?.Report(new Progress("Preparing new native catalog", 0, null));
            using (var db = Connect(stage, SqliteOpenMode.ReadWriteCreate))
            {
                using (var ddl = db.CreateCommand())
                {
                    ddl.CommandText = """
                        PRAGMA journal_mode=DELETE;
                        PRAGMA synchronous=FULL;
                        CREATE TABLE cards(
                          name TEXT PRIMARY KEY COLLATE NOCASE,
                          type TEXT NOT NULL,oracle TEXT NOT NULL,rarity TEXT NOT NULL,
                          mv REAL NOT NULL,mana_cost TEXT NOT NULL DEFAULT '',
                          colors TEXT NOT NULL,image TEXT NOT NULL,
                          uri TEXT NOT NULL,legalities TEXT NOT NULL,layout TEXT NOT NULL,
                          setcode TEXT NOT NULL,arena INTEGER NOT NULL DEFAULT 0
                        );
                        CREATE TABLE variants(
                          print_id TEXT PRIMARY KEY,name TEXT NOT NULL COLLATE NOCASE,
                          setcode TEXT NOT NULL,setname TEXT NOT NULL,collector TEXT NOT NULL,
                          rarity TEXT NOT NULL,image TEXT NOT NULL,image_back TEXT NOT NULL,
                          art_id TEXT NOT NULL,games TEXT NOT NULL,arena INTEGER NOT NULL,
                          digital INTEGER NOT NULL,promo INTEGER NOT NULL,
                          full_art INTEGER NOT NULL,border TEXT NOT NULL,
                          release TEXT NOT NULL,url TEXT NOT NULL,
                          usd TEXT NOT NULL DEFAULT '',usd_foil TEXT NOT NULL DEFAULT '',
                          usd_etched TEXT NOT NULL DEFAULT ''
                        );
                        CREATE TABLE state(key TEXT PRIMARY KEY,value TEXT NOT NULL);
                        """;
                    ddl.ExecuteNonQuery();
                }

                using (var tx = db.BeginTransaction())
                {
                    using var card = Prepare(db, tx, """
                        INSERT INTO cards(name,type,oracle,rarity,mv,mana_cost,colors,image,uri,legalities,layout,setcode,arena)
                        VALUES($n,$t,$o,$r,$mv,$mc,$c,$i,$u,$l,$ly,$s,$a)
                        ON CONFLICT(name) DO UPDATE SET
                          arena=MAX(cards.arena,excluded.arena),
                          mana_cost=CASE
                            WHEN cards.mana_cost='' AND excluded.mana_cost<>''
                            THEN excluded.mana_cost ELSE cards.mana_cost END
                        """, "$n","$t","$o","$r","$mv","$mc","$c","$i","$u","$l","$ly","$s","$a");
                    using var art = Prepare(db, tx, """
                        INSERT INTO variants(print_id,name,setcode,setname,collector,rarity,
                            image,image_back,art_id,games,arena,digital,promo,full_art,border,release,url,
                            usd,usd_foil,usd_etched)
                        VALUES($id,$n,$s,$sn,$cn,$r,$i,$ib,$ai,$g,$a,$d,$p,$f,$b,$rel,$u,
                            $usd,$foil,$etched)
                        """, "$id","$n","$s","$sn","$cn","$r","$i","$ib","$ai","$g",
                             "$a","$d","$p","$f","$b","$rel","$u","$usd","$foil","$etched");
                    long count = await ReadBulkAsync(bulkFile, card, art, progress, cancellation);
                    Check(count >= minimumPrintings,
                        "Bulk data contains too few printings. Existing catalog preserved.");
                    using var state = db.CreateCommand();
                    state.Transaction = tx;
                    state.CommandText = "INSERT INTO state(key,value) VALUES('bulk_updated_utc',$stamp)";
                    state.Parameters.AddWithValue("$stamp", updatedAt);
                    state.ExecuteNonQuery();
                    tx.Commit();
                }

                progress?.Report(new Progress("Building catalog search indexes", 0, null));
                using (var index = db.CreateCommand())
                {
                    index.CommandText = """
                        CREATE INDEX idx_variants_name ON variants(name COLLATE NOCASE);
                        CREATE INDEX idx_variants_set_name ON variants(setcode,name);
                        CREATE INDEX idx_catalog_type ON cards(type);
                        CREATE INDEX idx_catalog_rarity ON cards(rarity);
                        """;
                    index.ExecuteNonQuery();
                }
            }

            cancellation.ThrowIfCancellationRequested();
            var verified = Verify(stage, minimumPrintings);
            progress?.Report(new Progress("Verifying catalog before installation",
                verified.Printings, verified.Printings));
            cancellation.ThrowIfCancellationRequested();
            if (File.Exists(target))
            {
                // Check the preexisting SQLite file before any replacement.
                // If corruption is detected, preserve it and fail closed.
                using (var db = Connect(target, SqliteOpenMode.ReadOnly))
                using (var cmd = db.CreateCommand())
                {
                    cmd.CommandText = "PRAGMA quick_check";
                    Check(cmd.ExecuteScalar()?.ToString() == "ok",
                        "Existing native catalog is damaged; it was not replaced.");
                }
                rollback = BackupPath(target);
                File.Replace(stage, target, rollback, ignoreMetadataErrors: false);
            }
            else
                File.Move(stage, target, overwrite: false);

            // If this fails, keep current and rollback; never delete recovery.
            var installed = Verify(target, minimumPrintings);
            Check(installed.Printings == verified.Printings &&
                  installed.CardNames == verified.CardNames &&
                  installed.UpdatedAt == updatedAt,
                "Catalog publish verification failed; preserve the new catalog and recovery copy.");

            return new Result(true, installed.Printings, installed.CardNames,
                updatedAt, rollback);
        }
        finally { EraseScratch(stage); }
    }

    /// <summary>
    /// Offline import hook for synthetic regression fixtures. The ordinary UI
    /// always passes the full production minimum via SyncAsync.
    /// </summary>
    public static async Task<Result> ImportBulkFileAsync(string bulkFile,
        string targetPath, string sourceUpdatedAt,
        int minimumPrintings = ProductionMinimumPrintings,
        IProgress<Progress>? progress = null,
        CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bulkFile);
        Check(minimumPrintings > 0, "Minimum printing count must be positive.");
        await ProcessGate.WaitAsync(cancellation);
        try
        {
            return await BuildAndPublishAsync(bulkFile, targetPath,
                sourceUpdatedAt, minimumPrintings, progress, cancellation);
        }
        finally { ProcessGate.Release(); }
    }

    /// <summary>
    /// Discover the current official Scryfall English-first printing dataset,
    /// skip if already current, or download and build it in private scratch
    /// files. No per-card API requests and no edits to user ownership.
    /// </summary>
    public static async Task<Result> SyncAsync(string targetPath,
        HttpClient? http = null, IProgress<Progress>? progress = null,
        CancellationToken cancellation = default,
        int minimumPrintings = ProductionMinimumPrintings)
    {
        CheckTarget(targetPath);
        Check(minimumPrintings > 0, "Minimum printing count must be positive.");
        await ProcessGate.WaitAsync(cancellation);
        string bulkFile = NewSibling(Path.GetFullPath(targetPath), "download");
        bool ownsClient = http is null;
        http ??= new HttpClient { Timeout = TimeSpan.FromHours(2) };
        try
        {
            progress?.Report(new Progress("Checking Scryfall's bulk catalog", 0, null));
            using var metadata = new HttpRequestMessage(HttpMethod.Get, ManifestUrl);
            metadata.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            metadata.Headers.UserAgent.ParseAdd(
                "ArcaneVaultNext/0.1 (+https://github.com/thejohnfavaro/arcanevault)");
            using var metaResponse = await http.SendAsync(metadata,
                HttpCompletionOption.ResponseHeadersRead, cancellation);
            metaResponse.EnsureSuccessStatusCode();
            await using var metaStream = await metaResponse.Content.ReadAsStreamAsync(cancellation);
            using var manifest = await JsonDocument.ParseAsync(metaStream, cancellationToken: cancellation);
            Dataset chosen = ParseManifest(manifest.RootElement);

            // A previous legacy copy has no native source timestamp, so it
            // does not count as a current native bulk import.
            if (File.Exists(targetPath))
            {
                try
                {
                    var current = Verify(targetPath, minimumPrintings);
                    if (current.UpdatedAt == chosen.UpdatedAt &&
                        current.Printings >= minimumPrintings &&
                        HasManaCostColumn(targetPath))
                        return new Result(false, current.Printings,
                            current.CardNames, current.UpdatedAt, null);
                }
                catch (InvalidDataException) { /* publish will still fail safely for corrupt existing DB */ }
            }

            progress?.Report(new Progress("Downloading the Scryfall default_cards bulk file", 0, null));
            using var downloadRequest = new HttpRequestMessage(HttpMethod.Get, chosen.DownloadUrl);
            downloadRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            downloadRequest.Headers.UserAgent.ParseAdd(
                "ArcaneVaultNext/0.1 (+https://github.com/thejohnfavaro/arcanevault)");
            using var response = await http.SendAsync(downloadRequest,
                HttpCompletionOption.ResponseHeadersRead, cancellation);
            response.EnsureSuccessStatusCode();
            Check(response.Content.Headers.ContentLength is null or <= MaxDownloadBytes,
                "Scryfall bulk data exceeds safe maximum download size.");
            long total = response.Content.Headers.ContentLength ?? 0;
            await using (var input = await response.Content.ReadAsStreamAsync(cancellation))
            await using (var output = new FileStream(bulkFile, FileMode.CreateNew,
                FileAccess.Write, FileShare.None, 128 * 1024, useAsync: true))
            {
                var buffer = new byte[128 * 1024];
                long bytes = 0;
                int read;
                while ((read = await input.ReadAsync(buffer, cancellation)) != 0)
                {
                    bytes = checked(bytes + read);
                    Check(bytes <= MaxDownloadBytes,
                        "Scryfall download exceeded the safe maximum.");
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellation);
                    if (bytes == read || bytes % (8 * 1024 * 1024) < read)
                        progress?.Report(new Progress("Downloading Scryfall bulk file",
                            bytes, total > 0 ? total : null));
                }
                await output.FlushAsync(cancellation);
                Check(bytes > 0, "Scryfall returned an empty bulk file.");
            }
            return await BuildAndPublishAsync(bulkFile, targetPath, chosen.UpdatedAt,
                minimumPrintings, progress, cancellation);
        }
        finally
        {
            EraseScratch(bulkFile);
            if (ownsClient) http.Dispose();
            ProcessGate.Release();
        }
    }
}
