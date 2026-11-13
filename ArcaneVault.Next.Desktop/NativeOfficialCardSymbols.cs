using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows.Media;
using System.Xml;
using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// Scryfall symbology is a mapping from Oracle {tokens} to the original
/// official Magic symbol SVGs. No hand-drawn substitutes or font dependency.
/// Display only: unavailable symbols retain their literal Oracle token.
/// Cached assets can be reused offline after their first successful load.
/// </summary>
public static class NativeOfficialCardSymbols
{
    private const string SymbologyUri = "https://api.scryfall.com/symbology";
    private const int MaxSymbologyBytes = 1_000_000;
    private const int MaxSvgBytes = 100_000;
    private static readonly HttpClient Client = CreateHttp();
    private static readonly SemaphoreSlim DownloadGate = new(3);
    private static readonly ConcurrentDictionary<string, ImageSource> Images =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly Lazy<Task<IReadOnlyDictionary<string, string>>> SymbolUris =
        new(LoadOfficialUrisAsync);

    private static HttpClient CreateHttp()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("ArcaneVault", "0.9.62"));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private static string CacheDirectory
    {
        get
        {
            string root = Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(root, "ArcaneVault", "official-card-symbols");
        }
    }

    private static async Task<IReadOnlyDictionary<string, string>> LoadOfficialUrisAsync()
    {
        string cache = Path.Combine(CacheDirectory, "scryfall-symbology.json");
        try
        {
            // The cached official mapping is safe to use offline. It also
            // avoids a network request for every card-details window.
            if (File.Exists(cache))
                return NativeScryfallSymbology.ReadUris(await File.ReadAllTextAsync(cache));
        }
        catch (Exception) { /* Try a fresh official mapping. */ }

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, SymbologyUri);
            using var response = await Client.SendAsync(req,
                HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > MaxSymbologyBytes)
                return new Dictionary<string, string>();
            byte[] bytes = await response.Content.ReadAsByteArrayAsync();
            if (bytes.Length > MaxSymbologyBytes) return new Dictionary<string, string>();
            string json = Encoding.UTF8.GetString(bytes);
            var map = NativeScryfallSymbology.ReadUris(json);
            if (map.Count == 0) return map;
            try
            {
                Directory.CreateDirectory(CacheDirectory);
                await File.WriteAllTextAsync(cache, json);
            }
            catch (IOException) { /* Rendering can proceed without persistent cache. */ }
            catch (UnauthorizedAccessException) { }
            return map;
        }
        catch (Exception)
        {
            // No fake icon on a disconnected installation. Caller shows
            // the original token in braces, preserving all rules meaning.
            return new Dictionary<string, string>();
        }
    }

    public static async Task<ImageSource?> GetAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > 24) return null;
        string token = "{" + code.Trim().Trim('{', '}').ToUpperInvariant() + "}";
        if (Images.TryGetValue(token, out var hit)) return hit;
        var map = await SymbolUris.Value;
        if (!map.TryGetValue(token, out string? uri) || !NativeScryfallSymbology.IsTrustedSvgUri(uri))
            return null;

        await DownloadGate.WaitAsync();
        try
        {
            if (Images.TryGetValue(token, out hit)) return hit;
            string filename = Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(uri))) + ".svg";
            string file = Path.Combine(CacheDirectory, filename);
            if (!File.Exists(file))
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, uri);
                using var response = await Client.SendAsync(req,
                    HttpCompletionOption.ResponseHeadersRead);
                if (!response.IsSuccessStatusCode ||
                    response.RequestMessage?.RequestUri?.ToString() != uri ||
                    response.Content.Headers.ContentLength > MaxSvgBytes)
                    return null;
                byte[] bytes = await response.Content.ReadAsByteArrayAsync();
                if (bytes.Length is < 30 or > MaxSvgBytes) return null;
                using (var stream = new MemoryStream(bytes))
                using (var xml = XmlReader.Create(stream, new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    MaxCharactersInDocument = MaxSvgBytes
                }))
                {
                    xml.MoveToContent();
                    if (xml.LocalName != "svg") return null;
                }
                Directory.CreateDirectory(CacheDirectory);
                // Only official Scryfall CDN bytes are cached. Do not follow
                // arbitrary remote resources embedded in card metadata.
                string temp = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
                await File.WriteAllBytesAsync(temp, bytes);
                try { File.Move(temp, file, overwrite: true); }
                finally { if (File.Exists(temp)) File.Delete(temp); }
            }

            // SharpVectors converts the EXACT official SVG paths to a WPF
            // DrawingGroup; the original shapes and fills are unchanged.
            var settings = new WpfDrawingSettings { IncludeRuntime = false };
            var reader = new FileSvgReader(settings)
            {
                SaveXaml = false,
                SaveZaml = false
            };
            DrawingGroup? drawing = reader.Read(file);
            if (drawing is null) return null;
            drawing.Freeze();
            var image = new DrawingImage(drawing);
            image.Freeze();
            return Images.GetOrAdd(token, image);
        }
        catch (Exception)
        {
            return null;
        }
        finally { DownloadGate.Release(); }
    }
}
