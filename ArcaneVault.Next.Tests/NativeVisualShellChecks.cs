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

        // Administrative functions remain reachable but do not occupy the
        // main workspace navigation. Keep handlers and IDs in their groups.
        var shell = XDocument.Parse(main);
        var groups = shell.Descendants()
            .Where(e => e.Name.LocalName == "Expander").ToArray();
        foreach (var category in new[] {
            (Name: "CardDatabaseSettings", Header: "Card Database",
                Buttons: new[] { "SyncCatalogButton", "CancelCatalogButton",
                    "OpenCatalogButton", "SaveNativeCatalogButton" }),
            (Name: "DataSyncSettings", Header: "Data & Sync",
                Buttons: new[] { "MigrateLegacyButton", "ImportNativeButton",
                    "ExportFullBackupButton", "RestoreRecoveryButton",
                    "CollectionBackupButton" }),
            (Name: "NativeUpdatesSettings", Header: "Native Updates",
                Buttons: new[] { "CheckNativeUpdatesButton" }),
            (Name: "GeneralOptionsSettings", Header: "Options",
                Buttons: new[] { "StartFreshButton" })
        })
        {
            var group = groups.SingleOrDefault(e =>
                e.Attribute(XName.Get("Name", XamlNs))?.Value == category.Name);
            Must(group is not null &&
                 group.Attribute("Header")?.Value == category.Header &&
                 group.Attribute("IsExpanded")?.Value == "False",
                "Collapsible sidebar category missing: " + category.Name);
            foreach (string name in category.Buttons)
                Must(group!.Descendants().Count(e =>
                    e.Attribute(XName.Get("Name", XamlNs))?.Value == name) == 1,
                    "Existing command missing from settings category: " + name);
        }
        foreach (var navigation in new[] {
            "CatalogNavigationButton", "OpenNativeButton",
            "DeckLibraryNavigationButton", "SetProgressNavigationButton"
        })
            Must(!groups.Any(e => e.Descendants().Any(d =>
                d.Attribute(XName.Get("Name", XamlNs))?.Value == navigation)),
                "Core workspace navigation must stay outside collapsed categories.");

        string workspaceXaml = File.ReadAllText(
            Path.Combine(dir, "NativeCollectionWorkspace.xaml"));
        Must(workspaceXaml.Contains("ArtUrl=\"{Binding CoverArtworkUrl}\"",
                 StringComparison.Ordinal) &&
             workspaceXaml.Contains("DecodeWidth=\"220\"", StringComparison.Ordinal) &&
             workspaceXaml.Contains("VirtualizationMode=\"Recycling\"", StringComparison.Ordinal),
             "Deck covers must use the existing virtualized, cached card art viewer.");


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
            "CatalogNavigationButton", "DeckLibraryNavigationButton", "SetProgressNavigationButton",
            "NativeWorkspaceHost"
        })
            Must(main.Contains("x:Name=\"" + name + "\"", StringComparison.Ordinal),
                "WebView-inspired main shell lost functional control " + name);

        foreach (string name in new[]
        {
            "CollectionTabs", "PhysicalList", "ArenaList",
            "DeckLibraryList", "SetSummaryList", "SetSlotsList", "SetScopeBox",
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
        Must(main.Contains("OnOpenSetProgressClicked", StringComparison.Ordinal) &&
             mainCode.Contains("NavigateToNativeWorkspaceAsync(3)", StringComparison.Ordinal),
             "Set Progress must be available as a direct sidebar destination.");
        Must(main.Contains("OnOpenDeckLibraryClicked", StringComparison.Ordinal) &&
             main.Contains("OnShowCatalogClicked", StringComparison.Ordinal) &&
             mainCode.Contains("nativeWorkspaceView.NavigateTo(section)", StringComparison.Ordinal),
            "Card Catalog and Deck Library sidebar navigation must preserve in-shell routing.");
        string collectionCode = File.ReadAllText(
            Path.Combine(dir, "NativeCollectionWorkspace.xaml.cs"));
        string detailsCode = File.ReadAllText(
            Path.Combine(dir, "NativeCardDetailsWindow.xaml.cs"));
        string galleryCode = File.ReadAllText(
            Path.Combine(dir, "NativePrintingGalleryWindow.xaml.cs"));
        Must(collectionCode.Contains("arenaScope: arenaScope, selectedArtworkUrl: arenaArt",
                 StringComparison.Ordinal) &&
             detailsCode.Contains("arenaOnly: arenaScope", StringComparison.Ordinal) &&
             galleryCode.Contains("arenaOnly: arenaOnly", StringComparison.Ordinal),
            "Arena Collection, Card Details and artwork gallery must preserve the Arena printing scope.");
        Must(collection.Contains("Value=\"{Binding Percent, Mode=OneWay}\"", StringComparison.Ordinal) &&
             collection.Contains("VirtualizingStackPanel.IsVirtualizing=\"True\"",
                 StringComparison.Ordinal),
             "Set Progress must use a OneWay binding for the computed, read-only Percent property and preserve virtualization.");
        // Collection and Deck Library must be opened only from sidebar navigation.
        string startup = mainCode.Split("private async Task InitializeNativeAsync()", 2)[1]
            .Split("private async Task LoadNativeOwnershipForCatalogAsync()", 2)[0];
        Must(startup.Contains("await LoadNativeOwnershipForCatalogAsync();", StringComparison.Ordinal) &&
             !startup.Contains("await OpenNativeCollectionAsync();", StringComparison.Ordinal),
             "Startup must only prefetch ownership and not open Collection / Deck Library.");

        Must(collection.Contains("OnSetTileClicked", StringComparison.Ordinal) &&
             collection.Contains("UniformGrid Rows=\"1\"", StringComparison.Ordinal) &&
             collection.Contains("OnSetScopeChanged", StringComparison.Ordinal) &&
             collectionCode.Contains("LoadArenaSetNames", StringComparison.Ordinal) &&
             collectionCode.Contains("SetTileRow", StringComparison.Ordinal),
            "Set Progress requires responsive virtualized cards and distinct Arena / physical scopes.");
        Must(collection.Contains("BackToSetsButton", StringComparison.Ordinal) &&
             collection.Contains("HideUnownedSetSlots", StringComparison.Ordinal) &&
             collection.Contains("ArtUrl=\"{Binding ArtworkUrl}\"", StringComparison.Ordinal) &&
             collection.Contains("SetDetailsControls", StringComparison.Ordinal) &&
             collectionCode.Contains("SetDetailsMode(true)", StringComparison.Ordinal) &&
             collectionCode.Contains("OnBackToSetsClicked", StringComparison.Ordinal) &&
             collectionCode.Contains("RefreshSetSlotFilter()", StringComparison.Ordinal) &&
             collectionCode.Contains("x => x.VerifiedCopies > 0", StringComparison.Ordinal),
             "Set Progress must open an artwork-rich in-place checklist with a hide-unowned filter and back navigation.");
        Must(collection.Contains("BannerArtworkUrl", StringComparison.Ordinal) &&
             collection.Contains("NativeSetSymbol SetCode=", StringComparison.Ordinal) &&
             collection.Contains("DecodeWidth=\"600\"", StringComparison.Ordinal),
             "Set tiles must use full-bleed art crops and official vector set-symbol controls.");
        Must(collection.Contains("Height=\"196\" Margin=\"6,6\" ClipToBounds=\"True\"", StringComparison.Ordinal) &&
             collection.Contains("x:Name=\"SetBannerSurface\" ClipToBounds=\"True\"", StringComparison.Ordinal) &&
             collection.Contains("<ImageBrush ImageSource=\"{Binding Source, ElementName=SetBannerImage}\"", StringComparison.Ordinal) &&
             collection.Contains("Stretch=\"UniformToFill\" AlignmentX=\"Center\"", StringComparison.Ordinal) &&
             collection.Contains("x:Name=\"SetBannerImage\"", StringComparison.Ordinal) &&
             collection.Contains("Width=\"0\" Height=\"0\" Opacity=\"0\"", StringComparison.Ordinal),
             "Set tile artwork must fill via ImageBrush without participating in layout size, preserving 196px height.");
        Must(collection.Contains("x:Name=\"PhysicalList\"", StringComparison.Ordinal) &&
             collection.Contains("Header=\"Card / Printing\"", StringComparison.Ordinal) &&
             collection.Contains("Header=\"Arena Card\"", StringComparison.Ordinal) &&
             collection.Contains("AlternationCount=\"2\"", StringComparison.Ordinal) &&
             collection.Contains("ItemsControl.AlternationIndex", StringComparison.Ordinal) &&
             collection.Contains("x:Name=\"DeckLibraryList\"", StringComparison.Ordinal) &&
             collection.Contains("<RadialGradientBrush Center=", StringComparison.Ordinal) &&
             collection.Contains("Text=\"{Binding Format}\"", StringComparison.Ordinal),
             "Collection and deck-library visual parity must preserve virtualized owned rows and themed deck covers.");
        string deckEditor = File.ReadAllText(Path.Combine(
            dir, "NativeDeckEditorWindow.xaml"));
        string deckEditorCode = File.ReadAllText(Path.Combine(
            dir, "NativeDeckEditorWindow.xaml.cs"));
        foreach (string name in new[] {
            "AnalyticsPanel", "AnalyticsCharts", "AnalyticsText",
            "ManaCurveBars", "ColorIdentityBars", "CardTypeBars",
            "AverageManaText", "AnalyzeDeckButton"
        })
            Must(deckEditor.Contains("x:Name=\"" + name + "\"", StringComparison.Ordinal),
                "Deck analytics chart control missing: " + name);
        Must(deckEditor.Contains("x:Key=\"DeckChartBarTemplate\"", StringComparison.Ordinal) &&
             deckEditor.Contains("Maximum=\"{Binding Maximum}\"", StringComparison.Ordinal) &&
             deckEditor.Contains("Value=\"{Binding Count}\"", StringComparison.Ordinal) &&
             deckEditor.Contains("Foreground=\"{Binding Fill}\"", StringComparison.Ordinal) &&
             deckEditor.Contains("IsExpanded=\"False\"", StringComparison.Ordinal) &&
             deckEditorCode.Contains("NativeDeckAnalyticsCharts.FromReport(report)", StringComparison.Ordinal) &&
             deckEditorCode.Contains("InvalidateAnalytics()", StringComparison.Ordinal),
            "Deck chart visuals must bind computed series and hide stale values.");

        foreach (string name in new[] {
            "PreviewCardName", "PreviewOwnership", "PreviewArtwork",
            "PreviewArtFallback", "PreviewMetadata", "PreviewOracleText",
            "PreviewStatus", "OwnedCardsList"
        })
            Must(deckEditor.Contains("x:Name=\"" + name + "\"", StringComparison.Ordinal),
                "Deck Studio preview field missing: " + name);
        Must(deckEditor.Contains("SelectionChanged=\"OnOwnedCardSelectionChanged\"",
                 StringComparison.Ordinal) &&
             deckEditor.Contains("Click=\"OnSortMainboardClicked\"", StringComparison.Ordinal) &&
             deckEditor.Contains("Click=\"OnRemoveOwnedClicked\"", StringComparison.Ordinal) &&
             deckEditorCode.Contains("NativeCardArtwork.LoadAsync(", StringComparison.Ordinal) &&
             deckEditorCode.Contains("NativePrintingArtwork.Read(", StringComparison.Ordinal) &&
             deckEditorCode.Contains("arenaOnly: true", StringComparison.Ordinal) &&
             deckEditorCode.Contains("previewRequest?.Cancel()", StringComparison.Ordinal) &&
             deckEditorCode.Contains("NativeDeckOwnedPicker.RemoveOne(", StringComparison.Ordinal) &&
             deckEditorCode.Contains("NativeDeckOwnedPicker.SortAlphabetically(", StringComparison.Ordinal),
            "Deck preview must be scoped/cancellable, and copy/sort actions must use safe draft helpers.");

        string setSymbolCode = File.ReadAllText(
            Path.Combine(dir, "NativeSetSymbol.cs"));
        Must(setSymbolCode.Contains("svgs.scryfall.io/sets/", StringComparison.Ordinal) &&
             setSymbolCode.Contains("set-symbols", StringComparison.Ordinal),
             "Official set symbols must have a persistent per-user icon cache.");
        // Image view virtualizes rows instead of constructing every image at once.
        Must(main.Contains("CatalogTileRowTemplate", StringComparison.Ordinal) &&
             main.Contains("UniformGrid Rows=\"1\"", StringComparison.Ordinal) &&
             main.Contains("OnCatalogTileMouseDown", StringComparison.Ordinal) &&
             main.Contains("OnCatalogListSizeChanged", StringComparison.Ordinal),
             "Responsive catalog tile grid controls or handlers are missing.");
        Must(CatalogTileLayout.ColumnsForWidth(365) == 1 &&
             CatalogTileLayout.ColumnsForWidth(675) == 2 &&
             CatalogTileLayout.ColumnsForWidth(955) == 3 &&
             CatalogTileLayout.ColumnsForWidth(1600) == 4,
             "Catalog tile grid width breakpoints regressed.");
        var markerCards = Enumerable.Range(1, 8).Cast<object>().ToArray();
        var tileRows = CatalogTileLayout.BuildRows(markerCards, 3);
        Must(tileRows.Count == 3 &&
             tileRows.All(row => row.Cards.Count == 3) &&
             Equals(tileRows[0].Cards[0], 1) &&
             Equals(tileRows[1].Cards[2], 6) &&
             Equals(tileRows[2].Cards[1], 8) &&
             tileRows[2].Cards[2] is null,
             "Tile rows lost card order or final-row padding.");
        Must(tileRows.SelectMany(row => row.Cards).Where(x => x is not null)
                .SequenceEqual(markerCards.Cast<object?>()),
             "Responsive grid must preserve exact printing objects and order.");

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
