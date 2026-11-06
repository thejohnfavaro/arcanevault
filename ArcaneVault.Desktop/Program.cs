using System;
using System.Windows.Forms;

namespace ArcaneVault.Desktop;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // Prevent accidentally starting two desktop windows/embedded servers on port 8765.
        using var singleInstance = new Mutex(initiallyOwned: true,
            name: @"Local\ArcaneVault_Desktop_SingleInstance", createdNew: out bool firstInstance);
        if (!firstInstance)
        {
            MessageBox.Show("Arcane Vault is already running. Switch to the existing window.",
                "Arcane Vault", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
