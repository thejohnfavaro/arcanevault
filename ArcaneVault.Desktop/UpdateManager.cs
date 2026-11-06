using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ArcaneVault.Desktop;

/// <summary>Downloads only versioned, checksum-verified Windows releases from the official repository.</summary>
internal sealed class UpdateManager
{
    private const string ReleasesApi = "https://api.github.com/repos/thejohnfavaro/arcanevault/releases/tags/";
    private const string PackageName = "ArcaneVault-Windows-x64.zip";
    private static readonly HttpClient Client = CreateClient();
    private string? stagedFolder;
    private string? stagedVersion;

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ArcaneVault-Windows-Updater/1.0");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    public object Status() => new { ready = stagedFolder is not null, version = stagedVersion };

    public async Task<object> PrepareAsync(string version, CancellationToken cancellationToken)
    {
        if (!Regex.IsMatch(version, @"^\d+\.\d+\.\d+$"))
            throw new ArgumentException("Invalid Windows release version.");
        if (!Version.TryParse(version, out var requested) ||
            requested <= (typeof(UpdateManager).Assembly.GetName().Version ?? new Version(0, 0, 0)))
            throw new ArgumentException("The selected release isn't newer than this app.");
        if (Debugger.IsAttached || AppContext.BaseDirectory.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Run a published Arcane Vault executable to install updates, not the Visual Studio development build.");

        var tag = "windows-v" + version;
        using var releaseReply = await Client.GetAsync(ReleasesApi + tag, cancellationToken);
        releaseReply.EnsureSuccessStatusCode();
        using var release = JsonDocument.Parse(await releaseReply.Content.ReadAsStringAsync(cancellationToken));
        var root = release.RootElement;
        if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean() ||
            !string.Equals(root.GetProperty("tag_name").GetString(), tag, StringComparison.Ordinal))
            throw new InvalidOperationException("Only published stable Windows releases can be installed.");

        string? zipUrl = null;
        string? shaUrl = null;
        long packageSize = 0;
        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString();
            if (name == PackageName)
            {
                zipUrl = asset.GetProperty("browser_download_url").GetString();
                packageSize = asset.GetProperty("size").GetInt64();
            }
            else if (name == PackageName + ".sha256")
                shaUrl = asset.GetProperty("browser_download_url").GetString();
        }
        if (zipUrl is null || shaUrl is null || packageSize is < 1000 or > 250_000_000)
            throw new InvalidOperationException("Release must contain a Windows ZIP and its SHA-256 checksum.");
        AssertAssetUrl(zipUrl, tag);
        AssertAssetUrl(shaUrl, tag);

        var expectedLine = await Client.GetStringAsync(shaUrl, cancellationToken);
        var expected = Regex.Match(expectedLine, @"\b[a-fA-F0-9]{64}\b").Value;
        if (expected.Length != 64) throw new InvalidOperationException("Release checksum is invalid.");

        var staging = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ArcaneVault", "Updates", "windows-" + version + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        var zip = Path.Combine(staging, PackageName);
        var extracted = Path.Combine(staging, "content");
        try
        {
            using (var result = await Client.GetAsync(zipUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                result.EnsureSuccessStatusCode();
                await using var stream = await result.Content.ReadAsStreamAsync(cancellationToken);
                await using var output = new FileStream(zip, FileMode.CreateNew, FileAccess.Write, FileShare.None, 131072, true);
                await stream.CopyToAsync(output, cancellationToken);
                if (output.Length != packageSize) throw new InvalidOperationException("Incomplete Windows update download.");
            }
            using (var input = File.OpenRead(zip))
            {
                var actual = Convert.ToHexString(await SHA256.HashDataAsync(input, cancellationToken));
                if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Windows update failed checksum verification.");
            }
            Directory.CreateDirectory(extracted);
            using (var archive = ZipFile.OpenRead(zip))
            {
                long totalSize = 0;
                foreach (var entry in archive.Entries)
                {
                    var name = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
                    var resolved = Path.GetFullPath(Path.Combine(extracted, name));
                    if (!resolved.StartsWith(Path.GetFullPath(extracted) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Unsafe path in update package.");
                    totalSize += entry.Length;
                    if (totalSize > 700_000_000) throw new InvalidOperationException("Update package is too large.");
                    if (string.IsNullOrEmpty(entry.Name)) { Directory.CreateDirectory(resolved); continue; }
                    Directory.CreateDirectory(Path.GetDirectoryName(resolved)!);
                    entry.ExtractToFile(resolved, true);
                }
            }
            if (!File.Exists(Path.Combine(extracted, "ArcaneVault.Desktop.exe")))
                throw new InvalidOperationException("The release does not contain the Windows application executable.");
            stagedFolder = extracted;
            stagedVersion = version;
            return Status();
        }
        catch
        {
            try { Directory.Delete(staging, true); } catch { }
            throw;
        }
    }

    private static void AssertAssetUrl(string url, string tag)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
            !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
            !uri.AbsolutePath.StartsWith("/thejohnfavaro/arcanevault/releases/download/" + tag + "/", StringComparison.Ordinal))
            throw new InvalidOperationException("Unexpected update download host or release.");
    }

    public void LaunchAndRestart()
    {
        var stage = stagedFolder ?? throw new InvalidOperationException("Download and verify an update first.");
        var target = Path.GetDirectoryName(Environment.ProcessPath) ?? throw new InvalidOperationException("Installation folder unavailable.");
        var newExe = Path.Combine(stage, "ArcaneVault.Desktop.exe");
        var args = new[] { "--apply-update", Environment.ProcessId.ToString(), target, stage };
        var info = new ProcessStartInfo(newExe) { UseShellExecute = true };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        _ = Process.Start(info) ?? throw new InvalidOperationException("The updater could not start.");
    }

    /// <summary>Run by the verified new executable outside the old application's folder.</summary>
    public static void ApplyUpdate(string[] args)
    {
        if (args.Length != 4 || !int.TryParse(args[1], out var pid)) return;
        var target = Path.GetFullPath(args[2]);
        var source = Path.GetFullPath(args[3]);
        if (!File.Exists(Path.Combine(source, "ArcaneVault.Desktop.exe"))) return;
        try
        {
            using (var old = Process.GetProcessById(pid)) if (!old.WaitForExit(60000))
                throw new TimeoutException("Arcane Vault must close before the update can finish.");
        }
        catch (ArgumentException) { /* already exited */ }
        try
        {
            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(source, file);
                var destination = Path.GetFullPath(Path.Combine(target, relative));
                if (!destination.StartsWith(target + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Invalid update destination.");
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                var temp = destination + ".arcane-new";
                File.Copy(file, temp, overwrite: true);
                File.Move(temp, destination, overwrite: true);
            }
            Process.Start(new ProcessStartInfo(Path.Combine(target, "ArcaneVault.Desktop.exe"))
                { UseShellExecute = true, WorkingDirectory = target });
        }
        catch (Exception ex)
        {
            System.Windows.Forms.MessageBox.Show(
                "The update could not be installed automatically.\n\n" + ex.Message +
                "\n\nYou can download and install the release manually from GitHub.",
                "Arcane Vault updater", System.Windows.Forms.MessageBoxButtons.OK,
                System.Windows.Forms.MessageBoxIcon.Error);
        }
    }
}
