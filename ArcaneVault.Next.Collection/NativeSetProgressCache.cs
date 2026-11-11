using ArcaneVault.Next.Domain;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ArcaneVault.Next.Collection;

/// <summary>
/// Persistent, disposable Set Progress summaries; never an ownership source.
/// Lives under LocalAppData (not the versioned application installation).
/// Cache writes are best effort: any failure falls back to verified rebuild.
/// </summary>
public static class NativeSetProgressCache
{
    private const int Schema = 1;
    public static string DefaultDirectory =>
        Path.Combine(NativeWorkspace.RootDirectory, "cache", "set-progress");

    private sealed record Stamp(int Schema, string BackupSha, string CatalogFile,
        long CatalogLength, long CatalogModifiedUtcTicks, string Scope);
    private sealed record Stored(Stamp Stamp, NativeSetProgress.SetRow[] Sets,
        int CatalogPaperSlots, int ConfirmedOwnedSlots, long ConfirmedOwnedCopies);

    private static Stamp Identity(string backupSha, string catalogPath, string scope)
    {
        if (backupSha.Length != 64 || !backupSha.All(Uri.IsHexDigit))
            throw new InvalidDataException("Invalid native collection identity.");
        var info = new FileInfo(catalogPath);
        if (!info.Exists || info.Length == 0)
            throw new FileNotFoundException("Set Progress needs a complete local Scryfall catalog.");
        return new Stamp(Schema, backupSha.ToUpperInvariant(),
            Path.GetFullPath(catalogPath), info.Length,
            info.LastWriteTimeUtc.Ticks, scope);
    }

    /// <summary>
    /// A cache hit skips expensive variant aggregation and set artwork lookups.
    /// Identity changes when the authoritative backup or catalog file changes.
    /// </summary>
    public static NativeSetProgress.Result Physical(
        CollectionCatalogReview.Result verification, string catalogPath,
        string backupSha, string? cacheDirectory = null,
        CancellationToken cancellation = default) =>
        GetOrBuild("physical", backupSha, catalogPath, cacheDirectory,
            () => NativeSetProgress.Build(verification, catalogPath, cancellation),
            cancellation);

    public static NativeSetProgress.Result Arena(
        IReadOnlyList<NativeArenaRow> arenaCards, string catalogPath,
        string backupSha, string? cacheDirectory = null,
        CancellationToken cancellation = default) =>
        GetOrBuild("arena", backupSha, catalogPath, cacheDirectory,
            () => NativeSetProgress.BuildArena(arenaCards, catalogPath, cancellation),
            cancellation);

    private static NativeSetProgress.Result GetOrBuild(string scope,
        string sha, string catalogPath, string? directory,
        Func<NativeSetProgress.Result> build, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        // Identity calculation reads just filesystem metadata, not the large catalog.
        var stamp = Identity(sha, catalogPath, scope);
        string path = Path.Combine(directory ?? DefaultDirectory, scope + "-summary.json");
        try
        {
            if (File.Exists(path))
            {
                var bytes = File.ReadAllBytes(path);
                if (bytes.Length <= 8_000_000)
                {
                    var saved = JsonSerializer.Deserialize<Stored>(bytes);
                    if (saved is not null && saved.Stamp == stamp &&
                        saved.Sets is not null && Validate(saved))
                        return new NativeSetProgress.Result(saved.Sets,
                            saved.CatalogPaperSlots, saved.ConfirmedOwnedSlots,
                            saved.ConfirmedOwnedCopies);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or JsonException or NotSupportedException)
        {
            // Broken or inaccessible cache is disposable; verified data remains safe.
        }

        cancellation.ThrowIfCancellationRequested();
        var rebuilt = build();
        cancellation.ThrowIfCancellationRequested();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var saved = new Stored(stamp, rebuilt.Sets.ToArray(),
                rebuilt.CatalogPaperSlots, rebuilt.ConfirmedOwnedSlots,
                rebuilt.ConfirmedOwnedCopies);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(saved);
            if (bytes.Length <= 8_000_000)
            {
                string staged = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    File.WriteAllBytes(staged, bytes);
                    File.Move(staged, path, overwrite: true);
                }
                finally
                {
                    try { if (File.Exists(staged)) File.Delete(staged); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Cache unavailable? Return the correctly rebuilt read-only results.
        }
        return rebuilt;
    }

    private static bool Validate(Stored stored)
    {
        if (stored.CatalogPaperSlots < 0 || stored.ConfirmedOwnedSlots < 0 ||
            stored.ConfirmedOwnedCopies < 0 || stored.Sets.Length > 100_000)
            return false;
        if (stored.Sets.Any(x => x is null || x.TotalSlots <= 0 ||
            x.OwnedSlots < 0 || x.OwnedSlots > x.TotalSlots ||
            x.VerifiedCopies < 0 || string.IsNullOrWhiteSpace(x.SetCode) ||
            x.NameCoverage != (stored.Stamp.Scope == "arena")))
            return false;
        return true;
    }
}
