using ArcaneVault.Next.Desktop;
using System.Text.RegularExpressions;
using System.Xml.Linq;

internal static class NativeVisualShellChecks
{
    private const string XamlNs = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static void Must(bool yes, string message)
    {
        if (!yes) throw new InvalidOperationException("Native visual shell: " + message);
    }

    private static string FindDesktop()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, "ArcaneVault.Next.Desktop");
            if (File.Exists(Path.Combine(candidate, "MainWindow.xaml")))
                return candidate;
            dir = dir.Parent;
        }
        throw new InvalidOperationException(
            "Native visual shell regression could not locate the repository desktop source.");
    }

    public static void Run()
    {
        string dir = FindDesktop();
        var app = XDocument.Load(Path.Combine(dir, "App.xaml"));
        var keys = app.Descendants()
            .Select(e => e.Attribute(XName.Get("Key", XamlNs))?.Value)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var needed in new[]
        {
            "VaultBackground", "VaultSidebar", "VaultPanel",
            "VaultPanelRaised", "VaultText", "VaultMuted", "VaultAccent",
            "VaultAccentMuted", "VaultAccentSurface", "VaultAccentBorder",
            "VaultLine", "VaultGood", "VaultWarning", "VaultPrimaryButton",
            "VaultNavButton", "VaultNavPrimary", "VaultDangerButton",
            "VaultEyebrow", "VaultMutedText", "VaultPanelCard"
        })
            Must(keys.Contains(needed), "Theme resource missing: " + needed);

        var sourceWindows = new[]
        {
            "MainWindow", "NativeCollectionWindow", "NativeCollectionWorkspace",
            "NativeCardDetailsWindow",
            "NativeDeckEditorWindow", "NativeAddPrintingWindow",
            "NativeAdjustPhysicalWindow", "NativeArenaEditorWindow",
            "NativeFreshStartWindow", "CommanderBrowserWindow"
        };
        foreach (var name in sourceWindows)
        {
            string xamlPath = Path.Combine(dir, name + ".xaml");
            string csPath = Path.Combine(dir, name + ".xaml.cs");
            string xml = File.ReadAllText(xamlPath);
            var doc = XDocument.Parse(xml);
            string code = File.ReadAllText(csPath);
            var named = doc.Descendants()
                .Select(e => e.Attribute(XName.Get("Name", XamlNs))?.Value)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToArray();
            Must(named.Length == named.Distinct(StringComparer.Ordinal).Count(),
                name + " has duplicate x:Name controls.");

            foreach (Match match in Regex.Matches(xml,
                @"\{StaticResource (Vault[a-zA-Z0-9]+)\}"))
                Must(keys.Contains(match.Groups[1].Value),
                    name + " references undefined theme resource " + match.Groups[1].Value);

            foreach (Match match in Regex.Matches(xml,
                "(?:Click|Checked|Unchecked|TextChanged|SelectionChanged|" +
                "MouseDoubleClick|KeyDown|PasswordChanged)=\"(On[A-Za-z0-9_]+)\""))
                Must(code.Contains(match.Groups[1].Value + "(", StringComparison.Ordinal),
                    name + " XAML references missing event handler " +
                    match.Groups[1].Value);
        }

        string main = File.ReadAllText(Path.Combine(dir, "MainWindow.xaml"));
        string collection = File.ReadAllText(Path.Combine(dir, "NativeCollectionWorkspace.xaml"));
        string backupPreview = File.ReadAllText(
            Path.Combine(dir, "NativeCollectionWindow.xaml"));
        string backupPreviewCode = File.ReadAllText(
            Path.Combine(dir, "NativeCollectionWindow.xaml.cs"));
        string mainCode = File.ReadAllText(Path.Combine(dir, "MainWindow.xaml.cs"));
        foreach (string name in new[]
        {
            "OpenNativeButton", "StartFreshButton", "SyncCatalogButton",
            "CancelCatalogButton", "OpenCatalogButton", "SaveNativeCatalogButton",
            "MigrateLegacyButton", "ImportNativeButton",
            "ExportFullBackupButton", "RestoreRecoveryButton",
            "CollectionBackupButton", "SearchBox", "CardsList", "ModeStatus",
            "CatalogNavigationButton", "DeckLibraryNavigationButton",
            "NativeWorkspaceHost"
        })
            Must(main.Contains("x:Name=\"" + name + "\"", StringComparison.Ordinal),
                "WebView-inspired main shell lost functional control " + name);

        foreach (string name in new[]
        {
            "CollectionTabs", "PhysicalList", "ArenaList",
            "DeckLibraryList", "SetSummaryList", "SetSlotsList",
            "AddPhysicalButton", "AdjustPhysicalButton", "AddArenaButton",
            "EditArenaButton", "AddDeckButton", "EditDeckButton",
            "DuplicateDeckButton", "DeleteDeckButton", "TotalsText",
            "VerificationText", "SearchBox"
        })
            Must(collection.Contains("x:Name=\"" + name + "\"", StringComparison.Ordinal),
                "Native collection visual shell lost functional control " + name);

        Must(backupPreview.Contains("x:Name=\"PreviewHost\"", StringComparison.Ordinal) &&
             backupPreviewCode.Contains("new NativeCollectionWorkspace(", StringComparison.Ordinal) &&
             mainCode.Contains("new NativeCollectionWorkspace(", StringComparison.Ordinal),
            "Standalone backup preview and embedded native shell must share one collection control.");
        Must(main.Contains("OnOpenDeckLibraryClicked", StringComparison.Ordinal) &&
             main.Contains("OnShowCatalogClicked", StringComparison.Ordinal) &&
             mainCode.Contains("nativeWorkspaceView.NavigateTo(section)", StringComparison.Ordinal),
            "Card Catalog and Deck Library sidebar navigation must preserve in-shell routing.");
        Must(collection.Contains("Value=\"{Binding Percent}\"", StringComparison.Ordinal) &&
             collection.Contains("VirtualizingStackPanel.IsVirtualizing=\"True\"",
                 StringComparison.Ordinal),
             "Set Progress visualization must bind exact percentages and preserve virtualization.");
        Must(main.Contains("VirtualizingStackPanel.IsVirtualizing=\"True\"",
                 StringComparison.Ordinal),
             "Catalog list virtualization was dropped during redesign.");

        foreach (string badUrl in new[]
        {
            "http://cards.scryfall.io/normal/a.jpg",
            "https://cards.scryfall.io.evil.example/normal/a.jpg",
            "https://example.com/normal/a.jpg",
            "file:///C:/temp/art.jpg",
            "https://user:pass@cards.scryfall.io/normal/a.jpg",
            "https://cards.scryfall.io:4433/normal/a.jpg",
            "",
            "nonsense"
        })
            Must(!NativeCardArtwork.IsTrustedArtworkUrl(badUrl),
                 "Artwork loader trusted a non-official HTTPS artwork URL: " + badUrl);
        Must(NativeCardArtwork.IsTrustedArtworkUrl(
             "https://cards.scryfall.io/normal/front/a1/b2/example.jpg"),
             "Valid official Scryfall artwork URL was refused.");
        Must(NativeCardArtwork.LoadAsync("https://bad.example/card.jpg")
            .GetAwaiter().GetResult() is null,
             "Rejected non-Scryfall artwork URI should never initiate a fetch.");

        Console.WriteLine(
            "PASS: native WebView-inspired visual resources, XAML names/handlers, " +
            "embedded collection/deck navigation, shared backup preview, virtualized views, " +
            "exact Set Progress binding and official-only card artwork URLs");
    }
}
