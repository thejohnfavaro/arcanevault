using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;
using System.Diagnostics;

namespace ArcaneVault.Desktop;

public sealed class MainForm : Form
{
    private readonly WebView2 browser = new() { Dock = DockStyle.Fill, DefaultBackgroundColor = System.Drawing.Color.FromArgb(15, 18, 29) };
    private LocalAppServer? server;

    public MainForm()
    {
        // Keep the Windows title bar compact: application icon and product name only.
        Text = "Arcane Vault";
        ShowIcon = true;
        using (var iconStream = typeof(MainForm).Assembly.GetManifestResourceStream("ArcaneVault.Desktop.WindowIcon.ico"))
        {
            if (iconStream is not null)
            {
                using var resourceIcon = new System.Drawing.Icon(iconStream);
                Icon = (System.Drawing.Icon)resourceIcon.Clone();
            }
            else
            {
                // Use the EXE's embedded icon as a fallback, including published single-file builds.
                Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
        }
        Width = 1460;
        Height = 940;
        MinimumSize = new System.Drawing.Size(900, 640);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = System.Drawing.Color.FromArgb(15, 18, 29);
        Controls.Add(browser);
        Shown += async (_, _) => await StartAsync();
        FormClosed += (_, _) => server?.Dispose();
    }

    private async Task StartAsync()
    {
        try
        {
            server = new LocalAppServer();
            server.Start();

            var cacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ArcaneVault", "WebView2");
            Directory.CreateDirectory(cacheDir);
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: cacheDir);
            await browser.EnsureCoreWebView2Async(environment);
            browser.CoreWebView2.Settings.AreDevToolsEnabled = true;
            browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            browser.CoreWebView2.NavigationStarting += (_, args) =>
            {
                if (!args.Uri.StartsWith(LocalAppServer.BaseUrl, StringComparison.OrdinalIgnoreCase))
                {
                    args.Cancel = true;
                    try { Process.Start(new ProcessStartInfo(args.Uri) { UseShellExecute = true }); }
                    catch { /* ignore unsupported links */ }
                }
            };
            browser.Source = new Uri(LocalAppServer.BaseUrl);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                "Unable to start Arcane Vault.\n\n" + ex.Message +
                "\n\nIf the error mentions WebView2, install the Microsoft Edge WebView2 Runtime. " +
                "If port 8765 is in use, close the older Python Arcane Vault server first.",
                "Arcane Vault startup error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close();
        }
    }
}
