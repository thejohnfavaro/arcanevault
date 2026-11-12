using System.Diagnostics;
using ArcaneVault.Next.Collection;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace ArcaneVault.Next.Desktop;

public partial class App : Application
{
    private static int reportingFailure;

    // A WPF UI exception previously could close the process without a usable
    // error report. Keep collection data untouched and log to the user profile.
    private static string RecordFailure(string source, Exception error)
    {
        string directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ArcaneVault", "Diagnostics");
        string path = Path.Combine(directory, "native-crash.log");
        try
        {
            Directory.CreateDirectory(directory);
            File.AppendAllText(path,
                $"[{DateTimeOffset.Now:O}] {source}\n{error}\n\n");
        }
        catch { /* Logging must not create a second process failure. */ }
        return path;
    }

    private void OnDispatcherUnhandledException(object sender,
        DispatcherUnhandledExceptionEventArgs e)
    {
        if (Interlocked.Exchange(ref reportingFailure, 1) != 0) return;
        string path = RecordFailure("UI dispatcher", e.Exception);
        bool recovered = false;
        try
        {
            recovered = (MainWindow as MainWindow)?.RecoverFromSetProgressFault() == true;
        }
        catch (Exception recoveryError)
        {
            RecordFailure("Set Progress recovery", recoveryError);
        }
        try
        {
            MessageBox.Show(
                (recovered
                    ? "Set Progress could not render and was safely closed. "
                    : "Arcane Vault encountered an unexpected UI error. ") +
                "Your collection has not been intentionally modified.\n\n" +
                e.Exception.Message + "\n\nDiagnostic log: " + path,
                "Arcane Vault Native — error", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch { }
        // Only a failed Set Progress view has a known safe recovery route.
        // Other unexpected faults remain fatal rather than hiding corruption.
        e.Handled = recovered;
        Interlocked.Exchange(ref reportingFailure, 0);
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        // Register reporting BEFORE any theme or handoff work. Previously an
        // early startup exception could escape without a diagnostic.
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception error)
                RecordFailure("AppDomain unhandled", error);
        };

        int? previousPid = null;
        string? readyId = null;
        foreach (string arg in e.Args)
        {
            if (arg.StartsWith("--upgrade-from-pid=", StringComparison.Ordinal))
            {
                var value = arg["--upgrade-from-pid=".Length..];
                if (!int.TryParse(value, out int pid) || pid <= 0 ||
                    pid == Environment.ProcessId)
                {
                    RecordFailure("Native relaunch validation",
                        new InvalidDataException("Invalid previous process ID."));
                    Shutdown(1);
                    return;
                }
                previousPid = pid;
            }
            else if (arg.StartsWith(NativeUpgradeHandoff.ArgumentPrefix,
                StringComparison.Ordinal))
                readyId = arg[NativeUpgradeHandoff.ArgumentPrefix.Length..];
        }

        // Appearance is optional and never a reason to lose access to cards.
        // In particular, do not re-run a failed Apply on the same resources.
        try { VaultThemeManager.LoadSaved(); }
        catch (Exception ex)
        {
            RecordFailure("Theme preference fallback", ex);
            // The default XAML brushes remain available.
        }

        if (previousPid is not null)
        {
            if (readyId is not null)
            {
                try { NativeUpgradeHandoff.Signal(readyId); }
                catch (Exception ex)
                {
                    RecordFailure("Native relaunch handshake", ex);
                    Shutdown(1);
                    return;
                }
            }
            else
            {
                // Compatible with previous updater installations that only
                // supply the parent PID. Never open a live workspace twice.
                try
                {
                    using var previous = Process.GetProcessById(previousPid.Value);
                    if (!previous.WaitForExit(30_000))
                    {
                        RecordFailure("Legacy native relaunch",
                            new TimeoutException("Previous app did not exit."));
                        Shutdown(1);
                        return;
                    }
                }
                catch (ArgumentException) { }
            }
        }
        base.OnStartup(e);
    }

}
