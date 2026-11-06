using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace ArcaneVault.Desktop;

// Explicitly opt-in cache. Only Scryfall's image delivery hosts are accepted.
internal sealed class ArtworkCache
{
    private const long MaxBytes = 200L * 1024 * 1024;
    private readonly string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ArcaneVault", "Artwork");
    private readonly HttpClient client;
    private readonly SemaphoreSlim gate = new(1, 1);

    public ArtworkCache(HttpClient client) { this.client = client; Directory.CreateDirectory(directory); }

    private static bool Valid(string url, out Uri? uri)
    {
        bool valid = Uri.TryCreate(url, UriKind.Absolute, out uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && (uri.Host.Equals("cards.scryfall.io", StringComparison.OrdinalIgnoreCase)
                || uri.Host.Equals("svgs.scryfall.io", StringComparison.OrdinalIgnoreCase));
        return valid;
    }
    private string FileFor(string url)
    {
        string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url)));
        return Path.Combine(directory, key + (url.Contains(".svg", StringComparison.OrdinalIgnoreCase) ? ".svg" : ".img"));
    }
    public object Status()
    {
        var files = new DirectoryInfo(directory).GetFiles();
        return new { count = files.Length, bytes = files.Sum(f => f.Length), limitBytes = MaxBytes };
    }
    public void Clear()
    {
        gate.Wait();
        try { foreach (var f in Directory.EnumerateFiles(directory)) try { File.Delete(f); } catch (IOException) { } }
        finally { gate.Release(); }
    }
    public async Task<(byte[] bytes, string type)> ReadAsync(string url, CancellationToken token)
    {
        if (!Valid(url, out Uri? uri)) throw new ArgumentException("Only official Scryfall artwork hosts are supported");
        string file = FileFor(url);
        string type = url.Contains(".svg", StringComparison.OrdinalIgnoreCase) ? "image/svg+xml" : url.Contains(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : url.Contains(".webp", StringComparison.OrdinalIgnoreCase) ? "image/webp" : "image/jpeg";
        await gate.WaitAsync(token);
        try
        {
            if (File.Exists(file)) { File.SetLastWriteTimeUtc(file, DateTime.UtcNow); return (await File.ReadAllBytesAsync(file, token), type); }
            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > 3_000_000) throw new IOException("Artwork exceeds the per-image size limit");
            using var input = await response.Content.ReadAsStreamAsync(token);
            using var output = new MemoryStream();
            var buf = new byte[8192]; int count;
            while ((count = await input.ReadAsync(buf, token)) > 0)
            { if (output.Length + count > 3_000_000) throw new IOException("Artwork exceeds the per-image size limit"); await output.WriteAsync(buf.AsMemory(0, count), token); }
            byte[] data = output.ToArray();
            var media = response.Content.Headers.ContentType?.MediaType;
            if (media is "image/png" or "image/jpeg" or "image/webp" or "image/svg+xml") type = media;
            else throw new IOException("Artwork server returned a non-image content type");
            var files = new DirectoryInfo(directory).GetFiles().OrderBy(f => f.LastWriteTimeUtc).ToList();
            long used = files.Sum(f => f.Length);
            foreach (var f in files) { if (used + data.Length <= MaxBytes) break; try { used -= f.Length; f.Delete(); } catch (IOException) { } }
            await File.WriteAllBytesAsync(file, data, token);
            return (data, type);
        }
        finally { gate.Release(); }
    }
}
