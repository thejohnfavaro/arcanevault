using System.Diagnostics;

namespace ArcaneVault.Desktop;

/// <summary>
/// Uses the user-authorized USB debugging connection to map a phone-local port
/// to the short-lived Windows collection server. No collection data is sent via cloud.
/// </summary>
internal static class UsbAdbTransfer
{
    public static async Task ConnectAsync(int port, CancellationToken cancellation = default)
    {
        if (port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        var adb = LocateAdb() ?? throw new InvalidOperationException(
            "ADB was not found. Install Android SDK Platform-Tools (Android Studio > SDK Manager) or use Export collection sync file over USB instead.");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(12));
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(adb)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };
        // -d requires a single connected USB device; it does not fall back to Wi-Fi ADB.
        process.StartInfo.ArgumentList.Add("-d");
        process.StartInfo.ArgumentList.Add("reverse");
        process.StartInfo.ArgumentList.Add($"tcp:{port}");
        process.StartInfo.ArgumentList.Add($"tcp:{port}");
        try
        {
            if (!process.Start()) throw new InvalidOperationException("Could not launch Android Debug Bridge.");
            var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var stderr = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            var output = (await stdout + " " + await stderr).Trim();
            if (process.ExitCode != 0)
                throw new InvalidOperationException("USB connection unavailable. Unlock your Galaxy, enable USB debugging and approve the computer's RSA prompt. ADB: " + output[..Math.Min(output.Length, 300)]);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            throw new InvalidOperationException("The USB connection timed out. Check USB debugging authorization and reconnect the cable.");
        }
    }

    private static string? LocateAdb()
    {
        var candidates = new List<string>();
        foreach (var root in new[]
        {
            Environment.GetEnvironmentVariable("ANDROID_HOME"),
            Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Android", "Sdk")
        })
            if (!string.IsNullOrWhiteSpace(root)) candidates.Add(Path.Combine(root, "platform-tools", "adb.exe"));

        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        candidates.AddRange(path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries).Select(dir => Path.Combine(dir, "adb.exe")));
        return candidates.FirstOrDefault(File.Exists);
    }
}
