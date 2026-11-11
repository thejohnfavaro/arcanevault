using ArcaneVault.Next.Collection;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// Artwork is a presentation-only, opt-in detail-window read; the catalog,
/// native ownership and backup never depend on a network image. Only official
/// Scryfall CDN URLs are fetched, with bounded size and disk cache.
/// </summary>
public static class NativeCardArtwork
{
    private const int MaxImageBytes = 8 * 1024 * 1024;
    private const long CacheLimitBytes = 160L * 1024 * 1024;
    private const long CacheTargetBytes = 120L * 1024 * 1024;

    private static readonly HttpClient Client = NewClient();

    private static HttpClient NewClient()
    {
        var http = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.None
        })
        { Timeout = TimeSpan.FromSeconds(15) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "ArcaneVaultNext/0.1 (+https://github.com/thejohnfavaro/arcanevault)");
        return http;
    }

    public static bool IsTrustedArtworkUrl(string? raw)
    {
        return Uri.TryCreate(raw, UriKind.Absolute, out var uri) &&
               uri.Scheme == Uri.UriSchemeHttps &&
               uri.Host.Equals("cards.scryfall.io", StringComparison.OrdinalIgnoreCase) &&
               uri.Port == 443 &&
               string.IsNullOrEmpty(uri.UserInfo);
    }

    private static string CachePath(string url)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url)));
        return Path.Combine(NativeWorkspace.RootDirectory,
            "artwork-cache", hash + ".img");
    }

    private static void LimitCache(string directory)
    {
        try
        {
            var files = new DirectoryInfo(directory).EnumerateFiles("*.img")
                .OrderBy(x => x.LastWriteTimeUtc).ToArray();
            long total = files.Sum(x => x.Length);
            if (total <= CacheLimitBytes) return;
            foreach (var file in files)
            {
                if (total <= CacheTargetBytes) break;
                try { long bytes = file.Length; file.Delete(); total -= bytes; }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
        catch (IOException) { /* An unavailable cache must never block card details. */ }
        catch (UnauthorizedAccessException) { }
    }

    public static async Task<byte[]?> LoadAsync(string? url,
        CancellationToken cancellation = default)
    {
        if (!IsTrustedArtworkUrl(url)) return null;
        cancellation.ThrowIfCancellationRequested();

        string path = CachePath(url!);
        try
        {
            if (File.Exists(path))
            {
                var length = new FileInfo(path).Length;
                if (length is > 0 and <= MaxImageBytes)
                    return await File.ReadAllBytesAsync(path, cancellation);
            }
        }
        catch (IOException) { /* Download can still succeed without a cache. */ }
        catch (UnauthorizedAccessException) { }

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/jpeg"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/png"));
        using var response = await Client.SendAsync(request,
            HttpCompletionOption.ResponseHeadersRead, cancellation);
        if (response.StatusCode != HttpStatusCode.OK) return null;

        string? media = response.Content.Headers.ContentType?.MediaType;
        if (media is not ("image/jpeg" or "image/png")) return null;
        if (response.Content.Headers.ContentLength is > MaxImageBytes) return null;

        await using var stream = await response.Content.ReadAsStreamAsync(cancellation);
        using var memory = new MemoryStream();
        byte[] buffer = new byte[64 * 1024];
        int read;
        while ((read = await stream.ReadAsync(buffer.AsMemory(), cancellation)) > 0)
        {
            if (memory.Length + read > MaxImageBytes) return null;
            await memory.WriteAsync(buffer.AsMemory(0, read), cancellation);
        }
        if (memory.Length == 0) return null;
        byte[] bytes = memory.ToArray();

        string stage = path + "." + Guid.NewGuid().ToString("N") + ".partial";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllBytesAsync(stage, bytes, cancellation);
            File.Move(stage, path, overwrite: false);
            LimitCache(Path.GetDirectoryName(path)!);
        }
        catch (IOException) { /* Duplicate window/cached read; still show downloaded bytes. */ }
        catch (UnauthorizedAccessException) { /* Offline preview remains usable. */ }
        finally
        {
            try { if (File.Exists(stage)) File.Delete(stage); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return bytes;
    }
}
