using System.Text.Json;

namespace ArcaneVault.Next.Collection;

/// <summary>
/// Disposable native Deck Library display summaries. Only the verified
/// workspace backup SHA determines cache validity; never use cache content to
/// save, delete, edit or validate a deck. The source backup remains authoritative.
/// </summary>
public static class NativeDeckLibraryCache
{
    private const int Schema = 1;
    public static string DefaultDirectory =>
        Path.Combine(NativeWorkspace.RootDirectory, "cache", "deck-library");
    private sealed record Stored(int Schema, string BackupSha,
        NativeDeckEditing.DeckRow[] Rows);

    public static IReadOnlyList<NativeDeckEditing.DeckRow> List(
        string backupSha, Func<byte[]> readVerifiedBackup,
        string? cacheDirectory = null,
        CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupSha);
        ArgumentNullException.ThrowIfNull(readVerifiedBackup);
        if (backupSha.Length != 64 || !backupSha.All(Uri.IsHexDigit))
            throw new InvalidDataException("Invalid native collection backup identity.");
        cancellation.ThrowIfCancellationRequested();
        string sha = backupSha.ToUpperInvariant();
        string path = Path.Combine(cacheDirectory ?? DefaultDirectory, "deck-list.json");
        try
        {
            if (File.Exists(path) && new FileInfo(path).Length <= 8_000_000)
            {
                var saved = JsonSerializer.Deserialize<Stored>(File.ReadAllBytes(path));
                if (saved is not null && saved.Schema == Schema &&
                    saved.BackupSha == sha &&
                    saved.Rows is { Length: <= 10_000 } && Validate(saved.Rows))
                    return saved.Rows;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            JsonException or NotSupportedException)
        {
            // Corrupt/missing presentation cache is disposable; rebuild below.
        }

        cancellation.ThrowIfCancellationRequested();
        var entries = NativeDeckEditing.List(readVerifiedBackup()).ToArray();
        cancellation.ThrowIfCancellationRequested();
        try
        {
            byte[] json = JsonSerializer.SerializeToUtf8Bytes(
                new Stored(Schema, sha, entries));
            if (json.Length <= 8_000_000)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                string stage = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    File.WriteAllBytes(stage, json);
                    File.Move(stage, path, overwrite: true);
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
            // Never fail opening the real library because of a cache error.
        }
        return entries;
    }

    private static bool Validate(NativeDeckEditing.DeckRow[] rows)
    {
        if (rows.Any(x => x is null || x.Ordinal < 0 ||
            string.IsNullOrWhiteSpace(x.Name) || x.Name.Length > 255 ||
            x.EntryCount is < 0 or > 10_000 ||
            x.TotalCopies is < 0 or > 1_000 ||
            x.Revisions < 0 ||
            string.IsNullOrWhiteSpace(x.SourceSha256) ||
            x.SourceSha256.Length != 64 ||
            !x.SourceSha256.All(Uri.IsHexDigit) ||
            (x.MainboardText?.Length ?? 64_001) > 64_000 ||
            x.CoverArtworkUrl is null || x.CoverArtworkUrl.Length != 0))
            return false;
        return rows.Select(x => x.Ordinal).Distinct().Count() == rows.Length;
    }
}
