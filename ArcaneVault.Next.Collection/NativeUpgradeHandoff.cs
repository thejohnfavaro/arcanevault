namespace ArcaneVault.Next.Collection;

/// <summary>
/// Short-lived process handoff signal. Lives in the OS temp folder, outside
/// installed application and all collection/catalog/recovery directories.
/// This confirms the new process entered its startup code before the old app
/// closes; it is not a claim that every window has finished rendering.
/// </summary>
public static class NativeUpgradeHandoff
{
    public const string ArgumentPrefix = "--upgrade-ready-id=";

    public static string NewId() => Guid.NewGuid().ToString("N");

    public static string ReadyPath(string id)
    {
        if (id.Length != 32 || !id.All(Uri.IsHexDigit))
            throw new InvalidDataException("Invalid native upgrade handoff token.");
        return Path.Combine(Path.GetTempPath(),
            "arcane-vault-native-relaunch-" + id.ToLowerInvariant() + ".ready");
    }

    public static void Signal(string id)
    {
        var file = ReadyPath(id);
        // CreateNew refuses stale/reused tokens. No database or executable
        // file is touched by this handoff.
        using var stream = new FileStream(file, FileMode.CreateNew,
            FileAccess.Write, FileShare.Read);
        stream.Flush(flushToDisk: true);
    }

    public static async Task<bool> WaitForReadyAsync(string id,
        Func<bool> childStillAlive, TimeSpan timeout,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(childStillAlive);
        string file = ReadyPath(id);
        if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromSeconds(30))
            throw new ArgumentOutOfRangeException(nameof(timeout));
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (clock.Elapsed < timeout)
        {
            cancellation.ThrowIfCancellationRequested();
            if (!childStillAlive()) return false;
            if (File.Exists(file)) return true;
            await Task.Delay(100, cancellation);
        }
        return false;
    }

    public static void Cleanup(string id)
    {
        string file = ReadyPath(id);
        try { File.Delete(file); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
