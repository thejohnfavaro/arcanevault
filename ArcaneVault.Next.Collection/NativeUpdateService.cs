using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ArcaneVault.Next.Collection;

/// <summary>
/// Explicit, versioned, side-by-side native Windows upgrades. Binary files
/// live outside the native user-data directory. The existing executable and
/// all native/legacy collections are never modified by an update install.
/// </summary>
public static class NativeUpdateService
{
    public const string ZipName = "ArcaneVaultNative-win-x64.zip";
    public const string ChecksumName = ZipName + ".sha256";
    public const string ExecutableName = "ArcaneVault.Next.Desktop.exe";
    public const string ManifestName = "native-version.json";
    private const long MaxArchiveBytes = 300L * 1024 * 1024;
    private const long MaxUnpackedBytes = 800L * 1024 * 1024;
    private const int MaxFiles = 1000;

    public static string DefaultVersionsRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ArcaneVaultNativeApps", "versions");

    public sealed record Candidate(Version Version, string Tag, Uri ReleasePage,
        Uri ArchiveUrl, Uri ChecksumUrl);

    public sealed record Installed(Version Version, string ExecutablePath, string Directory,
        string ArchiveSha256);

    private static bool TryVersion(string tag, out Version version)
    {
        version = new Version(0, 0, 0);
        if (!Regex.IsMatch(tag, @"^native-v[0-9]+\.[0-9]+\.[0-9]+$",
                RegexOptions.CultureInvariant))
            return false;
        return Version.TryParse(tag["native-v".Length..], out version!);
    }

