using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace ArcaneVault.Next.Collection;

/// <summary>
/// Disposable deck-cover metadata cache, keyed by the verified workspace SHA
/// and native catalog file identity. It never stores or edits ownership.
/// Artwork bytes remain in the existing bounded NativeCardArtwork cache.
/// </summary>
public static class NativeDeckCoverCache
{
    private const int Schema = 1;
    public static string DefaultDirectory =>
        Path.Combine(NativeWorkspace.RootDirectory, "cache", "deck-covers");

    private sealed record Stamp(int Schema, string BackupSha,
        string CatalogPath, long CatalogLength, long CatalogModifiedUtcTicks);
    private sealed record Saved(Stamp Stamp, Dictionary<string, string> Covers);

    private static Stamp Identify(string sha, string catalogPath)
    {
        if (sha.Length != 64 || !sha.All(Uri.IsHexDigit))
            throw new InvalidDataException("Invalid native collection identity.");
        var info = new FileInfo(catalogPath);
        if (!info.Exists || info.Length == 0)
            throw new FileNotFoundException("A local Scryfall catalog is required.");
        return new Stamp(Schema, sha.ToUpperInvariant(),
            Path.GetFullPath(catalogPath), info.Length,
            info.LastWriteTimeUtc.Ticks);
    }

    /// <summary>
    /// Returns one illustrative official card artwork URL per exact commander
    /// name. The map can be rebuilt if missing/corrupt. Generic card art is not
    /// claimed to be an owned physical printing or an Arena cosmetic style.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Get(
        IEnumerable<string> commanders, string catalogPath, string backupSha,
        string? cacheDirectory = null, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(commanders);
        var requested = commanders.Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (requested.Length > 10_000)
            throw new InvalidDataException("Deck cover metadata list is too large.");
        cancellation.ThrowIfCancellationRequested();
        var stamp = Identify(backupSha, catalogPath);
        string file = Path.Combine(cacheDirectory ?? DefaultDirectory,
            "commander-covers.json");
        Dictionary<string, string>? cached = null;
        try
        {
            if (File.Exists(file) && new FileInfo(file).Length <= 4_000_000)
            {
                var saved = JsonSerializer.Deserialize<Saved>(File.ReadAllBytes(file));
                if (saved is not null && saved.Stamp == stamp &&
                    saved.Covers is not null && saved.Covers.Count <= 10_000 &&
                    saved.Covers.All(x => x.Key.Length is > 0 and <= 255 &&
                       x.Value is not null && x.Value.Length <= 2048 &&
                       (x.Value.Length == 0 || Trusted(x.Value))))
                    cached = new Dictionary<string, string>(saved.Covers,
                        StringComparer.OrdinalIgnoreCase);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            JsonException or NotSupportedException or ArgumentException)
        {
            // A cache is never authoritative; querying SQLite is safe.
        }

        if (cached is not null && requested.All(cached.ContainsKey))
            return requested.ToDictionary(x => x, x => cached[x],
                StringComparer.OrdinalIgnoreCase);

        var covers = cached ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using (var db = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = catalogPath, Mode = SqliteOpenMode.ReadOnly, Pooling = false
        }.ToString()))
        {
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT image FROM cards WHERE name=$name COLLATE NOCASE LIMIT 1";
            var name = cmd.Parameters.AddWithValue("$name", "");
            foreach (string commander in requested)
            {
                cancellation.ThrowIfCancellationRequested();
                if (covers.ContainsKey(commander)) continue;
                name.Value = commander;
                var image = cmd.ExecuteScalar()?.ToString() ?? "";
                covers[commander] = Trusted(image) ? image : "";
            }
        }
        cancellation.ThrowIfCancellationRequested();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            byte[] data = JsonSerializer.SerializeToUtf8Bytes(new Saved(stamp, covers));
            if (data.Length <= 4_000_000)
            {
                string stage = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    File.WriteAllBytes(stage, data);
                    File.Move(stage, file, overwrite: true);
                }
                finally
                {
                    try { if (File.Exists(stage)) File.Delete(stage); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Return validated catalog results even if cache persistence fails.
        }
        return requested.ToDictionary(x => x, x => covers[x],
            StringComparer.OrdinalIgnoreCase);
    }

    private static bool Trusted(string? raw) =>
        Uri.TryCreate(raw, UriKind.Absolute, out var url) &&
        url.Scheme == Uri.UriSchemeHttps &&
        url.Host.Equals("cards.scryfall.io", StringComparison.OrdinalIgnoreCase) &&
        url.Port == 443 && url.UserInfo.Length == 0 &&
        url.AbsolutePath.Length < 2048;
}
