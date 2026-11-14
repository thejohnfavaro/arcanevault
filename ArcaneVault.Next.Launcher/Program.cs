using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ArcaneVault.Next.Launcher;

/// <summary>
/// Permanent taskbar/Start Menu target. Its own path never changes with an update.
/// Installed app versions, and ONLY app versions, live under ./versions/vX.Y.Z.
/// The running app writes active-version.txt after its window has loaded.
/// </summary>
internal static class Program
{
    private const string AppExe = "ArcaneVault.Next.Desktop.exe";
    private const string Manifest = "native-version.json";
    private const string Pointer = "active-version.txt";

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "MessageBoxW")]
    private static extern int MessageBox(IntPtr hwnd, string text, string caption, uint flags);

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            string root = Path.GetFullPath(AppContext.BaseDirectory);
            string versions = Path.Combine(root, "versions");
            if (!SafeDirectory(root) || !SafeDirectory(versions))
                throw new InvalidDataException("The installed versions folder is missing or unsafe.");

            string? selected = null;
            string pointerFile = Path.Combine(root, Pointer);
            if (File.Exists(pointerFile) && SafeFile(pointerFile) &&
                new FileInfo(pointerFile).Length <= 40)
            {
                string token = File.ReadAllText(pointerFile).Trim();
                if (TryResolve(versions, token, out var path))
                    selected = path;
            }
            if (selected is null)
            {
                selected = Directory.EnumerateDirectories(versions, "v*",
                        SearchOption.TopDirectoryOnly)
                    .Select(path => new { Path = path, Version = ParseVersion(Path.GetFileName(path)) })
                    .Where(item => item.Version is not null)
                    .OrderByDescending(item => item.Version)
                    .Select(item => TryResolve(versions, Path.GetFileName(item.Path), out var exe)
                        ? exe : null)
                    .FirstOrDefault(path => path is not null);
            }
            if (selected is null)
                throw new InvalidDataException("No verified installed application version was found. Reinstall Arcane Vault without deleting your collection.");

            var start = new ProcessStartInfo(selected)
            {
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(selected)!
            };
            foreach (string arg in args) start.ArgumentList.Add(arg);
            using var process = Process.Start(start);
            if (process is null) throw new IOException("Windows did not start Arcane Vault.");
            return 0;
        }
        catch (Exception ex)
        {
            MessageBox(IntPtr.Zero, ex.Message +
                "\n\nYour Arcane Vault collection was not changed by the launcher.",
                "Arcane Vault could not start", 0x10);
            return 1;
        }
    }

    private static Version? ParseVersion(string? folder)
    {
        if (folder is null || !Regex.IsMatch(folder,
                @"^v[0-9]+\.[0-9]+\.[0-9]+$", RegexOptions.CultureInvariant))
            return null;
        return Version.TryParse(folder[1..], out var value) ? value : null;
    }

    private static bool SafeDirectory(string path)
    {
        var info = new DirectoryInfo(path);
        return info.Exists && (info.Attributes & FileAttributes.ReparsePoint) == 0;
    }

    private static bool SafeFile(string path)
    {
        var info = new FileInfo(path);
        return info.Exists && (info.Attributes & FileAttributes.ReparsePoint) == 0;
    }

    private static bool TryResolve(string versions, string token, out string? exe)
    {
        exe = null;
        Version? version = ParseVersion(token);
        if (version is null) return false;
        string directory = Path.Combine(versions, token);
        if (!SafeDirectory(directory)) return false;
        string binary = Path.Combine(directory, AppExe);
        string manifest = Path.Combine(directory, Manifest);
        if (!SafeFile(binary) || !SafeFile(manifest) ||
            new FileInfo(binary).Length == 0 ||
            new FileInfo(manifest).Length > 4096)
            return false;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(manifest));
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("version", out var raw) ||
                raw.ValueKind != JsonValueKind.String ||
                !Version.TryParse(raw.GetString(), out var found) ||
                found != version ||
                !root.TryGetProperty("channel", out var channel) ||
                channel.GetString() != "preview" ||
                !root.TryGetProperty("sourceSha", out var sha) ||
                sha.ValueKind != JsonValueKind.String ||
                !Regex.IsMatch(sha.GetString() ?? "", "^[a-fA-F0-9]{40}$"))
                return false;
            exe = binary;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                   or JsonException or InvalidOperationException)
        {
            return false;
        }
    }
}
