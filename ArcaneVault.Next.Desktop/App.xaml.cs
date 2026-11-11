using System.Diagnostics;
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
        // The old version launches the new version, then closes. Do not open
        // the same private native SQLite workspace from both processes at once.
        foreach (string arg in e.Args)
        {
            const string prefix = "--upgrade-from-pid=";
            if (!arg.StartsWith(prefix, StringComparison.Ordinal)) continue;
            if (!int.TryParse(arg[prefix.Length..], out int pid) || pid <= 0 ||
                pid == Environment.ProcessId)
            {
                MessageBox.Show("Invalid native upgrade restart argument.",
                    "Arcane Vault Native", MessageBoxButton.OK, MessageBoxImage.Warning);
                Shutdown(1);
                return;
            }
            try
            {
                using var previous = Process.GetProcessById(pid);
                if (!previous.WaitForExit(30_000))
                {
                    MessageBox.Show(
                        "The previous Arcane Vault Native instance is still open. " +
                        "Close it before starting the new version to protect your collection.",
                        "Native update paused", MessageBoxButton.OK, MessageBoxImage.Warning);
                    Shutdown(1);
                    return;
                }
            }
            catch (ArgumentException)
            {
                // The previous process has already exited. Normal upgrade path.
            }
            break;
        }
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception error)
                RecordFailure("AppDomain unhandled", error);
        };
        base.OnStartup(e);
    }
}
