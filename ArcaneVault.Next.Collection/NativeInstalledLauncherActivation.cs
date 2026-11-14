using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ArcaneVault.Next.Collection;

/// <summary>
/// Updates only the permanent launcher's app-version pointer AFTER the new
/// native WPF window has loaded. Never writes to collection or catalog paths.
/// The user may keep the previous verified version for rollback.
/// </summary>
public static class NativeInstalledLauncherActivation
{
    public const string LauncherName = "ArcaneVault.exe";
    public const string PointerName = "active-version.txt";

    public static bool TryActivate(string versionsRoot, string executable)
    {
        if (string.IsNullOrWhiteSpace(versionsRoot) ||
            string.IsNullOrWhiteSpace(executable)) return false;
        string versions = Path.GetFullPath(versionsRoot)
            .TrimEnd(Path.DirectorySeparatorChar);
        string installRoot = Path.GetDirectoryName(versions) ?? "";
        string versionFolder = Path.GetDirectoryName(Path.GetFullPath(executable)) ?? "";
        if (!Path.GetFileName(versions).Equals("versions",
                StringComparison.OrdinalIgnoreCase) ||
            !Path.GetDirectoryName(versionFolder)!.Equals(versions,
                StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(executable).Equals(NativeUpdateService.ExecutableName,
                StringComparison.OrdinalIgnoreCase) ||
            !SafeDir(versions) || !SafeDir(installRoot) ||
            !SafeDir(versionFolder) ||
            !SafeFile(Path.Combine(installRoot, LauncherName)))
            return false;

        string token = Path.GetFileName(versionFolder);
        if (!Regex.IsMatch(token, @"^v[0-9]+\.[0-9]+\.[0-9]+$",
                RegexOptions.CultureInvariant) ||
            !Version.TryParse(token[1..], out var version) ||
            !SafeFile(Path.Combine(versionFolder, NativeUpdateService.ManifestName)))
            return false;

        try
        {
            string manifestPath = Path.Combine(versionFolder,
                NativeUpdateService.ManifestName);
            if (new FileInfo(manifestPath).Length > 4096) return false;
            using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var obj = manifest.RootElement;
            if (obj.ValueKind != JsonValueKind.Object ||
                !obj.TryGetProperty("version", out var raw) ||
                !Version.TryParse(raw.GetString(), out var actual) ||
                actual != version ||
                !obj.TryGetProperty("channel", out var channel) ||
                channel.GetString() != "preview" ||
                !obj.TryGetProperty("sourceSha", out var sha) ||
                !Regex.IsMatch(sha.GetString() ?? "", @"^[a-fA-F0-9]{40}$"))
                return false;

            string pointer = Path.Combine(installRoot, PointerName);
            if (File.Exists(pointer))
            {
                if (!SafeFile(pointer) || new FileInfo(pointer).Length > 40)
                    return false;
                string previous = File.ReadAllText(pointer).Trim();
                if (Regex.IsMatch(previous, @"^v[0-9]+\.[0-9]+\.[0-9]+$") &&
                    Version.TryParse(previous[1..], out var oldVersion) &&
                    oldVersion > version)
                    return false; // Never silently downgrade a pinned launch.
            }

            string temporary = Path.Combine(installRoot,
                ".active-version-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.WriteAllText(temporary, token);
                File.Move(temporary, pointer, overwrite: true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                   or JsonException or InvalidOperationException)
        {
            return false;
        }
    }

    private static bool SafeDir(string path)
    {
        var info = new DirectoryInfo(path);
        return info.Exists && (info.Attributes & FileAttributes.ReparsePoint) == 0;
    }

    private static bool SafeFile(string path)
    {
        var info = new FileInfo(path);
        return info.Exists && (info.Attributes & FileAttributes.ReparsePoint) == 0;
    }
}
