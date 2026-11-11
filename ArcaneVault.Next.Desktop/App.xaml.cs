using System.Diagnostics;
using System.Windows;

namespace ArcaneVault.Next.Desktop;

public partial class App : Application
{
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
        base.OnStartup(e);
    }
}