    private static bool TrustedPage(Uri url, string tag) =>
        url.Scheme == Uri.UriSchemeHttps && url.Port == 443 &&
        url.UserInfo.Length == 0 &&
        url.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) &&
        url.AbsolutePath.Equals(
            "/thejohnfavaro/arcanevault/releases/tag/" + tag, StringComparison.Ordinal);

    private static bool TrustedAsset(Uri url, string tag, string filename) =>
        url.Scheme == Uri.UriSchemeHttps && url.Port == 443 &&
        url.UserInfo.Length == 0 &&
        url.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) &&
        url.AbsolutePath.Equals(
            "/thejohnfavaro/arcanevault/releases/download/" + tag + "/" + filename,
            StringComparison.Ordinal);

    /// <summary>Parse only official versioned native preview releases with both expected assets.</summary>
    public static Candidate? SelectLatest(string releasesJson, Version installed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(releasesJson);
        ArgumentNullException.ThrowIfNull(installed);
        using var json = JsonDocument.Parse(releasesJson);
        if (json.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("GitHub release response is not an array.");
        Candidate? newest = null;
        foreach (var release in json.RootElement.EnumerateArray())
        {
            if (release.ValueKind != JsonValueKind.Object ||
                !release.TryGetProperty("tag_name", out var tagValue) ||
                tagValue.ValueKind != JsonValueKind.String ||
                !TryVersion(tagValue.GetString() ?? "", out var version) ||
                version <= installed) continue;
            string tag = tagValue.GetString()!;
            if (release.TryGetProperty("draft", out var draft) &&
                draft.ValueKind == JsonValueKind.True) continue;
            // This pipeline intentionally ships preview builds, not final 1.0.
            if (!release.TryGetProperty("prerelease", out var prerelease) ||
                prerelease.ValueKind != JsonValueKind.True) continue;
            if (!release.TryGetProperty("html_url", out var html) ||
                !Uri.TryCreate(html.GetString(), UriKind.Absolute, out var page) ||
                !TrustedPage(page, tag)) continue;
            if (!release.TryGetProperty("assets", out var assets) ||
                assets.ValueKind != JsonValueKind.Array) continue;

            Uri? archive = null, checksum = null;
            foreach (var asset in assets.EnumerateArray())
            {
                if (asset.ValueKind != JsonValueKind.Object ||
                    !asset.TryGetProperty("name", out var n) ||
                    n.ValueKind != JsonValueKind.String ||
                    !asset.TryGetProperty("browser_download_url", out var u) ||
                    u.ValueKind != JsonValueKind.String ||
                    !Uri.TryCreate(u.GetString(), UriKind.Absolute, out var url)) continue;

                string? name = n.GetString();
                if (name == ZipName && TrustedAsset(url, tag, ZipName))
                    archive = url;
                else if (name == ChecksumName && TrustedAsset(url, tag, ChecksumName))
                    checksum = url;
            }
            if (archive is null || checksum is null) continue;
            if (newest is null || version > newest.Version)
                newest = new Candidate(version, tag, page, archive, checksum);
        }
        return newest;
    }

    private static bool TrustedRedirect(Uri url) =>
        url.Scheme == Uri.UriSchemeHttps && url.Port == 443 &&
        url.UserInfo.Length == 0 &&
        (url.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
         url.Host.Equals("release-assets.githubusercontent.com", StringComparison.OrdinalIgnoreCase));

    private static HttpClient Client() =>
        new(new HttpClientHandler { AllowAutoRedirect = false })
        {
            Timeout = TimeSpan.FromMinutes(10)
        };

    private static async Task<HttpResponseMessage> TrustedGetAsync(HttpClient http,
        Uri url, CancellationToken cancellation)
    {
        Uri current = url;
        for (int redirect = 0; redirect < 5; redirect++)
        {
            if (!TrustedRedirect(current) &&
                !current.Host.Equals("api.github.com", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Unexpected update download host.");
            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            request.Headers.UserAgent.ParseAdd("ArcaneVaultNative/0.9.0");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(
                current.Host.Equals("api.github.com", StringComparison.OrdinalIgnoreCase)
                    ? "application/vnd.github+json" : "application/octet-stream"));
            var response = await http.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead, cancellation);
            if (response.StatusCode is HttpStatusCode.Redirect or
                HttpStatusCode.MovedPermanently or
                HttpStatusCode.TemporaryRedirect or
                HttpStatusCode.PermanentRedirect)
            {
                var location = response.Headers.Location;
                response.Dispose();
                if (location is null)
                    throw new InvalidDataException("Update download redirect has no location.");
                Uri next = location.IsAbsoluteUri ? location : new Uri(current, location);
                if (!TrustedRedirect(next))
                    throw new InvalidDataException("Update download redirected to an untrusted host.");
                current = next;
                continue;
            }
            response.EnsureSuccessStatusCode();
            return response;
        }
        throw new InvalidDataException("Too many update download redirects.");
    }

    private static async Task<byte[]> ReadBoundedAsync(Stream stream, long max,
        CancellationToken cancellation)
    {
        using var memory = new MemoryStream();
        byte[] buffer = new byte[65536];
        long count = 0;
        while (true)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(), cancellation);
            if (read == 0) break;
            count = checked(count + read);
            if (count > max) throw new InvalidDataException("Update response exceeds its size limit.");
            memory.Write(buffer, 0, read);
        }
        return memory.ToArray();
    }

    public static async Task<Candidate?> FindUpdateAsync(Version current,
        CancellationToken cancellation = default)
    {
        using var http = Client();
        using var response = await TrustedGetAsync(http, new Uri(
            "https://api.github.com/repos/thejohnfavaro/arcanevault/releases?per_page=30"),
            cancellation);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellation);
        var data = await ReadBoundedAsync(stream, 2_000_000, cancellation);
        return SelectLatest(Encoding.UTF8.GetString(data), current);
    }

    public static string ParseChecksum(string input)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        var match = Regex.Match(input.Trim(), @"^([a-fA-F0-9]{64})(?:\s+\*?ArcaneVaultNative-win-x64\.zip)?$",
            RegexOptions.CultureInvariant);
        if (!match.Success)
            throw new InvalidDataException("Native release checksum file has an invalid format.");
        return match.Groups[1].Value.ToLowerInvariant();
    }

    private static string Digest(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string RelativeZipPath(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName) || fullName.StartsWith('/') ||
            fullName.StartsWith('\\') || fullName.Contains(':'))
            throw new InvalidDataException("Unsafe native update archive entry.");
        string normalized = fullName.Replace('\\', '/');
        string[] segments = normalized.TrimEnd('/').Split('/');
        if (segments.Any(s => s.Length == 0 || s is "." or ".."))
            throw new InvalidDataException("Native update ZIP contains path traversal.");
        return Path.Combine(segments);
    }

    private static void VerifyManifest(string stage, Version expected)
    {
        string file = Path.Combine(stage, ManifestName);
        if (!File.Exists(file) || new FileInfo(file).Length > 4096)
            throw new InvalidDataException("Native update version manifest is missing or oversized.");
        using var manifest = JsonDocument.Parse(File.ReadAllBytes(file));
        var root = manifest.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("version", out var v) ||
            v.ValueKind != JsonValueKind.String ||
            !Version.TryParse(v.GetString(), out var found) ||
            found != expected ||
            !root.TryGetProperty("sourceSha", out var sourceSha) ||
            sourceSha.ValueKind != JsonValueKind.String ||
            !Regex.IsMatch(sourceSha.GetString() ?? "", @"^[a-fA-F0-9]{40}$") ||
            !root.TryGetProperty("channel", out var channel) ||
            channel.GetString() != "preview")
            throw new InvalidDataException("Native update manifest identity does not match the release.");
    }

    /// <summary>
    /// Archive extraction is deliberately independent of collection storage:
    /// only newly created staging and version directories are ever written.
    /// No existing install is overwritten or deleted, even on failure.
    /// </summary>
    public static Installed InstallVerifiedArchive(string archivePath, string expectedSha256,
        Version expectedVersion, string versionsRoot,
        CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(versionsRoot);
        ArgumentNullException.ThrowIfNull(expectedVersion);
        if (!Regex.IsMatch(expectedSha256 ?? "", "^[a-fA-F0-9]{64}$"))
            throw new InvalidDataException("Update checksum is not a SHA-256 digest.");
        var source = new FileInfo(archivePath);
        if (!source.Exists || source.Length is < 1 or > MaxArchiveBytes)
            throw new InvalidDataException("Native update ZIP is missing or oversized.");
        string actual = Digest(archivePath);
        if (!actual.Equals(expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Native update SHA-256 did not match. Nothing was installed.");

        string root = Path.GetFullPath(versionsRoot);
        Directory.CreateDirectory(root);
        string target = Path.Combine(root, "v" + expectedVersion.ToString(3));
        if (Directory.Exists(target) || File.Exists(target))
            throw new InvalidDataException("That native version already exists; no files were replaced.");
        string stage = Path.Combine(root, ".stage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        try
        {
            using (var zip = ZipFile.OpenRead(archivePath))
            {
                if (zip.Entries.Count == 0 || zip.Entries.Count > MaxFiles)
                    throw new InvalidDataException("Native update ZIP has an unexpected file count.");
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                long expanded = 0;
                foreach (var entry in zip.Entries)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000)
                        throw new InvalidDataException("Native update ZIP may not contain symbolic links.");
                    string relative = RelativeZipPath(entry.FullName);
                    string dest = Path.GetFullPath(Path.Combine(stage, relative));
                    if (!dest.StartsWith(stage + Path.DirectorySeparatorChar,
                            StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Archive entry escapes its staging directory.");
                    if (!names.Add(dest))
                        throw new InvalidDataException("Archive contains duplicate or ambiguous paths.");
                    if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
                    {
                        Directory.CreateDirectory(dest);
                        continue;
                    }
                    expanded = checked(expanded + entry.Length);
                    if (expanded > MaxUnpackedBytes)
                        throw new InvalidDataException("Native update ZIP exceeds unpacked size limit.");
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    using var input = entry.Open();
                    using var output = new FileStream(dest, FileMode.CreateNew,
                        FileAccess.Write, FileShare.None);
                    byte[] buffer = new byte[65536];
                    long received = 0;
                    while (true)
                    {
                        int size = input.Read(buffer, 0, buffer.Length);
                        if (size == 0) break;
                        received = checked(received + size);
                        if (received > entry.Length || received > MaxUnpackedBytes)
                            throw new InvalidDataException("Archive entry exceeded its declared size.");
                        output.Write(buffer, 0, size);
                    }
                    if (received != entry.Length)
                        throw new InvalidDataException("Archive entry ended unexpectedly.");
                    output.Flush(flushToDisk: true);
                }
            }
            VerifyManifest(stage, expectedVersion);
            string exe = Path.Combine(stage, ExecutableName);
            if (!File.Exists(exe) || new FileInfo(exe).Length == 0)
                throw new InvalidDataException("Native update ZIP has no executable.");
            cancellation.ThrowIfCancellationRequested();
            Directory.Move(stage, target);
            return new Installed(expectedVersion, Path.Combine(target, ExecutableName),
                target, actual);
        }
        finally
        {
            // Only an unpublished, uniquely generated staging directory can
            // ever be removed; active versions and user databases are untouched.
            if (Directory.Exists(stage)) Directory.Delete(stage, recursive: true);
        }
    }

    public static async Task<Installed> DownloadAndStageAsync(Candidate update,
        CancellationToken cancellation = default, IProgress<string>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(update);
        if (!TryVersion(update.Tag, out var tagVersion) ||
            tagVersion != update.Version ||
            !TrustedPage(update.ReleasePage, update.Tag) ||
            !TrustedAsset(update.ArchiveUrl, update.Tag, ZipName) ||
            !TrustedAsset(update.ChecksumUrl, update.Tag, ChecksumName))
            throw new InvalidDataException("Untrusted native release metadata.");
        using var http = Client();
        progress?.Report("Reading the published native release checksum…");
        using var checksumResponse = await TrustedGetAsync(http, update.ChecksumUrl, cancellation);
        await using var checksumStream =
            await checksumResponse.Content.ReadAsStreamAsync(cancellation);
        string expected = ParseChecksum(Encoding.UTF8.GetString(
            await ReadBoundedAsync(checksumStream, 4096, cancellation)));

        string root = DefaultVersionsRoot;
        Directory.CreateDirectory(root);
        string download = Path.Combine(root, ".download-" + Guid.NewGuid().ToString("N") + ".zip");
        try
        {
            progress?.Report("Downloading the native application ZIP…");
            using (var response = await TrustedGetAsync(http, update.ArchiveUrl, cancellation))
            {
                if (response.Content.Headers.ContentLength > MaxArchiveBytes)
                    throw new InvalidDataException("Native update download is too large.");
                await using var from = await response.Content.ReadAsStreamAsync(cancellation);
                await using var to = new FileStream(download, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None, 65536, useAsync: true);
                byte[] buffer = new byte[65536];
                long total = 0;
                while (true)
                {
                    int n = await from.ReadAsync(buffer.AsMemory(), cancellation);
                    if (n == 0) break;
                    total = checked(total + n);
                    if (total > MaxArchiveBytes)
                        throw new InvalidDataException("Native update exceeds its download limit.");
                    await to.WriteAsync(buffer.AsMemory(0, n), cancellation);
                }
                await to.FlushAsync(cancellation);
            }
            progress?.Report("Verifying SHA-256 and staging a separate application version…");
            return InstallVerifiedArchive(download, expected, update.Version, root, cancellation);
        }
        finally
        {
            if (File.Exists(download)) File.Delete(download);
        }
    }
}
