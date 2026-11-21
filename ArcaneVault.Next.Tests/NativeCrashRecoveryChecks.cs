using ArcaneVault.Next.Collection;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

/// <summary>
/// Exercises the real NativeWorkspaceSafety.Refresh path in an isolated CHILD
/// process. The child is forcibly terminated at two injected boundaries.
/// No user workspace paths or normal application processes are touched.
/// </summary>
internal static class NativeCrashRecoveryChecks
{
    private static byte[] Backup(int physical, int arena) =>
        JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema = 2,
            cards = new[] { new { name = "Sample Arena Card", qty = arena } },
            physicalCards = new[] {
                new { name = "Sol Ring", qty = physical, set = "TST",
                    number = "1", finish = "nonfoil",
                    scryfallId = "00000000-0000-4000-8000-000000000111" }
            },
            deckLibrary = new[] { new {
                id = "crash_sample_deck", name = "Synthetic Commander Deck",
                commander = "Test Commander",
                entries = new[] { new { name = "Sol Ring", qty = 1 } },
                revisions = Array.Empty<object>()
            }},
            preferredArtwork = new { }
        });

    private static string Digest(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    public static void RunCrashChild(string checkpoint, string native, string incomingPath)
    {
        // Only accept the precisely-named disposable fixtures under our own
        // unique test directory. Never allow this subprocess to target live data.
        var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(native);
        var parent = Path.GetDirectoryName(full)!;
        var folder = Path.GetFileName(parent);
        if (!OperatingSystem.IsWindows() ||
            !full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) ||
            !folder.StartsWith("arcane-next-tests-", StringComparison.OrdinalIgnoreCase) ||
            Path.GetFileName(full) != "crash-workspace.sqlite" ||
            Path.GetDirectoryName(Path.GetFullPath(incomingPath)) != parent ||
            Path.GetDirectoryName(Path.GetFullPath(checkpoint)) != parent ||
            !File.Exists(full) || !File.Exists(incomingPath))
            throw new InvalidDataException("Crash test child refused a non-synthetic fixture.");

        var current = NativeWorkspace.Open(full);
        byte[] incoming = File.ReadAllBytes(incomingPath);
        var selected = Path.GetFileNameWithoutExtension(checkpoint);
        if (selected is not ("after-staging" or "after-atomic-replace"))
            throw new InvalidDataException("Unknown injected crash boundary.");

        NativeWorkspaceSafety.RegressionCrashPoint = point =>
        {
            if (point != selected) return;
            // Marker confirms the actual production Refresh boundary executed.
            // File.WriteAllText closes the marker before process termination.
            File.WriteAllText(checkpoint, point);
            Process.GetCurrentProcess().Kill();
            Thread.Sleep(Timeout.Infinite);
        };
        NativeWorkspaceSafety.Refresh(full, incoming, "synthetic-crash-recovery",
            current.BackupSha256);
        throw new InvalidOperationException("Fault injection did not terminate the child.");
    }

    private static void CrashAt(string root, string point)
    {
        string directory = Path.Combine(root, "crash-" + point);
        Directory.CreateDirectory(directory);
        // The child safety gate requires the fixture directly below a temp
        // 'arcane-next-tests-*' folder, so create that per scenario.
        string native = Path.Combine(directory, "crash-workspace.sqlite");
        string incomingPath = Path.Combine(directory, "candidate.json");
        string marker = Path.Combine(directory, point + ".txt");
        var first = NativeWorkspace.ImportNewBytes(Backup(2, 4),
            "crash-fixture-original", native);
        byte[] target = Backup(5, 6);
        File.WriteAllBytes(incomingPath, target);
        string originalHash = Digest(native);
        string originalSha = first.BackupSha256;

        var program = Environment.ProcessPath ??
            throw new InvalidOperationException("No path to regression runner executable.");
        using var child = new Process();
        child.StartInfo = new ProcessStartInfo(program)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = directory
        };
        if (Path.GetFileNameWithoutExtension(program).Equals("dotnet",
                StringComparison.OrdinalIgnoreCase))
            child.StartInfo.ArgumentList.Add(typeof(NativeCrashRecoveryChecks)
                .Assembly.Location);
        child.StartInfo.ArgumentList.Add("--crash-fixture");
        child.StartInfo.ArgumentList.Add(marker);
        child.StartInfo.ArgumentList.Add(native);
        child.StartInfo.ArgumentList.Add(incomingPath);
        RegressionExpect.That(child.Start(), "Launch isolated child for " + point);
        if (!child.WaitForExit(25000))
        {
            child.Kill(entireProcessTree: true);
            child.WaitForExit();
            throw new InvalidOperationException("Crash-test child did not exit at " + point);
        }
        RegressionExpect.That(child.ExitCode != 0,
            "Injected process termination must not look like a successful save");
        RegressionExpect.That(File.Exists(marker),
            "The actual refresh operation must reach fault injection point " + point);
        RegressionExpect.Equal(File.ReadAllText(marker), point,
            "Child marker must identify the requested crash boundary");

        var rollbacks = NativeWorkspaceSafety.RecoveryPaths(native);
        var scratch = Directory.GetFiles(directory, "crash-workspace.sqlite.refresh-*.sqlite");
        var active = NativeWorkspace.Open(native);
        if (point == "after-staging")
        {
            Console.WriteLine("  CHECK: crash before replacement retains original workspace");
            RegressionExpect.Equal(Digest(native), originalHash,
                "Original workspace bytes remain unchanged before atomic swap");
            RegressionExpect.Equal(active.BackupSha256, originalSha,
                "Original collection remains active after crash before swap");
            RegressionExpect.Equal(rollbacks.Count, 0,
                "No recovery copy should exist before the first replacement");
            RegressionExpect.Equal(scratch.Length, 1,
                "One complete uncommitted staged candidate remains after process kill");
            RegressionExpect.Equal(NativeWorkspace.Open(scratch[0]).BackupSha256,
                Convert.ToHexString(SHA256.HashData(target)),
                "Staged data is valid but must not become live without an atomic replace");
            // A normal retry can still publish the candidate; the original
            // remains available as its own recovery file.
            var retried = NativeWorkspaceSafety.Refresh(native, target,
                "synthetic-crash-retry", originalSha);
            RegressionExpect.Equal(retried.Current.Collection.PhysicalCopies, 5L,
                "Retry after staging crash applies the intended five physical copies");
            RegressionExpect.Equal(NativeWorkspace.Open(retried.RecoveryPath).BackupSha256,
                originalSha, "Retry preserves original workspace as recovery");
        }
        else
        {
            Console.WriteLine("  CHECK: crash after atomic replacement retains both generations");
            RegressionExpect.Equal(active.Collection.PhysicalCopies, 5L,
                "Committed replacement retains all new physical copies");
            RegressionExpect.Equal(active.Collection.ArenaCopies, 6L,
                "Committed replacement retains all new Arena copies");
            RegressionExpect.Equal(active.BackupSha256,
                Convert.ToHexString(SHA256.HashData(target)),
                "Active workspace has new verified backup checksum");
            RegressionExpect.Equal(rollbacks.Count, 1,
                "Atomic replacement preserves exactly one prior recovery copy");
            RegressionExpect.Equal(NativeWorkspace.Open(rollbacks[0]).BackupSha256,
                originalSha, "Rollback after crash retains original verified collection");
            RegressionExpect.Equal(scratch.Length, 0,
                "Atomic swap leaves no uncommitted staging file behind");
        }
    }

    public static void Run(string work)
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.WriteLine("SKIP crash fault injection: this suite requires Windows File.Replace.");
            return;
        }
        // 'work' is already a generated temp folder, never Arcane Vault user data.
        var full = Path.GetFullPath(work);
        var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        RegressionExpect.That(full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) &&
            Path.GetFileName(full).StartsWith("arcane-next-tests-", StringComparison.OrdinalIgnoreCase),
            "Crash tests must execute inside a generated temporary test directory");
        Console.WriteLine("  CHECK A: terminate child immediately after staged SQLite verification");
        CrashAt(full, "after-staging");
        Console.WriteLine("  CHECK B: terminate child immediately after Windows atomic File.Replace");
        CrashAt(full, "after-atomic-replace");
    }
}
