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
        // v0.9.26 startup crash: WPF can freeze application resource
        // SolidColorBrush instances. Theme updates must REPLACE resources,
        // never assign Color on an instance or reject IsFrozen.
        var appXaml = File.ReadAllText(Path.Combine(dir, "App.xaml"));
        var themeManagerCode = File.ReadAllText(
            Path.Combine(dir, "VaultThemeManager.cs"));
        var appStartupCode = File.ReadAllText(
            Path.Combine(dir, "App.xaml.cs"));
        Must(!appXaml.Contains("StartupUri=", StringComparison.Ordinal) &&
             appXaml.Contains("ShutdownMode=\"OnMainWindowClose\"",
                 StringComparison.Ordinal) &&
             appStartupCode.Contains("window = new MainWindow();",
                 StringComparison.Ordinal) &&
             appStartupCode.IndexOf("window = new MainWindow();",
                 StringComparison.Ordinal) <
             appStartupCode.IndexOf("NativeUpgradeHandoff.Signal(",
                 StringComparison.Ordinal),
             "Updater must initialize new WPF MainWindow before acknowledging the old app.");
        Must(!themeManagerCode.Contains("brush.IsFrozen", StringComparison.Ordinal) &&
             !themeManagerCode.Contains("change.Brush.Color =", StringComparison.Ordinal) &&
             themeManagerCode.Contains("resources[replacement.Key] = replacement.Brush;",
                 StringComparison.Ordinal),
             "Frozen WPF theme brushes must be replaced rather than mutated.");
        foreach (string filename in Directory.GetFiles(dir, "*.xaml"))
        {
            string xaml = File.ReadAllText(filename);
            foreach (Match match in Regex.Matches(xaml,
                @"\{StaticResource (Vault(?:Background|Sidebar|Panel(?:Raised)?|Line|Text|Muted|Accent(?:Muted|Surface|Border)?|Teal|Good|Warning|Danger))\}"))
                Must(false, "Palette resource must be dynamic to support replacement: " +
                    Path.GetFileName(filename) + " / " + match.Groups[1].Value);
        }

        string mainXaml = File.ReadAllText(Path.Combine(dir, "MainWindow.xaml"));
        string optionsXaml = File.ReadAllText(Path.Combine(dir, "NativeOptionsWindow.xaml"));
        var readinessOptionsCode = File.ReadAllText(
            Path.Combine(dir, "NativeOptionsWindow.xaml.cs"));
        var readinessXaml = File.ReadAllText(
            Path.Combine(dir, "NativeReadinessWindow.xaml"));
        var readinessCode = File.ReadAllText(
            Path.Combine(dir, "NativeReadinessWindow.xaml.cs"));
        Must(readinessXaml.Contains("Click=\"OnCompareBackupClicked\"") &&
             readinessCode.Contains("NativeCollectionIntegrity.Compare(") &&
             readinessCode.Contains("NativeWorkspace.DefaultPath") &&
             readinessCode.Contains("CompareBackupButton.IsEnabled = false;"),
             "Collection comparison must remain an explicit, read-only Options workflow.");
        var starStart = mainXaml.IndexOf("x:Name=\"CardShortlistStar\"",
            StringComparison.Ordinal);
        Must(starStart >= 0 &&
             mainXaml[starStart..Math.Min(starStart + 1050, mainXaml.Length)]
                .Contains("HorizontalContentAlignment=\"Center\"") &&
             mainXaml[starStart..Math.Min(starStart + 1050, mainXaml.Length)]
                .Contains("VerticalContentAlignment=\"Center\""),
             "Catalog favorite-star glyph must be centered within its clickable tile.");

        Must(optionsXaml.Contains("Click=\"OnBackupReadinessClicked\"") &&
             readinessOptionsCode.Contains("new NativeReadinessWindow { Owner = this }") &&
             readinessXaml.Contains("Click=\"OnChooseBackupClicked\"") &&
             readinessXaml.Contains("Click=\"OnCopyReportClicked\"") &&
             readinessCode.Contains("NativeReleaseReadiness.Inspect(") &&
             readinessCode.Contains("Task.Run(") &&
             !readinessCode.Contains("NativeWorkspace.Import") &&
             !readinessCode.Contains("NativeWorkspaceSafety.Save") &&
             !readinessCode.Contains("File.WriteAll"),
             "Backup readiness must be accessible, local and read-only.");
        Must(appXaml.Contains("x:Key=\"VaultThemeBanner\"", StringComparison.Ordinal) &&
             themeManagerCode.Contains("resources[\"VaultThemeBanner\"] = gradient;",
                 StringComparison.Ordinal) &&
             mainXaml.Contains("Background=\"{DynamicResource VaultThemeBanner}\"",
                 StringComparison.Ordinal) &&
             optionsXaml.Contains("SelectedItem.PreviewColors",
                 StringComparison.Ordinal) &&
             optionsXaml.Contains("SelectedItem.Description",
                 StringComparison.Ordinal) &&
             optionsXaml.Contains("Background=\"{DynamicResource VaultThemeBanner}\"",
                 StringComparison.Ordinal),
             "Named theme previews and the actual shell must display matching palette gradients.");
        Must(!mainXaml.Contains("Color=\"#302748\"", StringComparison.Ordinal),
             "Hard-coded purple catalog banner must not override non-purple themes.");

        string depthMainCode = File.ReadAllText(Path.Combine(dir, "MainWindow.xaml.cs"));
        // v0.9.59: the visual-depth pass must be genuinely theme-aware,
        // not hardcoded purple or a heavyweight shadow on every catalog row.
        // Dynamic resources must be replaced atomically when themes switch.
        foreach (string token in new[] { "VaultHeroWash", "VaultSidebarWash",
            "VaultTileWash", "VaultNavHoverWash", "VaultEdgeLift" })
        {
            Must(app.Descendants().Any(x =>
                 x.Attribute(XName.Get("Key", XamlNs))?.Value == token) &&
                 themeManagerCode.Contains("resources[\"" + token + "\"" + "] = ",
                     StringComparison.Ordinal),
                "Visual depth theme token must be declared and updated: " + token);
        }
        Must(appXaml.Contains("x:Key=\"VaultElevatedPanel\"") &&
             appXaml.Contains("x:Key=\"VaultNavButtonDepth\"") &&
             appXaml.Contains("x:Key=\"VaultPanelShadow\"") &&
             mainXaml.Contains("Style=\"{StaticResource VaultCatalogTileStyle}\"") &&
             mainXaml.Contains("Background=\"{DynamicResource VaultHeroWash}\"") &&
             mainXaml.Contains("Background=\"{DynamicResource VaultSidebarWash}\"") &&
             mainXaml.Contains("Style=\"{StaticResource VaultElevatedPanel}\"") &&
             mainXaml.Contains("VaultCatalogTileStyle") &&
             depthMainCode.Contains("FindResource(\"VaultNavButtonDepth\")") &&
             !appXaml.Contains("Foreground\" Value=\"#E5DBFF") &&
             !appXaml.Contains("Foreground\" Value=\"#E5D9FF"),
             "Catalog depth, active navigation and selected text must follow the chosen theme.");
        var visualTile = XDocument.Parse(mainXaml).Descendants()
            .FirstOrDefault(x => x.Name.LocalName == "Style" &&
                x.Attribute(XName.Get("Key", XamlNs))?.Value == "VaultCatalogTileStyle");
        Must(visualTile?.Descendants().Any(x =>
                 x.Name.LocalName == "Trigger" &&
                 x.Attribute("Property")?.Value == "IsMouseOver") == true &&
             visualTile.Elements().Where(x => x.Name.LocalName == "Setter")
                 .All(x => x.Attribute("Property")?.Value != "Effect") &&
             mainXaml.Contains("VirtualizingStackPanel.IsVirtualizing=\"True\""),
             "Card shadows must be hover-only; catalog rows must retain virtualization.");

        var collectionVisual = File.ReadAllText(
            Path.Combine(dir, "NativeCollectionWorkspace.xaml"));
        var labVisual = File.ReadAllText(
            Path.Combine(dir, "CommanderBrowserWindow.xaml"));
        var deckVisual = File.ReadAllText(
            Path.Combine(dir, "NativeDeckEditorWindow.xaml"));
        Must(collectionVisual.Contains("Style=\"{StaticResource VaultElevatedPanel}\"") &&
             collectionVisual.Contains("VaultNavHoverWash") &&
             labVisual.Contains("Background=\"{DynamicResource VaultHeroWash}\"") &&
             deckVisual.Contains("Background=\"{DynamicResource VaultBackground}\"") &&
             deckVisual.Contains("VaultAccentSurface") &&
             optionsXaml.Contains("Background=\"{DynamicResource VaultHeroWash}\""),
             "Collection, EDH Lab, Deck Studio and Appearance previews must share depth tokens.");

        var commanderXaml = File.ReadAllText(
            Path.Combine(dir, "CommanderBrowserWindow.xaml"));
        var deckEditorXaml = File.ReadAllText(
            Path.Combine(dir, "NativeDeckEditorWindow.xaml"));
        var draftEditorCode = File.ReadAllText(
            Path.Combine(dir, "NativeDeckEditorWindow.xaml.cs"));
        var mainShellCode = File.ReadAllText(
            Path.Combine(dir, "MainWindow.xaml.cs"));
        // v0.9.48: the side-by-side Explorer must retain the existing
        // collapsed Improve workspace and never call deck edits from a
        // suggestion inspection or navigation event.
        var explorerUi = XDocument.Parse(deckEditorXaml);
        foreach (string name in new[] { "EdhChainEnablerArt", "EdhChainPayoffArt",
            "EdhChainEnablerButton", "EdhChainPayoffButton", "EdhChainJumpButton" })
            Must(explorerUi.Descendants().Any(x =>
                    x.Attribute(XName.Get("Name", XamlNs))?.Value == name),
                "Chain Explorer is missing UI control: " + name);
        Must(deckEditorXaml.Contains("Click=\"OnEdhInspectEnablerClicked\"") &&
             deckEditorXaml.Contains("Click=\"OnEdhInspectPayoffClicked\"") &&
             deckEditorXaml.Contains("Click=\"OnEdhChainJumpClicked\"") &&
             draftEditorCode.Contains("NativeCardArtwork.IsTrustedArtworkUrl(url)") &&
             draftEditorCode.Contains("arenaOnly: true") &&
             draftEditorCode.Contains("ResetOptimizationBrowsing();") &&
             !draftEditorCode.Contains("OnEdhChainJumpClicked(object sender, RoutedEventArgs e) =>"),
             "Explorer buttons must point to safe inspection and catalog browsing handlers.");
        // Native v0.9.49 three-card pathways must be reachable, read-only
        // and use the existing protected preview and picker flow.
        var threeEngineXaml = XDocument.Parse(deckEditorXaml);
        foreach (string control in new[] { "EdhEnginesList", "EdhEngineDetails",
            "EdhEngineProducerButton", "EdhEngineBridgeButton",
            "EdhEnginePayoffButton", "EdhEngineFindMissingButton" })
            Must(threeEngineXaml.Descendants().Any(x =>
                    x.Attribute(XName.Get("Name", XamlNs))?.Value == control),
                "Three-card engine inspector missing: " + control);
        Must(deckEditorXaml.Contains("Click=\"OnEdhInspectProducerClicked\"") &&
             deckEditorXaml.Contains("Click=\"OnEdhInspectBridgeClicked\"") &&
             deckEditorXaml.Contains("Click=\"OnEdhInspectEnginePayoffClicked\"") &&
             deckEditorXaml.Contains("Click=\"OnEdhEngineFindMissingClicked\"") &&
             draftEditorCode.Contains("EdhLabThreeCardEngines.Analyze") &&
             draftEditorCode.Contains("engine.Status != \"Owned missing link\"") &&
             draftEditorCode.Contains("ResetOptimizationBrowsing();"),
             "Three-card actions must only inspect candidates or navigate to the existing manual picker.");
        // v0.9.50: health checklist is an integrated read-only Improve panel.
        // Its optional browse button must route through the existing picker
        // after revalidating current draft legality and ownership.
        var healthUi = XDocument.Parse(deckEditorXaml);
        foreach (var control in new[] { "EdhHealthHeadline", "EdhHealthOverview",
            "EdhHealthAreasList", "EdhHealthAreaDetails", "EdhHealthNextStep",
            "EdhHealthBrowseButton" })
            Must(healthUi.Descendants().Any(x =>
                    x.Attribute(XName.Get("Name", XamlNs))?.Value == control),
                "Deck health panel control missing: " + control);
        Must(deckEditorXaml.Contains("SelectionChanged=\"OnEdhHealthSelectionChanged\"") &&
             deckEditorXaml.Contains("Click=\"OnEdhHealthBrowseClicked\"") &&
             draftEditorCode.Contains("EdhLabDeckHealth.Analyze") &&
             draftEditorCode.Contains("if (!fresh.SafeToSuggest") &&
             draftEditorCode.Contains("ResetOptimizationBrowsing();") &&
             draftEditorCode.Contains("EdhWorkspaceTabs.SelectedItem = EdhCardsTab;"),
             "Deck health interactions must refresh and use the protected manual card browser.");
        // v0.9.51: repeatedly typing into a live EDH draft should coalesce
        // catalog analysis. Stale actions must be disabled before the next
        // queued pass; closing the editor cancels both timers/artwork.
        Must(draftEditorCode.Contains("EdhAnalysisQuietMilliseconds = 220") &&
             draftEditorCode.Contains("private void ScheduleEdhAnalysis()") &&
             draftEditorCode.Contains("edhAnalysisTimer.Stop();") &&
             draftEditorCode.Contains("edhAnalysisTimer.Start();") &&
             draftEditorCode.Contains("if (IsLoaded && !busy)") &&
             draftEditorCode.Contains("EdhApplySwapButton.IsEnabled = false;") &&
             draftEditorCode.Contains("EdhHealthBrowseButton.IsEnabled = false;") &&
             draftEditorCode.Contains("EdhChainJumpButton.IsEnabled = false;") &&
             draftEditorCode.Contains("EdhEngineFindMissingButton.IsEnabled = false;") &&
             draftEditorCode.Contains("edhAnalysisTimer?.Stop();") &&
             draftEditorCode.Contains("chainArtworkRequest?.Cancel();"),
             "EDH typing must debounce read-only work, disarm stale actions and cancel on close.");
        var draftEditStart = draftEditorCode.IndexOf("private void OnDraftChanged(",
            StringComparison.Ordinal);
        var draftEditEnd = draftEditorCode.IndexOf("private void OnFormatChanged(",
            StringComparison.Ordinal);
        Must(draftEditStart >= 0 && draftEditEnd > draftEditStart &&
             draftEditorCode[draftEditStart..draftEditEnd]
                .Contains("ScheduleEdhAnalysis();") &&
             draftEditorCode[draftEditStart..draftEditEnd]
                .Contains("RefreshPreviewOwnership();"),
             "Deferred analysis must retain immediate mainboard and preview ownership updates.");
        // Regression: v0.9.32 shipped the EDH Lab builder but hid its only
        // launcher with Visibility=Collapsed, making it inaccessible.
        var edhLauncher = XDocument.Parse(mainXaml).Descendants()
            .SingleOrDefault(x => x.Attribute(XName.Get("Name", XamlNs))?.Value ==
                "EdhLabButton");
        Must(edhLauncher is not null &&
             edhLauncher.Name.LocalName == "Button" &&
             edhLauncher.Attribute("Visibility")?.Value is not "Collapsed" and not "Hidden" &&
             edhLauncher.Attribute("Click")?.Value == "OnEdhLabClicked" &&
             mainXaml.IndexOf("x:Name=\"DeckLibraryNavigationButton\"",
                 StringComparison.Ordinal) <
             mainXaml.IndexOf("x:Name=\"EdhLabButton\"",
                 StringComparison.Ordinal) &&
             mainXaml.IndexOf("x:Name=\"EdhLabButton\"",
                 StringComparison.Ordinal) <
             mainXaml.IndexOf("x:Name=\"SetProgressNavigationButton\"",
                 StringComparison.Ordinal),
             "EDH Lab must remain a visible, accessible sidebar workspace.");
        Must(Regex.IsMatch(mainShellCode,
                 @"catalogSnapshot = persisted;[\s\S]{0,220}EdhLabButton\.IsEnabled = true;") &&
             Regex.IsMatch(mainShellCode,
                 @"catalogSnapshot = fresh;[\s\S]{0,220}EdhLabButton\.IsEnabled = true;") &&
             !mainShellCode.Contains("EdhLabButton.IsEnabled = false; // EDH Lab remains backlog",
                 StringComparison.Ordinal),
             "EDH Lab launch must be enabled after loading or syncing a real Scryfall catalog.");
        Must(commanderXaml.Contains("x:Name=\"BuildDeckButton\"",
                 StringComparison.Ordinal) &&
             commanderXaml.Contains("Click=\"OnBuildDeckClicked\"",
                 StringComparison.Ordinal) &&
             commanderXaml.Contains("SelectionChanged=\"OnCommanderSelected\"",
                 StringComparison.Ordinal) &&
             mainShellCode.Contains("draftCommander: commander.Name",
                 StringComparison.Ordinal) &&
             mainShellCode.Contains("workspace.Collection, active.ReadOnlySnapshotPath",
                 StringComparison.Ordinal) &&
             mainShellCode.Contains("currentVersion != modeVersion",
                 StringComparison.Ordinal) &&
             mainShellCode.Contains("await RefreshNativeWorkspaceAsync();",
                 StringComparison.Ordinal),
             "EDH Lab Build must launch shared Deck Studio and refresh verified library on save.");
        // A new tabbed layout keeps the card list full-height and puts
        // analysis/improvements in independent scrollable workspaces.
        var editorDoc = XDocument.Parse(deckEditorXaml);
        var tabs = editorDoc.Descendants().SingleOrDefault(x =>
            x.Attribute(XName.Get("Name", XamlNs))?.Value == "EdhWorkspaceTabs");
        static string? NearestTab(XElement element) =>
            element.Ancestors().FirstOrDefault(x => x.Name.LocalName == "TabItem")
                ?.Attribute(XName.Get("Name", XamlNs))?.Value;
        XElement NamedEditor(string name) =>
            editorDoc.Descendants().Single(x =>
                x.Attribute(XName.Get("Name", XamlNs))?.Value == name);
        Must(tabs is not null && tabs.Name.LocalName == "TabControl" &&
             NearestTab(NamedEditor("OwnedCardsList")) == "EdhCardsTab" &&
             NearestTab(NamedEditor("EdhRoleBox")) == "EdhCardsTab" &&
             NearestTab(NamedEditor("EdhManaDetails")) == "EdhManaTab" &&
             NearestTab(NamedEditor("EdhCurveDetails")) == "EdhManaTab" &&
             NearestTab(NamedEditor("EdhSwapProposalsList")) == "EdhImproveTab" &&
             NearestTab(NamedEditor("EdhOptimizationDetails")) == "EdhImproveTab" &&
             NearestTab(NamedEditor("EdhManaScroll")) == "EdhManaTab" &&
             NearestTab(NamedEditor("EdhImproveScroll")) == "EdhImproveTab" &&
             NamedEditor("EdhManaScroll").Attribute("VerticalScrollBarVisibility")?.Value == "Visible" &&
             NamedEditor("EdhImproveScroll").Attribute("VerticalScrollBarVisibility")?.Value == "Visible" &&
             tabs.Attribute("SizeChanged")?.Value == "OnEdhWorkspaceSizeChanged" &&
             tabs.Attribute("Loaded")?.Value == "OnEdhWorkspaceLoaded" &&
             draftEditorCode.Contains("ConstrainEdhWorkspaceScrollers()", StringComparison.Ordinal) &&
             draftEditorCode.Contains("EdhManaScroll.MaxHeight = viewport;", StringComparison.Ordinal) &&
             draftEditorCode.Contains("EdhImproveScroll.MaxHeight = viewport;", StringComparison.Ordinal) &&
             NamedEditor("EdhManaTab").Ancestors().Any(x =>
                 x.Name.LocalName == "TabControl") &&
             deckEditorXaml.Contains("x:Name=\"EdhCardsFilters\"") &&
             deckEditorXaml.Contains("ResizeBehavior=\"PreviousAndNext\"") &&
             draftEditorCode.Contains("EdhWorkspaceTabs.SelectedItem = EdhCardsTab;",
                 StringComparison.Ordinal) &&
             draftEditorCode.Contains("EdhImproveTab.Visibility = Visibility.Visible;",
                 StringComparison.Ordinal),
             "EDH Lab navigation must separate full-height Cards, Mana and Improve workspaces, with routed card browsing.");

        // The editor's mainboard remains a normal editable TextBox, while
        // selecting a valid line drives the same read-only card preview used
        // by the Cards tab. No list filter/selection is required.
        var board = NamedEditor("MainboardBox");
        Must(board.Name.LocalName == "TextBox" &&
             board.Attribute("SelectionChanged")?.Value == "OnMainboardSelectionChanged" &&
             board.Attribute("GotKeyboardFocus")?.Value == "OnMainboardGotKeyboardFocus" &&
             board.Attribute("PreviewMouseLeftButtonUp")?.Value == "OnMainboardMouseReleased" &&
             draftEditorCode.Contains("NativeDeckEditing.PreviewNameAtCaret(",
                 StringComparison.Ordinal) &&
             draftEditorCode.Contains("NativeDeckOwnedPicker.CountOwned(",
                 StringComparison.Ordinal) &&
             draftEditorCode.Contains("OwnedCardsList?.UnselectAll();",
                 StringComparison.Ordinal) &&
             draftEditorCode.Contains("ShowCardPreviewAsync(", StringComparison.Ordinal) &&
             draftEditorCode.Contains("previewedCard is not OwnedDisplay card",
                 StringComparison.Ordinal) &&
             draftEditorCode.Contains("if (OwnedCardsList?.SelectedItem is OwnedDisplay card)",
                 StringComparison.Ordinal),
             "Mainboard click, keyboard selection and ownership must share a read-only preview without depending on the Cards tab.");

        var reconciliationXaml = File.ReadAllText(
            Path.Combine(dir, "NativeImportDiffWindow.xaml"));
        var reconciliationCode = File.ReadAllText(
            Path.Combine(dir, "NativeImportDiffWindow.xaml.cs"));
        var workspaceCode = File.ReadAllText(
            Path.Combine(dir, "NativeCollectionWorkspace.xaml.cs"));
        Must(reconciliationXaml.Contains("x:Name=\"ChangesTable\"",
                 StringComparison.Ordinal) &&
             reconciliationXaml.Contains("Binding=\"{Binding Delta}\"",
                 StringComparison.Ordinal) == false &&
             reconciliationXaml.Contains("Binding=\"{Binding ChangeLabel}\"",
                 StringComparison.Ordinal) &&
             reconciliationXaml.Contains("Click=\"OnApplyClicked\"",
                 StringComparison.Ordinal) &&
             reconciliationCode.Contains("ApplyButton.IsEnabled = report.HasChanges &&",
                 StringComparison.Ordinal) &&
             reconciliationCode.Contains("report.Audit.UnresolvedRows == 0",
                 StringComparison.Ordinal) &&
             reconciliationCode.Contains("UnresolvedAcknowledged.IsChecked == true",
                 StringComparison.Ordinal) &&
             workspaceCode.Contains("PreviewPhysicalReconciliation(",
                 StringComparison.Ordinal) &&
             workspaceCode.Contains("ReconcilePhysicalImport(",
                 StringComparison.Ordinal) &&
             workspaceCode.Contains("PreviewArenaReconciliation(",
                 StringComparison.Ordinal) &&
             workspaceCode.Contains("ReconcileArenaImport(",
                 StringComparison.Ordinal) &&
             !workspaceCode.Contains("Reimporting the same file will ADD",
                 StringComparison.Ordinal),
             "CSV/Excel physical and Arena imports must show an auditable quantity diff and commit only reviewed changes.");

        Must(deckEditorXaml.Contains("x:Name=\"EdhRecordedOnly\"",
                 StringComparison.Ordinal) &&
             deckEditorXaml.Contains("x:Name=\"EdhRoleBox\"",
                 StringComparison.Ordinal) &&
             deckEditorXaml.Contains("x:Name=\"EdhLegalityText\"",
                 StringComparison.Ordinal) &&
             deckEditorXaml.Contains("x:Name=\"EdhBlueprintText\"",
                 StringComparison.Ordinal) &&
             deckEditorXaml.Contains("Click=\"OnEdhFindGapClicked\"",
                 StringComparison.Ordinal) &&
             deckEditorXaml.Contains("x:Name=\"EdhPrioritizeSynergy\"",
                 StringComparison.Ordinal) &&
             deckEditorXaml.Contains("x:Name=\"EdhSynergyOnly\"",
                 StringComparison.Ordinal) &&
             deckEditorXaml.Contains("x:Name=\"EdhSynergyStatus\"",
                 StringComparison.Ordinal) &&
             deckEditorXaml.Contains("x:Name=\"PreviewSynergyText\"",
                 StringComparison.Ordinal) &&
             deckEditorXaml.Contains("x:Name=\"EdhAutoBalanceLands\"",
                 StringComparison.Ordinal) &&
             deckEditorXaml.Contains("x:Name=\"EdhLandTargetBox\"",
                 StringComparison.Ordinal) &&
             deckEditorXaml.Contains("x:Name=\"EdhWeightSpellPips\"",
                 StringComparison.Ordinal) &&
             deckEditorXaml.Contains("Checked=\"OnEdhPipModeChanged\"",
                 StringComparison.Ordinal) &&
             draftEditorCode.Contains("weightSpellPips: EdhWeightSpellPips",
                 StringComparison.Ordinal) &&
             deckEditorXaml.Contains("x:Name=\"EdhManaDetails\"",
                 StringComparison.Ordinal) &&
             deckEditorXaml.Contains("x:Name=\"EdhCurveHeadline\"",
                 StringComparison.Ordinal) &&
             deckEditorXaml.Contains("x:Name=\"EdhCurveDetails\"",
                 StringComparison.Ordinal) &&
             deckEditorXaml.Contains("Mana curve &amp; casting consistency",
                 StringComparison.Ordinal) &&
             deckEditorXaml.Contains("x:Name=\"EdhStrategyFocusBox\"",
                 StringComparison.Ordinal) &&
             deckEditorXaml.Contains("SelectionChanged=\"OnEdhStrategyFocusChanged\"",
                 StringComparison.Ordinal) &&
             deckEditorXaml.Contains("x:Name=\"EdhStrategyExplanation\"",
                 StringComparison.Ordinal) &&
             deckEditorXaml.Contains("Tag=\"FastStart\"",
                 StringComparison.Ordinal) &&
             deckEditorXaml.Contains("Tag=\"CommanderSynergy\"",
                 StringComparison.Ordinal) &&
             draftEditorCode.Contains("focus: SelectedStrategyFocus",
                 StringComparison.Ordinal) &&
             draftEditorCode.Contains("EdhStrategyFocusBox.SelectedIndex = 0;",
                 StringComparison.Ordinal) &&
             draftEditorCode.Contains("EdhLabStrategyGoals.Description(",
                 StringComparison.Ordinal) &&
             deckEditorXaml.Contains("x:Name=\"EdhOptimizationHeadline\"",
                 StringComparison.Ordinal) &&
             deckEditorXaml.Contains("x:Name=\"EdhOptimizationDetails\"",
                 StringComparison.Ordinal) &&
             deckEditorXaml.Contains("x:Name=\"EdhBrowseImprovementsButton\"",
                 StringComparison.Ordinal) &&
             deckEditorXaml.Contains("Click=\"OnEdhBrowseImprovementsClicked\"",
                 StringComparison.Ordinal) &&
             deckEditorXaml.Contains("x:Name=\"EdhSwapProposalsList\"",
                 StringComparison.Ordinal) &&
             deckEditorXaml.Contains("SelectionChanged=\"OnEdhSwapSelectionChanged\"",
                 StringComparison.Ordinal) &&
             deckEditorXaml.Contains("x:Name=\"EdhApplySwapButton\"",
                 StringComparison.Ordinal) &&
             deckEditorXaml.Contains("Click=\"OnEdhApplySwapClicked\"",
                 StringComparison.Ordinal) &&
             draftEditorCode.Contains("EdhLabSwapAdvisor.Analyze(",
                 StringComparison.Ordinal) &&
             draftEditorCode.Contains("EdhLabSwapAdvisor.IsCurrent(",
                 StringComparison.Ordinal) &&
             draftEditorCode.Contains("MessageBoxButton.YesNo, MessageBoxImage.Question",
                 StringComparison.Ordinal) &&
             draftEditorCode.Contains("NativeDeckOwnedPicker.RemoveOne(",
                 StringComparison.Ordinal) &&
             draftEditorCode.Contains("NativeDeckOwnedPicker.AddOne(",
                 StringComparison.Ordinal) &&
             draftEditorCode.Contains("EdhLabDeckOptimizer.Analyze(",
                 StringComparison.Ordinal) &&
             draftEditorCode.Contains("browsingOptimizationCandidates",
                 StringComparison.Ordinal) &&
             draftEditorCode.Contains("ResetOptimizationBrowsing()",
                 StringComparison.Ordinal) &&
             draftEditorCode.Contains("EdhLabCurveAnalysis.Analyze(",
                 StringComparison.Ordinal) &&
             draftEditorCode.Contains("RefreshCurveAdvice(entries, plan);",
                 StringComparison.Ordinal) &&
             deckEditorXaml.Contains("Click=\"OnEdhBalanceNowClicked\"",
                 StringComparison.Ordinal) &&
             draftEditorCode.Contains("EdhLabManaPlanner.Analyze(",
                 StringComparison.Ordinal) &&
             draftEditorCode.Contains("EdhLabManaPlanner.Apply(",
                 StringComparison.Ordinal) &&
             draftEditorCode.Contains("EdhAutoBalanceLands.IsChecked = selected is null",
                 StringComparison.Ordinal) &&
             draftEditorCode.Contains("if (applyingManaBalance) return;",
                 StringComparison.Ordinal) &&
             draftEditorCode.Contains("SynergyExplanation: fit?.Why",
                 StringComparison.Ordinal) &&
             draftEditorCode.Contains("synergyOnly: EdhSynergyOnly",
                 StringComparison.Ordinal) &&
             draftEditorCode.Contains("EdhLabDeckBlueprint.Analyze(",
                 StringComparison.Ordinal) &&
             draftEditorCode.Contains("EdhLabDraftCatalog.OpenReadOnly(",
                 StringComparison.Ordinal) &&
             draftEditorCode.Contains("NativeDeckPreflight.Check(",
                 StringComparison.Ordinal) &&
             draftEditorCode.Contains("NativeWorkspaceSafety.SaveDeck(",
                 StringComparison.Ordinal),
             "EDH Lab needs catalog-filtered suggestions, missing/legality warnings and protected deck saves.");


        var deckLibraryXaml = File.ReadAllText(
            Path.Combine(dir, "NativeCollectionWorkspace.xaml"));
        var deckLibraryCode = File.ReadAllText(
            Path.Combine(dir, "NativeCollectionWorkspace.xaml.cs"));
        Must(deckLibraryXaml.Contains("x:Name=\"EditDeckInEdhLabButton\"",
                 StringComparison.Ordinal) &&
             deckLibraryXaml.Contains("Click=\"OnEditDeckInEdhLabClicked\"",
                 StringComparison.Ordinal) &&
             deckLibraryCode.Contains("CommanderCatalog.OpenReadOnly(catalogPath!)",
                 StringComparison.Ordinal) &&
             deckLibraryCode.Contains("EdhLabSavedDeckAccess.FindEligibleCommander(",
                 StringComparison.Ordinal) &&
             deckLibraryCode.Contains("savedLabFormat: format",
                 StringComparison.Ordinal) &&
             draftEditorCode.Contains("savedLabFormat is not null ? selected.Commander",
                 StringComparison.Ordinal) &&
             draftEditorCode.Contains("HeaderText.Text = selected is null",
                 StringComparison.Ordinal) &&
             draftEditorCode.Contains("NativeWorkspaceSafety.SaveDeck(",
                 StringComparison.Ordinal),
             "Saved Commander/Brawl decks must opt into verified EDH Lab without replacing native save or metadata.");

        var commanderCode = File.ReadAllText(
            Path.Combine(dir, "CommanderBrowserWindow.xaml.cs"));
        Must(commanderXaml.Contains("x:Name=\"RecordedOnly\"", StringComparison.Ordinal) &&
             commanderXaml.Contains("Text=\"{Binding InventoryText}\"",
                 StringComparison.Ordinal) &&
             commanderXaml.Contains("Background=\"{DynamicResource VaultBackground}\"",
                 StringComparison.Ordinal) &&
             commanderXaml.Contains("x:Name=\"PreviousButton\"", StringComparison.Ordinal) &&
             commanderXaml.Contains("x:Name=\"NextButton\"", StringComparison.Ordinal),
             "EDH Lab needs a theme-aware, bounded, inventory-aware commander browser.");
        Must(commanderCode.Contains("inventory.Filter(", StringComparison.Ordinal) &&
             commanderCode.Contains("RecordedOnly.IsEnabled = hasCollection",
                 StringComparison.Ordinal),
             "EDH Lab ownership filtering must be disabled when no verified collection exists.");

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
            "NativeDeckEditorWindow", "NativeImportDiffWindow", "NativeAddPrintingWindow",
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

            // StaticResource may be defined locally within a Window or
            // UserControl rather than in App.xaml. Keep validation strict,
            // but accept resources available in the owning XAML document.
            var localKeys = doc.Descendants()
                .Select(e => e.Attribute(XName.Get("Key", XamlNs))?.Value)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.Ordinal);
            foreach (Match match in Regex.Matches(xml,
                @"\{StaticResource (Vault[a-zA-Z0-9]+)\}"))
                Must(keys.Contains(match.Groups[1].Value) ||
                     localKeys.Contains(match.Groups[1].Value),
                    name + " references undefined theme resource " + match.Groups[1].Value);

            foreach (Match match in Regex.Matches(xml,
                "(?:Click|Checked|Unchecked|TextChanged|SelectionChanged|" +
                "MouseDoubleClick|KeyDown|PasswordChanged)=\"(On[A-Za-z0-9_]+)\""))
                Must(code.Contains(match.Groups[1].Value + "(", StringComparison.Ordinal),
                    name + " XAML references missing event handler " +
                    match.Groups[1].Value);
        }

        string main = File.ReadAllText(Path.Combine(dir, "MainWindow.xaml"));
        string optionsXml = File.ReadAllText(Path.Combine(dir, "NativeOptionsWindow.xaml"));
        var optionsDoc = XDocument.Parse(optionsXml);
        foreach (var id in new[] { "ThemeChoice", "ThemeStatus" })
            Must(optionsDoc.Descendants().Any(x =>
                x.Attribute(XName.Get("Name", XamlNs))?.Value == id),
                "Appearance screen missing live theme control " + id);
        string optionsCode = File.ReadAllText(
            Path.Combine(dir, "NativeOptionsWindow.xaml.cs"));
        Must(optionsCode.Contains("NativeThemeSettings.Save(", StringComparison.Ordinal) &&
             optionsCode.Contains("VaultThemeManager.Apply(", StringComparison.Ordinal) &&
             optionsCode.Contains("OnCancelClicked(", StringComparison.Ordinal),
             "Native appearance screen must save, preview and revert settings.");
        Must(main.Contains("Click=\"OnOpenAppearanceClicked\"", StringComparison.Ordinal),
             "Options sidebar must expose the theme screen.");



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
        // v0.9.55: selective inventory clearing is an explicit action
        // inside the live native workspace, never a preview/destructive
        // one-click reset. Require both typed phrase and acknowledgement.
        var clearUi = XDocument.Load(
            Path.Combine(dir, "NativeCollectionClearWindow.xaml"));
        var clearCode = File.ReadAllText(
            Path.Combine(dir, "NativeCollectionClearWindow.xaml.cs"));
        Must(collection.Contains("x:Name=\"ClearInventoryButton\"") &&
             collection.Contains("Click=\"OnClearInventoryClicked\"") &&
             clearUi.Descendants().Any(x =>
                 x.Attribute(XName.Get("Name", XamlNs))?.Value == "ConfirmationText") &&
             clearUi.Descendants().Any(x =>
                 x.Attribute(XName.Get("Name", XamlNs))?.Value == "Acknowledged") &&
             clearCode.Contains("\"CLEAR PHYSICAL\"") &&
             clearCode.Contains("\"CLEAR ARENA\"") &&
             clearCode.Contains("\"CLEAR BOTH\"") &&
             clearCode.Contains("Acknowledged.IsChecked == true") &&
             clearCode.Contains("ConfirmButton.IsEnabled") &&
             !clearCode.Contains("StartFresh("),
             "Inventory clear must require an explicit scope, phrase and acknowledgement.");
        // v0.9.56: regression for the invisible backup acknowledgement
        // (the old star-sized row collapsed to zero below the typed phrase).
        // Confirmation and action buttons must be pinned OUTSIDE the scroll area.
        var clearLayout = clearUi.Root?.Elements()
            .FirstOrDefault(x => x.Name.LocalName == "Grid");
        var clearRows = clearLayout?.Elements()
            .FirstOrDefault(x => x.Name.LocalName == "Grid.RowDefinitions")?
            .Elements().ToArray();
        var clearScroll = clearLayout?.Elements()
            .FirstOrDefault(x => x.Name.LocalName == "ScrollViewer" &&
                x.Attribute("Grid.Row")?.Value == "0");
        var clearPinnedPanel = clearLayout?.Elements()
            .FirstOrDefault(x => x.Name.LocalName == "Border" &&
                x.Attribute("Grid.Row")?.Value == "1");
        var clearPinnedAck = clearPinnedPanel?.Descendants()
            .FirstOrDefault(x =>
                x.Attribute(XName.Get("Name", XamlNs))?.Value == "Acknowledged");
        var clearAckText = clearPinnedAck?.Descendants()
            .FirstOrDefault(x => x.Name.LocalName == "TextBlock");
        var clearActions = clearLayout?.Elements()
            .FirstOrDefault(x => x.Name.LocalName == "StackPanel" &&
                x.Attribute("Grid.Row")?.Value == "2");
        Must(clearRows?.Length == 3 &&
             clearRows[0].Attribute("Height")?.Value == "*" &&
             clearRows[1].Attribute("Height")?.Value == "Auto" &&
             clearRows[2].Attribute("Height")?.Value == "Auto" &&
             clearScroll?.Attribute("VerticalScrollBarVisibility")?.Value == "Auto" &&
             clearScroll.Descendants().Any(x =>
                 x.Attribute(XName.Get("Name", XamlNs))?.Value == "ConfirmationText") &&
             clearPinnedAck is not null &&
             clearAckText?.Attribute("TextWrapping")?.Value == "Wrap" &&
             clearAckText.Attribute("Width") is null &&
             clearActions?.Descendants().Any(x =>
                 x.Attribute(XName.Get("Name", XamlNs))?.Value == "ConfirmButton") == true,
             "Clear inventory must always expose required acknowledgement and buttons, with only the upper form scrolling.");
        string collectionCode = File.ReadAllText(
            Path.Combine(dir, "NativeCollectionWorkspace.xaml.cs"));
        // v0.9.57: source audit must be reviewable/exportable before
        // committing, and unresolved physical printings require explicit
        // acknowledgement rather than being silently treated as verified.
        var importUi = XDocument.Load(
            Path.Combine(dir, "NativeImportDiffWindow.xaml"));
        string importCode = File.ReadAllText(
            Path.Combine(dir, "NativeImportDiffWindow.xaml.cs"));
        Must(importUi.Descendants().Any(x =>
                 x.Attribute(XName.Get("Name", XamlNs))?.Value == "AuditText") &&
             importUi.Descendants().Any(x =>
                 x.Attribute(XName.Get("Name", XamlNs))?.Value == "UnresolvedAcknowledged") &&
             importUi.Descendants().Any(x =>
                 x.Attribute("Click")?.Value == "OnExportAuditClicked") &&
             importCode.Contains("report.Audit.Display") &&
             importCode.Contains("report.Audit.UnresolvedRows == 0") &&
             importCode.Contains("UnresolvedAcknowledged.IsChecked == true") &&
             importCode.Contains("Source SHA-256:") &&
             importCode.Contains("Proposed candidate SHA-256:") &&
             collectionCode.Contains("ImportFileSha256(picker.FileName)") &&
             collectionCode.Contains("The source file changed since audit review") &&
             collectionCode.Contains("diff.Audit.Display"),
             "CSV/XLSX review must show source and identity audit, export it, require unresolved acknowledgement and reject modified source files.");
        // v0.9.54: the ownership summary belongs under the artwork/
        // printings pane. The card text keeps its own star-sized row, and
        // long metadata/legalities remain independently scrollable.
        var cardDetailsLayout = XDocument.Load(
            Path.Combine(dir, "NativeCardDetailsWindow.xaml"));
        var detailsGrid = cardDetailsLayout.Descendants()
            .FirstOrDefault(x => x.Name.LocalName == "Grid" &&
                x.Elements().Any(y => y.Name.LocalName == "ScrollViewer" &&
                    y.Attribute("Grid.Column")?.Value == "0"));
        Must(detailsGrid is not null,
            "Card Details must retain separate artwork and text columns.");
        var leftScroll = detailsGrid.Elements().FirstOrDefault(x =>
            x.Name.LocalName == "ScrollViewer" &&
            x.Attribute("Grid.Column")?.Value == "0");
        var rightGrid = detailsGrid.Elements().FirstOrDefault(x =>
            x.Name.LocalName == "Grid" &&
            x.Attribute("Grid.Column")?.Value == "2");
        Must(leftScroll?.Descendants().Any(x =>
                x.Attribute(XName.Get("Name", XamlNs))?.Value == "Ownership") == true &&
             rightGrid is not null &&
             !rightGrid.Descendants().Any(x =>
                x.Attribute(XName.Get("Name", XamlNs))?.Value == "Ownership") &&
             rightGrid.Descendants().Any(x =>
                x.Attribute(XName.Get("Name", XamlNs))?.Value == "OracleText") &&
             rightGrid.Elements().Any(x =>
                x.Name.LocalName == "Border" &&
                x.Attribute("Grid.Row")?.Value == "0" &&
                x.Attribute("MaxHeight")?.Value == "230") &&
             rightGrid.Elements().Any(x =>
                x.Name.LocalName == "Border" &&
                x.Attribute("Grid.Row")?.Value == "1" &&
                x.Attribute("MinHeight")?.Value == "190"),
             "Ownership must sit below artwork; Oracle text must retain a minimum height independently of long legalities.");
        Must(collectionCode.Contains("NativeWorkspaceSafety.ClearInventory(") &&
             collectionCode.Contains("expectedBackupSha") &&
             collectionCode.Contains("outcome.RecoveryPath") &&
             collectionCode.Contains("if (prompt.ShowDialog() != true) return"),
             "Inventory clearing must use SHA-guarded selective recovery, never clear the entire workspace.");
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
        // v0.9.58: catalog search cannot terminate at the first 200 matches.
        // Paging controls remain below the virtualized results in all 3 views.
        var catalogLayout = XDocument.Parse(main);
        var catalogPager = catalogLayout.Descendants()
            .FirstOrDefault(x => x.Attribute(XName.Get("Name", XamlNs))?.Value ==
                "CatalogPageStatus");
        Must(catalogPager is not null &&
             main.Contains("PreviousCatalogPageButton") &&
             main.Contains("NextCatalogPageButton") &&
             main.Contains("CatalogPageLabel") &&
             main.Contains("OnPreviousCatalogPageClicked") &&
             main.Contains("OnNextCatalogPageClicked") &&
             mainCode.Contains("private const int CatalogPageSize = 200;") &&
             mainCode.Contains("offset: checked(catalogPageIndex * CatalogPageSize)") &&
             mainCode.Contains("FilterAsync(debounce: false, resetPage: false)") &&
             mainCode.Contains("if (resetPage) catalogPageIndex = 0;") &&
             mainCode.Contains("SetCatalogPageState(results.Rows.Count, results.MayHaveMore)") &&
             !mainCode.Contains("First 200 matching results") &&
             main.Contains("VirtualizingStackPanel.IsVirtualizing"),
             "All catalog pages must remain accessible with stable back/next controls and virtualized views.");

        // Image view virtualizes rows instead of constructing every image at once.
        Must(main.Contains("CatalogTileRowTemplate", StringComparison.Ordinal) &&
             main.Contains("UniformGrid Rows=\"1\"", StringComparison.Ordinal) &&
             main.Contains("OnCatalogTileMouseDown", StringComparison.Ordinal) &&
             main.Contains("OnCatalogListSizeChanged", StringComparison.Ordinal),
             "Responsive catalog tile grid controls or handlers are missing.");
        Must(main.Contains("x:Name=\"CardShortlistStar\"", StringComparison.Ordinal) &&
             main.Contains("Click=\"OnCatalogShortlistStarClicked\"", StringComparison.Ordinal) &&
             main.Contains("Content=\"{Binding ShortlistStar}\"", StringComparison.Ordinal) &&
             main.Contains("ToolTip=\"{Binding ShortlistTooltip}\"", StringComparison.Ordinal) &&
             !main.Contains("View selected card  ›", StringComparison.Ordinal) &&
             !main.Contains("x:Name=\"AddShortlistButton\"", StringComparison.Ordinal) &&
             !main.Contains("Text=\"CATALOG STATUS\"", StringComparison.Ordinal) &&
             main.Contains("x:Name=\"FilterStatus\"", StringComparison.Ordinal),
             "Card art must provide inline bookmark control while preserving compact status text.");
        Must(mainCode.Contains("OnCatalogShortlistStarClicked(", StringComparison.Ordinal) &&
             mainCode.Contains("e.Handled = true;", StringComparison.Ordinal) &&
             mainCode.Contains("NativeWorkspaceSafety.SetShortlistCard(", StringComparison.Ordinal) &&
             mainCode.Contains("bookmarkedCatalogNames.Contains(card.Name)", StringComparison.Ordinal),
             "Shortlist star must route to the verified native bookmark save and refresh state.");
        var printing = new CatalogSnapshot.PrintingRow("Sol Ring", "CMM", "396");
        Must(printing.ShortlistStar == "☆" && !printing.IsShortlisted &&
             (printing with { IsShortlisted = true }).ShortlistStar == "★" &&
             (printing with { IsShortlisted = true }).Name == printing.Name,
             "Bookmark UI state must never modify catalog printing identity.");

        Must(CatalogTileLayout.ColumnsForWidth(365) == 1 &&
             CatalogTileLayout.ColumnsForWidth(675) == 2 &&
             CatalogTileLayout.ColumnsForWidth(955) == 4 &&
             CatalogTileLayout.ColumnsForWidth(1600) == 5 &&
             CatalogTileLayout.ColumnsForWidth(double.NaN) == 2,
             "Compact Catalog tile grid width breakpoints regressed.");
        Must(main.Contains("Height=\"366\"", StringComparison.Ordinal) &&
             main.Contains("Width=\"200\" Height=\"278\"", StringComparison.Ordinal) &&
             main.Contains("ArtUrl=\"{Binding ImageUrl}\"", StringComparison.Ordinal) &&
             main.Contains("DecodeWidth=\"320\"", StringComparison.Ordinal) &&
             main.Contains("Text=\"{Binding RarityLabel}\"", StringComparison.Ordinal) &&
             main.Contains("PreviewMouseLeftButtonDown=\"OnCatalogTileMouseDown\"", StringComparison.Ordinal),
             "Card-first tile must devote most available space to art, keep rarity and preserve card selection.");
        Must(!main.Contains("YOUR COLLECTION. YOUR RULES.", StringComparison.Ordinal) &&
             !main.Contains("Every printing tells a story.", StringComparison.Ordinal) &&
             main.Contains("Grid x:Name=\"CatalogWorkspace\"") &&
             main.Contains("Arena catalog (one per card)") &&
             main.Contains("This is NOT your owned Arena inventory") &&
             mainCode.Contains("ArenaOnlyBox?.IsChecked == true"),
             "Catalog must remove promotional hero and clarify that Arena availability differs from Arena ownership.");
        var detailsXaml = File.ReadAllText(Path.Combine(dir, "NativeCardDetailsWindow.xaml"));
        // Reuse the earlier card-details code snapshot.
        var manaCode = File.ReadAllText(Path.Combine(dir, "NativeManaSymbol.cs"));
        Must(detailsXaml.Contains("ColorIdentitySymbols") &&
             detailsXaml.Contains("ColorIdentityLabel") &&
             detailsXaml.Contains("local:NativeManaSymbol") &&
             detailsXaml.Contains("Height=\"445\"") &&
             detailsXaml.Contains("CardRarityBadge") &&
             detailsCode.Contains("NativeManaIdentity.ParseColorIdentity(info.ColorIdentity)") &&
             detailsCode.Contains("ColorIdentitySymbols.ItemsSource = colors") &&
             manaCode.Contains("new DrawingImage(group)") &&
             !manaCode.Contains("HttpClient") &&
             !manaCode.Contains("Process.Start"),
             "Card details must use larger art, an accessible rarity ribbon and offline mana symbols.");
        Must(NativeManaIdentity.ParseColorIdentity("WU").Select(x => x.Name)
                 .SequenceEqual(new[] { "White", "Blue" }) &&
             NativeManaIdentity.ParseColorIdentity("U,W").Select(x => x.Symbol)
                 .SequenceEqual(new[] { "W", "U" }) &&
             NativeManaIdentity.ParseColorIdentity("UBRG").Count == 4 &&
             NativeManaIdentity.ParseColorIdentity("Colorless").Single().Symbol == "C" &&
             NativeManaIdentity.ParseColorIdentity("").Single().Symbol == "C" &&
             NativeManaIdentity.ParseColorIdentity("unknown").Count == 0,
             "Mana identity icons must preserve WUBRG order, colorless fallback and unknown-data safety.");
        var deckCardsXaml = File.ReadAllText(
            Path.Combine(dir, "NativeCollectionWorkspace.xaml"));
        Must(deckCardsXaml.Contains("Width=\"108\" Height=\"145\"") &&
             deckCardsXaml.Contains("ArtUrl=\"{Binding CoverArtworkUrl}\" DecodeWidth=\"260\"") &&
             deckCardsXaml.Contains("Background=\"{DynamicResource VaultTileWash}\"") &&
             deckCardsXaml.Contains("VirtualizingStackPanel.IsVirtualizing=\"True\""),
             "Saved-deck boxes should give artwork more space without losing list virtualization.");
        Must(new CatalogSnapshot.PrintingRow("Test", "tst", "1", Rarity: "mythic").RarityLabel ==
             "MYTHIC", "Catalog rarity indicator must use the printing's actual rarity.");

        var markerCards = Enumerable.Range(1, 8).Cast<object>().ToArray();
        var tileRows = CatalogTileLayout.BuildRows(markerCards, 3);
        Must(tileRows.Count == 3 &&
             tileRows.All(row => row.Cards.Count == 3) &&
             Equals(tileRows[0].Cards[0], 1) &&
             Equals(tileRows[1].Cards[2], 6) &&
             Equals(tileRows[2].Cards[1], 8) &&
             tileRows[2].Cards[2] is null,
             "Tile rows lost card order or final-row padding.");
        var wideRows = CatalogTileLayout.BuildRows(markerCards, 5);
        Must(wideRows.Count == 2 && wideRows.All(row => row.Cards.Count == 5) &&
             Equals(wideRows[0].Cards[4], 5) &&
             Equals(wideRows[1].Cards[2], 8) &&
             wideRows[1].Cards[3] is null,
             "Five-column compact rows must preserve every card and pad only the last row.");
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
