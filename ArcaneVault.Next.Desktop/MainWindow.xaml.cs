using Microsoft.Win32;
using ArcaneVault.Next.EDHLab;
using ArcaneVault.Next.Domain;
using ArcaneVault.Next.Collection;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// Native WPF paging and query performance spike. Synthetic by default; users
/// must explicitly choose a Scryfall SQLite catalog to make a read-only snapshot.
/// No ownership or deck data is imported or modified.
/// </summary>
public partial class MainWindow : Window
{
    private sealed record SampleCard(string Name, string SetCode, string CollectorNumber);

    private readonly IReadOnlyList<SampleCard> sampleCards;
    private CatalogSnapshot? catalogSnapshot;
    private NativeCatalogOwnership? catalogOwnership;
    private CommanderBrowserView? edhLabWindow;
    private NativeDeckEditorView? embeddedEdhDraft;
    private bool edhLabRequested;
    private NativeCollectionWindow? collectionWindow; // explicit read-only backup preview only
    private NativeCollectionWorkspace? nativeWorkspaceView;
    private bool nativeWorkspaceRequested;
    private bool nativeWorkspaceLoading;
    private int nativeWorkspaceSelectedTab;
    private int nativeWorkspaceGeneration;
    private NativeShortlistWindow? shortlistWindow;
    private CancellationTokenSource? searchCancellation;
    private bool closing;
    private CancellationTokenSource? catalogSyncCancellation;
    private bool catalogSyncBusy;
    private bool nativeUpdateBusy;
    private DataTemplate? catalogDetailedTemplate;
    private ItemsPanelTemplate? catalogDetailedPanel;
    private bool syntheticMode;
    private int modeVersion;
    private object[] catalogFlatCards = Array.Empty<object>();
    private CatalogSnapshot.PrintingRow? selectedTilePrinting;
    private readonly HashSet<string> bookmarkedCatalogNames = new(StringComparer.OrdinalIgnoreCase);
    private bool shortlistStarBusy;
    private int activeTileColumns;
    private const int CatalogPageSize = 200;
    private int catalogPageIndex;
    private bool catalogPageMayHaveMore;
    private bool catalogPageBusy;

    private void SetCatalogPageState(int displayedRows, bool hasMore)
    {
        catalogPageMayHaveMore = hasMore;
        catalogPageBusy = false;
        if (CatalogPageStatus is null || CatalogPageLabel is null ||
            PreviousCatalogPageButton is null || NextCatalogPageButton is null) return;
        long first = displayedRows == 0 ? 0 : (long)catalogPageIndex * CatalogPageSize + 1;
        long last = first == 0 ? 0 : first + displayedRows - 1;
        CatalogPageLabel.Text = "Page " + (catalogPageIndex + 1).ToString("N0");
        CatalogPageStatus.Text = displayedRows == 0
            ? "No matching cards"
            : $"Showing {first:N0}–{last:N0}" + (hasMore ? " · More matches available" : " · End of results");
        PreviousCatalogPageButton.IsEnabled = catalogPageIndex > 0;
        NextCatalogPageButton.IsEnabled = hasMore;
    }

    private void SetCatalogPageBusy()
    {
        catalogPageBusy = true;
        if (PreviousCatalogPageButton is not null)
            PreviousCatalogPageButton.IsEnabled = false;
        if (NextCatalogPageButton is not null)
            NextCatalogPageButton.IsEnabled = false;
        if (CatalogPageStatus is not null)
            CatalogPageStatus.Text = "Loading matching catalog page…";
    }

    private void OnPreviousCatalogPageClicked(object sender, RoutedEventArgs e)
    {
        if (catalogPageBusy || catalogPageIndex <= 0) return;
        catalogPageIndex--;
        _ = FilterAsync(debounce: false, resetPage: false);
    }

    private void OnNextCatalogPageClicked(object sender, RoutedEventArgs e)
    {
        if (catalogPageBusy || !catalogPageMayHaveMore ||
            catalogPageIndex == int.MaxValue / CatalogPageSize) return;
        catalogPageIndex++;
        _ = FilterAsync(debounce: false, resetPage: false);
    }

    private CatalogSnapshot.PrintingRow? SelectedCatalogPrinting()
        => CatalogViewMode.SelectedIndex == 2
            ? selectedTilePrinting
            : CardsList.SelectedItem as CatalogSnapshot.PrintingRow;

    private void SetCatalogResults(IEnumerable<object> rows)
    {
        catalogFlatCards = rows.Select(x => x is CatalogSnapshot.PrintingRow card
            ? card with { IsShortlisted = bookmarkedCatalogNames.Contains(card.Name) }
            : x).ToArray();
        selectedTilePrinting = null;
        activeTileColumns = 0;
        ApplyCatalogLayout();
    }

    private void ApplyCatalogLayout()
    {
        if (CardsList is null || CatalogViewMode is null) return;
        if (CatalogViewMode.SelectedIndex != 2)
        {
            CardsList.ItemsSource = catalogFlatCards;
            return;
        }
        int columns = CatalogTileLayout.ColumnsForWidth(CardsList.ActualWidth);
        if (columns == activeTileColumns &&
            CardsList.ItemsSource is IReadOnlyList<CatalogTileRow>) return;
        activeTileColumns = columns;
        CardsList.ItemsSource = CatalogTileLayout.BuildRows(catalogFlatCards, columns);
    }

    private void OnCatalogListSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (CatalogViewMode?.SelectedIndex == 2 &&
            CatalogTileLayout.ColumnsForWidth(e.NewSize.Width) != activeTileColumns)
            ApplyCatalogLayout();
    }

    private void OnCatalogTileMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: CatalogSnapshot.PrintingRow card })
            return;
        // The overlaid star is an independent action: it must not open the
        // detail window, even on a rapid/double click.
        for (System.Windows.DependencyObject? node = e.OriginalSource as System.Windows.DependencyObject;
             node is not null; node = System.Windows.Media.VisualTreeHelper.GetParent(node))
        {
            if (node is Button { Name: "CardShortlistStar" }) return;
            if (ReferenceEquals(node, sender)) break;
        }
        selectedTilePrinting = card;
        // A single click updates the persistent read-only inspector.
        // Double-click opens the exact clicked printing in full details,
        // without changing the inspector's width or the catalog view.
        _ = ShowCardInInspectorAsync(card);
        if (e.ClickCount >= 2)
        {
            e.Handled = true;
            OnCatalogCardDetailsClicked(sender, new RoutedEventArgs());
        }
    }


    public MainWindow()
    {
        InitializeComponent();
        InitializeInAppNotices();
        UpdateWorkspaceNavigation();
        catalogDetailedTemplate = CardsList.ItemTemplate;
        catalogDetailedPanel = CardsList.ItemsPanel;
        // SelectedIndex is set by XAML before these templates are captured.
        // Reapply the initial view now so Card Art actually uses the tile-row
        // template, rather than silently retaining the detailed list template.
        OnCatalogViewModeChanged(CatalogViewMode, new SelectionChangedEventArgs(
            System.Windows.Controls.Primitives.Selector.SelectionChangedEvent,
            Array.Empty<object>(), Array.Empty<object>()));
        NativeVersionLabel.Text = "Native preview v" +
            (GetType().Assembly.GetName().Version?.ToString(3) ?? "unknown");
        sampleCards = Enumerable.Range(1, 25_000).Select(i =>
            new SampleCard($"Sample Card {i:D5}", $"S{i % 60:D2}", $"#{i % 450 + 1:D3}")).ToArray();
        // A new real workspace must never appear populated with demo entries.
        SetCatalogResults(Array.Empty<object>());
        FilterStatus.Text = "No catalog selected · No sample cards are owned";
        Loaded += async (_, _) => await InitializeNativeAsync();
        Closed += (_, _) =>
        {
            closing = true;
            inspectorRequestVersion++;
            DisconnectInAppNotices();
            catalogSyncCancellation?.Cancel();
            ClearEmbeddedEdhLab();
            collectionWindow?.Close();
            InvalidateNativeWorkspace();
            shortlistWindow?.Close();
            CancelPendingSearch();
            catalogSnapshot?.Dispose();
        };
    }

    /// <summary>
    /// Start from the independent native profile. Never read or modify the
    /// installed WebView app automatically and never silently import a backup.
    /// A corrupt workspace is reported, not reset.
    /// </summary>
    private async Task InitializeNativeAsync()
    {
        OpenNativeButton.IsEnabled = System.IO.File.Exists(NativeWorkspace.DefaultPath);
        DeckLibraryNavigationButton.IsEnabled = OpenNativeButton.IsEnabled;
        SetProgressNavigationButton.IsEnabled = OpenNativeButton.IsEnabled;
        ExportFullBackupButton.IsEnabled = OpenNativeButton.IsEnabled;
        RestoreRecoveryButton.IsEnabled =
            NativeWorkspaceSafety.RecoveryPaths(NativeWorkspace.DefaultPath).Count > 0;
        ImportNativeButton.IsEnabled = !OpenNativeButton.IsEnabled;
        MigrateLegacyButton.IsEnabled = true;
        SaveNativeCatalogButton.IsEnabled = false;

        if (System.IO.File.Exists(NativeWorkspace.CatalogPath))
        {
            try
            {
                var persisted = await Task.Run(() =>
                    CatalogSnapshot.OpenPersistent(NativeWorkspace.CatalogPath));
                if (closing) { persisted.Dispose(); return; }
                var previous = catalogSnapshot;
                EdhLabReadOnlyCache.Clear();
                catalogSnapshot = persisted;
                modeVersion++;
                previous?.Dispose();
                EdhLabButton.IsEnabled = true;
                ModeStatus.Text =
                    $"{persisted.TotalPrintings:N0} printings loaded from private native catalog · No live data edits";
                _ = FilterAsync();
            }
            catch (Exception)
            {
                if (!closing)
                    ModeStatus.Text =
                        "Native catalog could not be loaded. Choose a catalog source manually; no files were altered.";
            }
        }

        // Load verified ownership and bookmark names without changing the
        // collection; stars and ownership can share the same verified read.
        if (!closing && System.IO.File.Exists(NativeWorkspace.DefaultPath))
            await LoadNativeOwnershipForCatalogAsync();
    }

    private async Task LoadNativeOwnershipForCatalogAsync()
    {
        int version = modeVersion;
        try
        {
            var loaded = await Task.Run(() =>
            {
                var workspace = NativeWorkspace.Open(NativeWorkspace.DefaultPath);
                var proxies = NativeWorkspaceSafety.ListProxies(workspace.DatabasePath);
                var ownership = NativeCatalogOwnership.From(workspace.Collection, proxies);
                var favorites = NativeWorkspaceSafety.ListShortlist(workspace.DatabasePath);
                return (Ownership: ownership, Favorites: favorites);
            });
            if (closing || version != modeVersion || nativeWorkspaceRequested) return;
            catalogOwnership = loaded.Ownership;
            bookmarkedCatalogNames.Clear();
            bookmarkedCatalogNames.UnionWith(loaded.Favorites);
            _ = FilterAsync();
        }
        catch (Exception ex)
        {
            if (!closing)
                FilterStatus.Text =
                    "Catalog ownership could not be loaded: " + ex.Message +
                    ". Open My Collection for recovery options.";
        }
    }

    /// <summary>
    /// Updates only executable files, never the native workspace. The user
    /// reviews the candidate before download and before switching versions.
    /// The installed legacy app and old native executable remain intact.
    /// </summary>
    private async void OnCheckNativeUpdatesClicked(object sender, RoutedEventArgs e)
    {
        if (nativeUpdateBusy || closing) return;
        nativeUpdateBusy = true;
        CheckNativeUpdatesButton.IsEnabled = false;
        try
        {
            Version current = GetType().Assembly.GetName().Version ??
                new Version(0, 9, 0);
            FilterStatus.Text = "Checking versioned Arcane Vault Native preview releases…";
            var release = await NativeUpdateService.FindUpdateAsync(current);
            if (closing) return;
            if (release is null)
            {
                FilterStatus.Text = "No newer versioned native preview release is available.";
                NativeInAppDialog.Show(this,
                    $"Current native version: {current.ToString(3)}.\n\n" +
                    "No newer verified native preview release was found. " +
                    "The existing WebView app uses a separate updater.",
                    "Native updates", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            string version = release.Version.ToString(3);
            if (NativeInAppDialog.Show(this,
                $"Native preview v{version} is available.\n\n" +
                "Download the official ZIP and SHA-256 checksum from the project’s " +
                "GitHub release. The ZIP will be verified and unpacked into a NEW " +
                "versioned application folder; existing installed versions, " +
                "the installed WebView app and all collection/recovery files are " +
                "left untouched.\n\n" +
                "This is an unsigned preview, not the certified 1.0 release. " +
                "Install this version side-by-side?",
                "Install native update?", MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                FilterStatus.Text = "Native update canceled. No application or collection files changed.";
                return;
            }
            var progress = new Progress<string>(message =>
            {
                if (!closing) FilterStatus.Text = message;
            });
            var installed = await NativeUpdateService.DownloadAndStageAsync(release,
                progress: progress);
            if (closing) return;
            FilterStatus.Text = $"Native v{version} installed separately; current collection unchanged.";
            if (NativeInAppDialog.Show(this,
                $"Native preview v{version} is installed at:\n" +
                installed.Directory +
                "\n\nThe previous executable remains available. " +
                "Your existing native collection will be opened by the new " +
                "version AFTER this instance closes.\n\nLaunch the new version now?",
                "Native update staged", MessageBoxButton.YesNo,
                MessageBoxImage.Information) == MessageBoxResult.Yes)
            {
                string token = NativeUpgradeHandoff.NewId();
                try
                {
                    var start = new ProcessStartInfo
                    {
                        FileName = installed.ExecutablePath,
                        UseShellExecute = true,
                        WorkingDirectory = installed.Directory
                    };
                    start.ArgumentList.Add("--upgrade-from-pid=" + Environment.ProcessId);
                    start.ArgumentList.Add(NativeUpgradeHandoff.ArgumentPrefix + token);
                    using var child = Process.Start(start) ??
                        throw new InvalidOperationException("Windows did not start the new executable.");
                    // Starting a process does not mean it entered WPF startup.
                    // Keep the working version open if the child crashes or
                    // fails to acknowledge startup within the safety window.
                    bool ready = await NativeUpgradeHandoff.WaitForReadyAsync(
                        token, () => !child.HasExited, TimeSpan.FromSeconds(20));
                    if (closing) return;
                    if (!ready)
                    {
                        string reason = child.HasExited
                            ? "The new process exited before confirming startup."
                            : "The new process did not confirm startup in 20 seconds.";
                        FilterStatus.Text = "New version installed but automatic relaunch failed.";
                        NativeInAppDialog.Show(this,
                            reason + Environment.NewLine + Environment.NewLine +
                            "The current app remains open. Your collection and both " +
                            "version folders are unchanged." +
                            Environment.NewLine + Environment.NewLine +
                            "Check " + Path.Combine(
                                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                "ArcaneVault", "Diagnostics", "native-crash.log"),
                            "Native update launch failed",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    // Child has reached application startup. It now waits
                    // for THIS parent process to exit before opening the
                    // shared native workspace.
                    Application.Current.Shutdown();
                }
                finally { NativeUpgradeHandoff.Cleanup(token); }
            }
        }
        catch (Exception ex)
        {
            if (!closing)
            {
                FilterStatus.Text = "Native update was not installed. Existing collection was not edited.";
                NativeInAppDialog.Show(this,
                    "Native update failed verification or installation.\n\n" +
                    ex.Message +
                    "\n\nYour active executable and collection were not replaced.",
                    "Native update not applied",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        finally
        {
            nativeUpdateBusy = false;
            if (!closing) CheckNativeUpdatesButton.IsEnabled = true;
        }
    }

    /// <summary>
    /// First-party native Scryfall sync: download one official bulk file,
    /// build an isolated SQLite database and publish only after validation.
    /// No legacy Windows app, export or card-by-card scraping is involved.
    /// </summary>
    private async void OnSyncCatalogClicked(object sender, RoutedEventArgs e)
    {
        if (closing || catalogSyncBusy) return;

        if (NativeInAppDialog.Show(this,
            "Arcane Vault Next will download Scryfall's English-first DEFAULT CARDS " +
            "bulk dataset (all default printing IDs), which may require hundreds " +
            "of MB of disk space and a substantial download.\n\n" +
            "The new catalog is built in a separate database. If an existing " +
            "native catalog is replaced, its entire previous version will be " +
            "preserved as a catalog recovery file. Physical/Arena ownership " +
            "and all deck data will NOT be modified.\n\n" +
            "Download/check for updates now?",
            "Official Scryfall bulk catalog", MessageBoxButton.YesNo,
            MessageBoxImage.Information) != MessageBoxResult.Yes) return;

        catalogSyncBusy = true;
        var cts = new CancellationTokenSource();
        catalogSyncCancellation = cts;
        SyncCatalogButton.IsEnabled = false;
        CancelCatalogButton.IsEnabled = true;
        OpenCatalogButton.IsEnabled = false;
        SaveNativeCatalogButton.IsEnabled = false;
        FilterStatus.Text = "Checking official Scryfall bulk data…";

        var status = new Progress<NativeScryfallCatalogSync.Progress>(stage =>
        {
            if (!closing && ReferenceEquals(catalogSyncCancellation, cts))
                FilterStatus.Text = stage.Message;
        });
        try
        {
            System.IO.Directory.CreateDirectory(NativeWorkspace.RootDirectory);
            var result = await Task.Run(() =>
                NativeScryfallCatalogSync.SyncAsync(
                    NativeWorkspace.CatalogPath, progress: status, cancellation: cts.Token));

            if (closing) return;
            if (!result.Updated)
            {
                FilterStatus.Text =
                    $"Scryfall catalog already current: {result.Printings:N0} printings " +
                    $"and {result.CardNames:N0} unique card names; no files changed.";
                if (catalogSnapshot is not null) return;
            }

            // Queries and native collection windows must be re-created from
            // the new verified catalog, never left with stale SQLite references.
            var fresh = await Task.Run(() =>
                CatalogSnapshot.OpenPersistent(NativeWorkspace.CatalogPath));
            if (closing) { fresh.Dispose(); return; }

            CancelPendingSearch();
            ClearEmbeddedEdhLab();
            collectionWindow?.Close();
            collectionWindow = null;
            InvalidateNativeWorkspace();
            var previous = catalogSnapshot;
            EdhLabReadOnlyCache.Clear();
                catalogSnapshot = fresh;
            syntheticMode = false;
            modeVersion++;
            previous?.Dispose();
            EdhLabButton.IsEnabled = true; // Valid, refreshed native catalog is now available
            SearchBox.Text = "";
            SaveNativeCatalogButton.IsEnabled = false;
            ModeStatus.Text =
                $"Native Scryfall catalog: {result.Printings:N0} printings / " +
                $"{result.CardNames:N0} card names · bulk updated {result.BulkUpdatedAt}";
            FilterStatus.Text = result.Updated
                ? "Official Scryfall catalog rebuilt and verified. Saved native collection unchanged."
                : "Existing native Scryfall catalog loaded and verified.";
            _ = FilterAsync();
            if (System.IO.File.Exists(NativeWorkspace.DefaultPath))
                await OpenNativeCollectionAsync();
            if (!closing && result.RecoveryPath is not null)
                NativeInAppDialog.Show(this,
                    "Catalog updated successfully. The previous complete native " +
                    "catalog was preserved here:\n" + result.RecoveryPath +
                    "\n\nPhysical and Arena inventory were not changed.",
                    "Scryfall catalog refresh complete",
                    MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (OperationCanceledException)
        {
            if (!closing)
                FilterStatus.Text = "Scryfall download/index canceled. Existing native catalog was not replaced.";
        }
        catch (Exception ex)
        {
            if (!closing)
            {
                FilterStatus.Text = "Scryfall catalog update was not confirmed; native ownership was not changed.";
                NativeInAppDialog.Show(this,
                    "The catalog update did not finish successfully. If a catalog already " +
                    "existed, keep its recovery files. No native ownership data was edited.\n\n" +
                    ex.Message,
                    "Scryfall sync failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        finally
        {
            if (ReferenceEquals(catalogSyncCancellation, cts))
                catalogSyncCancellation = null;
            cts.Dispose();
            catalogSyncBusy = false;
            if (!closing)
            {
                SyncCatalogButton.IsEnabled = true;
                CancelCatalogButton.IsEnabled = false;
                OpenCatalogButton.IsEnabled = true;
                SaveNativeCatalogButton.IsEnabled = catalogSnapshot is not null &&
                    !System.IO.File.Exists(NativeWorkspace.CatalogPath);
            }
        }
    }

    private void OnCancelCatalogClicked(object sender, RoutedEventArgs e)
    {
        if (!catalogSyncBusy || catalogSyncCancellation is null) return;
        CancelCatalogButton.IsEnabled = false;
        FilterStatus.Text = "Canceling Scryfall sync and cleaning up staged files…";
        catalogSyncCancellation.Cancel();
    }

    private async void OnSaveNativeCatalogClicked(object sender, RoutedEventArgs e)
    {
        var active = catalogSnapshot;
        if (active is null) return;
        if (System.IO.File.Exists(NativeWorkspace.CatalogPath))
        {
            FilterStatus.Text = "A native catalog already exists. Existing data was preserved.";
            return;
        }

        SaveNativeCatalogButton.IsEnabled = false;
        OpenCatalogButton.IsEnabled = false;
        SyntheticButton.IsEnabled = false;
        FilterStatus.Text = "Saving a native catalog once, without changing the installed app…";
        int version = modeVersion;
        try
        {
            System.IO.Directory.CreateDirectory(NativeWorkspace.RootDirectory);
            var saved = await Task.Run(() =>
                CatalogSnapshot.SavePersistentCopy(active.ReadOnlySnapshotPath,
                    NativeWorkspace.CatalogPath));
            if (closing || version != modeVersion)
            {
                saved.Dispose();
                return;
            }
            CancelPendingSearch();
            collectionWindow?.Close();
            collectionWindow = null;
            InvalidateNativeWorkspace();
            var previous = catalogSnapshot;
            EdhLabReadOnlyCache.Clear();
                catalogSnapshot = saved;
            modeVersion++;
            previous?.Dispose();
            EdhLabButton.IsEnabled = true;
            ModeStatus.Text =
                $"{saved.TotalPrintings:N0} printings now loaded directly from the native catalog";
            FilterStatus.Text = "Native catalog saved. It will reopen automatically next launch.";
            _ = FilterAsync();
            if (nativeWorkspaceRequested) _ = OpenNativeCollectionAsync();
        }
        catch (Exception)
        {
            if (!closing) FilterStatus.Text =
                "Native catalog import failed or already exists. Original catalog was not changed.";
        }
        finally
        {
            if (!closing)
            {
                SaveNativeCatalogButton.IsEnabled =
                    catalogSnapshot is not null &&
                    !System.IO.File.Exists(NativeWorkspace.CatalogPath);
                OpenCatalogButton.IsEnabled = true;
                SyntheticButton.IsEnabled = true;
            }
        }
    }

    private async void OnMigrateLegacyClicked(object sender, RoutedEventArgs e)
    {
        bool compareOnly = System.IO.File.Exists(NativeWorkspace.DefaultPath);
        if (!System.IO.Directory.Exists(LegacyProfileSnapshot.LiveProfilePath))
        {
            FilterStatus.Text = "No installed Arcane Vault browser profile was found. Use Import backup once if necessary.";
            return;
        }

        if (NativeInAppDialog.Show(this,
            "Close the old Arcane Vault Windows app before continuing.\n\n" +
            "This reads a temporary ISOLATED COPY of its local browser storage. " +
            "The original app and collection will not be modified, and the new native workspace is read-only. " +
            "You will review recovered totals before anything could be imported. " +
            "If a native workspace already exists, this is COMPARISON ONLY and cannot replace it.\n\n" +
            "This one-time migration requires the installed Microsoft Edge WebView2 Runtime. Continue?",
            "Migrate installed Arcane Vault collection", MessageBoxButton.YesNo,
            MessageBoxImage.Information) != MessageBoxResult.Yes)
            return;

        MigrateLegacyButton.IsEnabled = false;
        ImportNativeButton.IsEnabled = false;
        OpenNativeButton.IsEnabled = false;
        FilterStatus.Text = "Reading an isolated snapshot of installed Arcane Vault WebView storage…";
        try
        {
            var recovered = await OfflineLegacyBridge.ExtractAsync(this);
            if (closing) return;

            var view = recovered.Collection;
            if (compareOnly)
            {
                var existing = await Task.Run(() =>
                    NativeWorkspace.Open(NativeWorkspace.DefaultPath));
                if (closing) return;
                var comparison = await Task.Run(() =>
                    NativeWorkspaceSafety.Compare(NativeWorkspace.DefaultPath, recovered.BackupBytes));
                if (closing) return;

                string match(bool yes) => yes ? "MATCH" : "DIFFERENT";
                string details =
                    $"Installed legacy: {view.PhysicalCopies:N0} physical / {view.ArenaCopies:N0} Arena copies, " +
                    $"{view.Decks:N0} decks, {view.DeckRevisions:N0} revisions, {view.PreferredArtworks:N0} art entries.\n" +
                    $"Native workspace: {existing.Collection.PhysicalCopies:N0} physical / " +
                    $"{existing.Collection.ArenaCopies:N0} Arena copies, " +
                    $"{existing.Collection.Decks:N0} decks, " +
                    $"{existing.Collection.DeckRevisions:N0} revisions, " +
                    $"{existing.Collection.PreferredArtworks:N0} art entries.\n\n" +
                    $"Physical/Arena records: {match(comparison.InventoryEqual)}\n" +
                    $"Deck/revision/art counts: {match(comparison.OtherCountsEqual)}\n" +
                    $"Complete data (excluding export timestamp): {match(comparison.PayloadEquivalent)}\n" +
                    $"Unresolved physical: {comparison.CurrentUnresolvedCopies:N0} native, " +
                    $"{comparison.CandidateUnresolvedCopies:N0} legacy copies.\n\n";
                if (!comparison.HasChanges)
                {
                    NativeInAppDialog.Show(this, details + "No meaningful changes found. Nothing was modified.",
                        "Native collection is up to date", MessageBoxButton.OK, MessageBoxImage.Information);
                    FilterStatus.Text = "Native collection matches legacy source; no refresh necessary.";
                    return;
                }

                if (NativeInAppDialog.Show(this, details +
                    "Replace (not merge) the native workspace with these exact installed-app records? " +
                    "The previous native database will be retained as a separate recovery point. " +
                    "The original installed application is never modified.\n\n" +
                    "Choose No if any numbers appear unexpected.",
                    "Confirm native refresh with recovery", MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) != MessageBoxResult.Yes)
                {
                    FilterStatus.Text = "Refresh canceled; native collection unchanged.";
                    return;
                }

                FilterStatus.Text = "Validating replacement and preserving a native rollback…";
                var refreshed = await Task.Run(() =>
                    NativeWorkspaceSafety.Refresh(NativeWorkspace.DefaultPath, recovered.BackupBytes,
                        "installed-webview2-offline-refresh", comparison.CurrentBackupSha256));
                if (closing) return;
                collectionWindow?.Close();
                collectionWindow = null;
            InvalidateNativeWorkspace();
                RestoreRecoveryButton.IsEnabled = true;
                await OpenNativeCollectionAsync();
                if (!closing)
                    NativeInAppDialog.Show(this,
                        "Native refresh completed. The previous collection was saved at:\n" +
                        refreshed.RecoveryPath +
                        "\n\nUse 'Restore native recovery' to reverse this refresh.",
                        "Native refresh verified", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (NativeInAppDialog.Show(this,
                $"Legacy source recovered:\n" +
                $"Physical: {view.PhysicalCards.Count:N0} inventory rows / {view.PhysicalCopies:N0} copies\n" +
                $"Arena: {view.ArenaCards.Count:N0} entries / {view.ArenaCopies:N0} copies\n" +
                $"Decks: {view.Decks:N0}, revisions: {view.DeckRevisions:N0}\n" +
                $"Preferred artwork: {view.PreferredArtworks:N0}\n" +
                $"Missing or invalid physical printing IDs: {view.CopiesWithMissingOrInvalidIds:N0} copies\n\n" +
                "Create a NEW private native workspace with these exact records? " +
                "The installed app will remain unchanged. Empty or unexpected totals should be investigated before confirming.",
                "Verify local collection before native migration", MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                FilterStatus.Text = "Migration canceled. No native workspace was created.";
                return;
            }

            FilterStatus.Text = "Verifying and committing independent native collection…";
            System.IO.Directory.CreateDirectory(NativeWorkspace.RootDirectory);
            await Task.Run(() => NativeWorkspace.ImportNewBytes(
                recovered.BackupBytes, "installed-webview2-offline-snapshot",
                NativeWorkspace.DefaultPath));
            if (closing) return;

            // Catalog is derived metadata, never authoritative ownership.
            // Its import is optional and must not invalidate the new collection.
            string legacyCatalog = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ArcaneVault", "scryfall-catalog.sqlite");
            if (System.IO.File.Exists(legacyCatalog) &&
                !System.IO.File.Exists(NativeWorkspace.CatalogPath))
            {
                try
                {
                    using var nativeCatalog = await Task.Run(() =>
                        CatalogSnapshot.SavePersistentCopy(
                            legacyCatalog, NativeWorkspace.CatalogPath));
                }
                catch (Exception)
                {
                    if (!closing) FilterStatus.Text =
                        "Collection migrated; optional native catalog copy failed. Card metadata can be loaded separately.";
                }
            }

            if (closing) return;
            if (System.IO.File.Exists(NativeWorkspace.CatalogPath))
            {
                try
                {
                    var catalog = await Task.Run(() =>
                        CatalogSnapshot.OpenPersistent(NativeWorkspace.CatalogPath));
                    if (closing) { catalog.Dispose(); return; }
                    collectionWindow?.Close();
                    collectionWindow = null;
            InvalidateNativeWorkspace();
                    CancelPendingSearch();
                    var previous = catalogSnapshot;
                    EdhLabReadOnlyCache.Clear();
                catalogSnapshot = catalog;
                    modeVersion++;
                    previous?.Dispose();
                    _ = FilterAsync();
                }
                catch (Exception) { /* Collection still safe and independently readable. */ }
            }

            FilterStatus.Text = "Direct native migration completed. Reopening persisted collection…";
            await OpenNativeCollectionAsync();
        }
        catch (Exception ex)
        {
            if (!closing)
            {
                FilterStatus.Text = "Direct migration did not complete. Existing data was not overwritten.";
                NativeInAppDialog.Show(this,
                    "Migration was stopped without changing the installed app.\n\n" + ex.Message +
                    "\n\nIf the original app uses an unsupported browser profile, " +
                    "the previous manual full-backup import is still available.",
                    "Arcane Vault Next — safe migration stop",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        finally
        {
            if (!closing)
            {
                bool exists = System.IO.File.Exists(NativeWorkspace.DefaultPath);
                OpenNativeButton.IsEnabled = exists;
                ImportNativeButton.IsEnabled = !exists;
                MigrateLegacyButton.IsEnabled = true;
                RestoreRecoveryButton.IsEnabled =
                    NativeWorkspaceSafety.RecoveryPaths(NativeWorkspace.DefaultPath).Count > 0;
            }
        }
    }

    /// <summary>
    /// Create an empty native workspace with no source import, or replace an
    /// existing workspace by an empty verified SQLite file while retaining
    /// the COMPLETE previous native workspace as a new recovery point.
    /// Nothing here touches the installed legacy app or the Scryfall catalog.
    /// </summary>
    private async void OnStartFreshClicked(object sender, RoutedEventArgs e)
    {
        StartFreshButton.IsEnabled = false;
        NativeWorkspace.Workspace? previous;
        try
        {
            previous = System.IO.File.Exists(NativeWorkspace.DefaultPath)
                ? await Task.Run(() => NativeWorkspace.Open(NativeWorkspace.DefaultPath))
                : null;
            if (closing) return;
        }
        catch (Exception ex)
        {
            if (!closing)
                NativeInAppDialog.Show(this,
                    "The existing native workspace could not be verified, so a fresh " +
                    "collection has NOT been started. Preserve the database and its " +
                    "recovery copies.\n\n" + ex.Message,
                    "Cannot safely start fresh", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        finally
        {
            if (!closing) StartFreshButton.IsEnabled = true;
        }

        var approval = new NativeFreshStartWindow(previous?.Collection) { Owner = this };
        if (approval.ShowDialog() != true) return;

        StartFreshButton.IsEnabled = false;
        MigrateLegacyButton.IsEnabled = false;
        ImportNativeButton.IsEnabled = false;
        OpenNativeButton.IsEnabled = false;
        RestoreRecoveryButton.IsEnabled = false;
        CollectionBackupButton.IsEnabled = false;
        FilterStatus.Text = "Preparing a verified empty native collection with recovery protection…";
        try
        {
            var fresh = await Task.Run(() =>
                NativeWorkspaceSafety.StartFresh(NativeWorkspace.DefaultPath,
                    previous?.BackupSha256));
            if (closing) return;

            collectionWindow?.Close();
            collectionWindow = null;
            InvalidateNativeWorkspace();
            await OpenNativeCollectionAsync();
            if (closing) return;

            if (fresh.AlreadyEmpty)
            {
                FilterStatus.Text = "The native collection is already empty. No files changed.";
            }
            else if (fresh.RecoveryPath is null)
            {
                FilterStatus.Text = "A new empty native collection was created. No export required.";
            }
            else
            {
                FilterStatus.Text = "Started a fresh native collection. Previous collection preserved for restore.";
                NativeInAppDialog.Show(this,
                    "Your native collection is now empty.\n\n" +
                    "The complete previous native collection (including decks, artwork and preferences) " +
                    "was preserved here:\n" + fresh.RecoveryPath +
                    "\n\nUse 'Restore native recovery' to bring it back. " +
                    "The original Arcane Vault installation and saved card catalog are unchanged.",
                    "Fresh native collection created",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            if (!closing)
            {
                FilterStatus.Text =
                    "Could not confirm a fresh native collection. Existing/recovery files were not deliberately deleted.";
                NativeInAppDialog.Show(this,
                    "The fresh-start operation was not confirmed. Preserve all native " +
                    "workspace and recovery files, and check the collection before proceeding.\n\n" +
                    ex.Message, "Fresh start stopped",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        finally
        {
            if (!closing)
            {
                bool nativeExists = System.IO.File.Exists(NativeWorkspace.DefaultPath);
                StartFreshButton.IsEnabled = true;
                MigrateLegacyButton.IsEnabled = true;
                ImportNativeButton.IsEnabled = !nativeExists;
                OpenNativeButton.IsEnabled = nativeExists;
                ExportFullBackupButton.IsEnabled = nativeExists;
                RestoreRecoveryButton.IsEnabled =
                    NativeWorkspaceSafety.RecoveryPaths(NativeWorkspace.DefaultPath).Count > 0;
                CollectionBackupButton.IsEnabled = true;
            }
        }
    }

    /// <summary>
    /// User-controlled recovery export from the CURRENT native SQLite source.
    /// This includes all native edits and deck versions; it does not depend
    /// on the old Windows application or an exported legacy backup.
    /// </summary>
    private async void OnExportPeerSyncClicked(object sender, RoutedEventArgs e)
    {
        if (!File.Exists(NativeWorkspace.DefaultPath))
        {
            FilterStatus.Text = "Open a native collection before exporting peer sync.";
            return;
        }
        var picker = new SaveFileDialog
        {
            Title = "Export Windows / Android peer sync snapshot",
            Filter = "Arcane Vault peer sync (*.json)|*.json",
            AddExtension = true,
            DefaultExt = ".json",
            OverwritePrompt = false,
            FileName = "ArcaneVault-PeerSync-Windows-" +
                DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json"
        };
        if (picker.ShowDialog(this) != true) return;
        if (File.Exists(picker.FileName))
        {
            FilterStatus.Text = "Existing peer sync file must not be overwritten.";
            return;
        }
        try
        {
            byte[] bytes = await Task.Run(() =>
                NativePeerSync.Export(NativeWorkspace.DefaultPath, NativePeerSync.DeviceId()));
            using (var stream = new FileStream(picker.FileName,
                FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await stream.WriteAsync(bytes);
                stream.Flush(flushToDisk: true);
            }
            FilterStatus.Text =
                "Peer snapshot exported. Import it on Android > Data & Sync > Preview peer file. " +
                "Export is read-only and does not synchronize automatically.";
        }
        catch (Exception error)
        {
            FilterStatus.Text = "Peer snapshot export failed: " + error.Message;
        }
    }

    private async void OnPreviewPeerSyncClicked(object sender, RoutedEventArgs e)
    {
        if (!File.Exists(NativeWorkspace.DefaultPath))
        {
            FilterStatus.Text = "Open a native collection before comparing peer changes.";
            return;
        }
        var picker = new OpenFileDialog
        {
            Title = "Preview an Arcane Vault peer sync file from Android / Windows",
            Filter = "Arcane Vault peer sync (*.json)|*.json",
            CheckFileExists = true
        };
        if (picker.ShowDialog(this) != true) return;
        try
        {
            var preview = await Task.Run(() =>
            {
                var info = new FileInfo(picker.FileName);
                if (info.Length is <= 20 or > NativePeerSync.MaxBytes)
                    throw new InvalidDataException("Peer file is empty or exceeds 30 MiB.");
                return NativePeerSync.Compare(NativeWorkspace.DefaultPath,
                    File.ReadAllBytes(picker.FileName));
            });
            string details = string.Join("\n", preview.Changes.Take(14).Select(c =>
                c.Action + " · " + c.Group + " · " +
                c.Key[..Math.Min(100, c.Key.Length)]));
            if (preview.Changes.Count > 14)
                details += "\n… and " + (preview.Changes.Count - 14) + " more";
            NativeInAppDialog.Show(this,
                "Other device: " + preview.Device + "\n" +
                "Matching records: " + preview.EqualRecords.ToString("N0") + "\n" +
                "Additions: " + preview.Additions.ToString("N0") + "\n" +
                "Differences: " + preview.Updates.ToString("N0") + "\n" +
                "Absent on peer: " + preview.Removals.ToString("N0") + "\n" +
                "Potential conflicts: " + preview.Conflicts.ToString("N0") +
                "\n\n" + details +
                "\n\nNo data was changed. This preview does not authorize replacing " +
                "records without a verified shared sync baseline.",
                "Arcane Vault peer comparison", MessageBoxButton.OK,
                MessageBoxImage.Information);
            FilterStatus.Text = preview.HasChanges
                ? "Peer differences inspected (read-only). Existing data preserved."
                : "Peer data matches your current native workspace.";
        }
        catch (Exception error)
        {
            FilterStatus.Text = "Peer comparison rejected: " + error.Message;
        }
    }

    private async void OnComparePeerBaselineClicked(object sender, RoutedEventArgs e)
    {
        if (!File.Exists(NativeWorkspace.DefaultPath))
        {
            FilterStatus.Text = "Open your native collection before comparing peer edits.";
            return;
        }
        var incomingPicker = new OpenFileDialog
        {
            Title = "Step 1: Choose the latest Android / Windows peer snapshot",
            Filter = "Arcane Vault peer sync (*.json)|*.json",
            CheckFileExists = true
        };
        if (incomingPicker.ShowDialog(this) != true) return;
        var basePicker = new OpenFileDialog
        {
            Title = "Step 2: Choose the previously shared sync baseline",
            Filter = "Arcane Vault peer sync (*.json)|*.json",
            CheckFileExists = true
        };
        if (basePicker.ShowDialog(this) != true) return;
        if (Path.GetFullPath(incomingPicker.FileName).Equals(
                Path.GetFullPath(basePicker.FileName), StringComparison.OrdinalIgnoreCase))
        {
            FilterStatus.Text = "Peer and baseline must be different verified snapshots.";
            return;
        }
        try
        {
            var planned = await Task.Run(() =>
            {
                byte[] Read(string path)
                {
                    var info = new FileInfo(path);
                    if (info.Length is <= 20 or > NativePeerSync.MaxBytes)
                        throw new InvalidDataException("Peer file must be 21 bytes to 30 MiB.");
                    return File.ReadAllBytes(path);
                }
                return NativePeerSync.CompareWithBaseline(NativeWorkspace.DefaultPath,
                    Read(incomingPicker.FileName), Read(basePicker.FileName));
            });
            string details = string.Join("\n", planned.Records.Take(16).Select(c =>
                c.Decision + (c.IncomingDeletion ? " (peer lacks record)" : "") +
                " · " + c.Group + " · " + c.Key[..Math.Min(100,c.Key.Length)]));
            if (planned.Records.Count > 16)
                details += "\n… and " + (planned.Records.Count - 16) + " more";
            NativeInAppDialog.Show(this,
                "Compared your native workspace, the latest peer snapshot and " +
                "a user-selected previous common snapshot.\n\n" +
                "Incoming-only changes: " + planned.IncomingOnly.ToString("N0") + "\n" +
                "Windows-only changes: " + planned.LocalOnly.ToString("N0") + "\n" +
                "Concurrent conflicts: " + planned.Conflicts.ToString("N0") +
                "\n\n" + details +
                "\n\nNo changes were applied. The supplied baseline has not yet been " +
                "authenticated as a paired-device checkpoint.",
                "Windows / Android three-way sync review", MessageBoxButton.OK,
                MessageBoxImage.Information);
            FilterStatus.Text = "Three-way peer comparison completed without changing data.";
        }
        catch (Exception ex)
        {
            FilterStatus.Text = "Three-way peer comparison failed: " + ex.Message;
        }
    }

    private async void OnExportFullBackupClicked(object sender, RoutedEventArgs e)
    {
        if (!System.IO.File.Exists(NativeWorkspace.DefaultPath))
        {
            FilterStatus.Text = "Start or import a native collection before exporting.";
            ExportFullBackupButton.IsEnabled = false;
            return;
        }

        var picker = new SaveFileDialog
        {
            Title = "Save a complete Arcane Vault Next collection backup",
            Filter = "Arcane Vault full backup (*.json)|*.json",
            AddExtension = true,
            DefaultExt = ".json",
            OverwritePrompt = false,
            FileName = "arcane-vault-next-full-backup-" +
                DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json",
            InitialDirectory = Environment.GetFolderPath(
                Environment.SpecialFolder.MyDocuments)
        };
        if (picker.ShowDialog(this) != true) return;

        if (System.IO.File.Exists(picker.FileName))
        {
            NativeInAppDialog.Show(this,
                "This backup filename is already in use. Choose a new name. " +
                "Arcane Vault Next will not overwrite an existing backup.",
                "Keep existing backup", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (NativeInAppDialog.Show(this,
            "Save the complete CURRENT native collection as a portable full JSON " +
            "backup, including physical and Arena ownership, printings, decks, " +
            "revision history, artwork preferences and any retained legacy settings?\n\n" +
            "The file is not encrypted; keep it in a private location. " +
            "No installed application or native database will be changed, " +
            "and existing backups will not be overwritten.",
            "Confirm native full backup export", MessageBoxButton.YesNo,
            MessageBoxImage.Information) != MessageBoxResult.Yes)
            return;

        ExportFullBackupButton.IsEnabled = false;
        FilterStatus.Text = "Verifying current native collection and writing a complete backup…";
        try
        {
            var saved = await Task.Run(() =>
            {
                var native = NativeWorkspace.Open(NativeWorkspace.DefaultPath);
                return NativeWorkspaceSafety.ExportFullBackup(
                    NativeWorkspace.DefaultPath, picker.FileName,
                    native.BackupSha256);
            });
            if (closing) return;
            FilterStatus.Text =
                $"Native full backup saved: {saved.PhysicalCopies:N0} physical, " +
                $"{saved.ArenaCopies:N0} Arena copies, {saved.Decks:N0} decks.";
            NativeInAppDialog.Show(this,
                "A complete validated native collection backup was saved to:\n" +
                saved.FilePath + "\n\n" +
                $"Physical: {saved.PhysicalCopies:N0} copies\n" +
                $"Arena: {saved.ArenaCopies:N0} copies\n" +
                $"Decks: {saved.Decks:N0} ({saved.Revisions:N0} revisions)\n\n" +
                "The original native database and prior backups were unchanged.",
                "Full backup saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            if (!closing)
            {
                FilterStatus.Text = "Native full backup was not completed. Existing data is unchanged.";
                NativeInAppDialog.Show(this,
                    "Could not verify or write the full backup. No native data " +
                    "was reset or overwritten.\n\n" + ex.Message,
                    "Backup export stopped", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        finally
        {
            if (!closing)
                ExportFullBackupButton.IsEnabled =
                    System.IO.File.Exists(NativeWorkspace.DefaultPath);
        }
    }

    private async void OnRestoreRecoveryClicked(object sender, RoutedEventArgs e)
    {
        var options = NativeWorkspaceSafety.RecoveryPaths(NativeWorkspace.DefaultPath);
        if (options.Count == 0)
        {
            FilterStatus.Text = "No native recovery copies have been created.";
            RestoreRecoveryButton.IsEnabled = false;
            return;
        }
        var picker = new OpenFileDialog
        {
            Title = "Choose a native collection recovery point",
            Filter = "Native recovery SQLite (*.sqlite)|*.sqlite",
            InitialDirectory = NativeWorkspace.RootDirectory,
            FileName = System.IO.Path.GetFileName(options[0]),
            CheckFileExists = true
        };
        if (picker.ShowDialog(this) != true) return;
        string chosen = System.IO.Path.GetFullPath(picker.FileName);
        if (!options.Contains(chosen, StringComparer.OrdinalIgnoreCase))
        {
            FilterStatus.Text = "Selected file is not a registered native recovery point.";
            return;
        }
        RestoreRecoveryButton.IsEnabled = false;
        MigrateLegacyButton.IsEnabled = false;
        OpenNativeButton.IsEnabled = false;
        try
        {
            var pair = await Task.Run(() =>
                (current: NativeWorkspace.Open(NativeWorkspace.DefaultPath),
                 prior: NativeWorkspace.Open(chosen)));
            if (closing) return;
            if (NativeInAppDialog.Show(this,
                $"Current: {pair.current.Collection.PhysicalCopies:N0} physical, " +
                $"{pair.current.Collection.ArenaCopies:N0} Arena copies, " +
                $"{pair.current.Collection.Decks:N0} decks.\n" +
                $"Recovery: {pair.prior.Collection.PhysicalCopies:N0} physical, " +
                $"{pair.prior.Collection.ArenaCopies:N0} Arena copies, " +
                $"{pair.prior.Collection.Decks:N0} decks.\n\n" +
                "Restore this recovery? Your current native database will ALSO be " +
                "saved as a new recovery point. The installed Windows app is untouched.",
                "Confirm verified native recovery", MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                FilterStatus.Text = "Recovery canceled; native data unchanged.";
                return;
            }
            FilterStatus.Text = "Validating native recovery and preserving current workspace…";
            var recovered = await Task.Run(() =>
                NativeWorkspaceSafety.Restore(NativeWorkspace.DefaultPath, chosen,
                    pair.current.BackupSha256));
            if (closing) return;
            collectionWindow?.Close();
            collectionWindow = null;
            InvalidateNativeWorkspace();
            await OpenNativeCollectionAsync();
            if (!closing)
                NativeInAppDialog.Show(this,
                    "Previous native collection restored. The replaced version is preserved at:\n" +
                    recovered.RecoveryPath,
                    "Recovery completed", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            if (!closing)
                NativeInAppDialog.Show(this,
                    "Recovery was not confirmed. Keep all native database files and verify " +
                    "the current collection before another attempt.\n\n" + ex.Message,
                    "Native recovery stopped", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            if (!closing)
            {
                RestoreRecoveryButton.IsEnabled =
                    NativeWorkspaceSafety.RecoveryPaths(NativeWorkspace.DefaultPath).Count > 0;
                OpenNativeButton.IsEnabled =
                    System.IO.File.Exists(NativeWorkspace.DefaultPath);
                MigrateLegacyButton.IsEnabled = true;
            }
        }
    }

    private async void OnImportNativeClicked(object sender, RoutedEventArgs e)
    {
        if (System.IO.File.Exists(NativeWorkspace.DefaultPath))
        {
            FilterStatus.Text = "Native collection already exists. Import is blocked to prevent replacement.";
            return;
        }
        var picker = new OpenFileDialog
        {
            Title = "Import a complete full backup once into a native Windows workspace",
            Filter = "Arcane Vault full backup (*.json)|*.json|All files (*.*)|*.*",
            CheckFileExists = true,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };
        if (picker.ShowDialog(this) != true) return;

        if (NativeInAppDialog.Show(this,
            "This will make an independent, persistent, READ-ONLY native SQLite collection from your exported full backup. " +
            "It will not alter the installed Arcane Vault app, and it will NOT silently replace an existing native workspace. Continue?",
            "Import once into native Arcane Vault Next", MessageBoxButton.YesNo,
            MessageBoxImage.Information) != MessageBoxResult.Yes)
            return;

        ImportNativeButton.IsEnabled = false;
        OpenNativeButton.IsEnabled = false;
        FilterStatus.Text = "Importing and validating complete backup into a native workspace…";
        try
        {
            System.IO.Directory.CreateDirectory(NativeWorkspace.RootDirectory);
            await Task.Run(() => NativeWorkspace.ImportNew(
                picker.FileName, NativeWorkspace.DefaultPath));
            if (closing) return;
            FilterStatus.Text = "Native collection imported and verified. Reopening persisted data…";
            await OpenNativeCollectionAsync();
        }
        catch (Exception)
        {
            if (!closing) FilterStatus.Text =
                "Native import failed or workspace already exists; original backup and installed app are unchanged.";
        }
        finally
        {
            if (!closing)
            {
                OpenNativeButton.IsEnabled =
                    System.IO.File.Exists(NativeWorkspace.DefaultPath);
                ExportFullBackupButton.IsEnabled = OpenNativeButton.IsEnabled;
                ImportNativeButton.IsEnabled = !OpenNativeButton.IsEnabled;
                MigrateLegacyButton.IsEnabled = true;
            }
        }
    }

    private async void OnOpenShortlistClicked(object sender, RoutedEventArgs e)
    {
        if (shortlistWindow is not null)
        {
            shortlistWindow.Activate();
            return;
        }
        if (!System.IO.File.Exists(NativeWorkspace.DefaultPath))
        {
            FilterStatus.Text =
                "My Shortlist belongs to a native collection. Start Fresh or import a full backup first.";
            return;
        }
        var view = new NativeShortlistWindow(NativeWorkspace.DefaultPath,
            catalogSnapshot?.ReadOnlySnapshotPath) { Owner = this };
        shortlistWindow = view;
        view.Closed += async (_, _) =>
        {
            if (ReferenceEquals(shortlistWindow, view)) shortlistWindow = null;
            if (closing || !view.ShortlistChanged)
                return;
            await RefreshCatalogBookmarkNamesAsync();
            await RefreshNativeWorkspaceAsync(); // reload after optimistic SHA change
        };
        view.Show();
    }

    private async Task RefreshCatalogBookmarkNamesAsync()
    {
        if (closing || !System.IO.File.Exists(NativeWorkspace.DefaultPath)) return;
        try
        {
            var names = await Task.Run(() =>
            {
                var workspace = NativeWorkspace.Open(NativeWorkspace.DefaultPath);
                return NativeWorkspaceSafety.ListShortlist(workspace.DatabasePath);
            });
            if (closing) return;
            bookmarkedCatalogNames.Clear();
            bookmarkedCatalogNames.UnionWith(names);
            RebindShortlistStars();
        }
        catch (Exception ex)
        {
            if (!closing)
                FilterStatus.Text = "Could not refresh shortlist star state: " + ex.Message;
        }
    }

    private void RebindShortlistStars()
    {
        // Change presentation rows, never catalog SQLite or collection state.
        // Multiple printings of the same name always show the same star.
        var current = SelectedCatalogPrinting();
        catalogFlatCards = catalogFlatCards.Select(x => x is CatalogSnapshot.PrintingRow card
            ? card with { IsShortlisted = bookmarkedCatalogNames.Contains(card.Name) }
            : x).ToArray();
        selectedTilePrinting = current is null ? null :
            catalogFlatCards.OfType<CatalogSnapshot.PrintingRow>()
                .FirstOrDefault(x => x.Name == current.Name &&
                                     x.SetCode == current.SetCode &&
                                     x.CollectorNumber == current.CollectorNumber);
        activeTileColumns = 0;
        ApplyCatalogLayout();
    }

    private async void OnCatalogShortlistStarClicked(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is not Button { Tag: CatalogSnapshot.PrintingRow selection }) return;
        // A star is a name-level bookmark, not a printing or ownership action.
        if (closing || shortlistStarBusy || catalogSnapshot is null) return;
        if (bookmarkedCatalogNames.Contains(selection.Name))
        {
            FilterStatus.Text = "★ " + selection.Name + " is already in My Shortlist. " +
                "Open My Shortlist to remove it.";
            return;
        }
        if (!System.IO.File.Exists(NativeWorkspace.DefaultPath))
        {
            FilterStatus.Text = "Create or import a native collection before saving bookmarks.";
            return;
        }
        shortlistStarBusy = true;
        try
        {
            var result = await Task.Run(() =>
            {
                var workspace = NativeWorkspace.Open(NativeWorkspace.DefaultPath);
                var existing = NativeWorkspaceSafety.ListShortlist(workspace.DatabasePath);
                if (existing.Any(n => n.Equals(selection.Name,
                    StringComparison.OrdinalIgnoreCase)))
                    return (Already: true, Saved: false);
                NativeWorkspaceSafety.SetShortlistCard(workspace.DatabasePath,
                    selection.Name, true, workspace.BackupSha256);
                return (Already: false, Saved: true);
            });
            if (closing) return;
            bookmarkedCatalogNames.Add(selection.Name);
            RebindShortlistStars();
            FilterStatus.Text = result.Already
                ? $"★ {selection.Name} is already in My Shortlist."
                : $"★ {selection.Name} saved to My Shortlist with native rollback protection. " +
                  "Ownership remains unchanged.";
            NativeNoticeHub.Publish("My Shortlist", FilterStatus.Text);
            if (result.Saved)
            {
                if (shortlistWindow is not null)
                    await shortlistWindow.RefreshFromWorkspaceAsync();
                if (nativeWorkspaceView is not null || nativeWorkspaceRequested)
                    await RefreshNativeWorkspaceAsync();
            }
        }
        catch (Exception ex)
        {
            if (!closing)
                NativeInAppDialog.Show(this,
                    "Bookmark not saved: " + ex.Message +
                    "\n\nIf the collection changed in another window, reopen it before retrying.",
                    "My Shortlist", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            shortlistStarBusy = false;
        }
    }

    private void OnOpenAppearanceClicked(object sender, RoutedEventArgs e)
    {
        var dialog = new NativeOptionsWindow { Owner = this };
        dialog.ShowDialog();
    }

    private void UpdateWorkspaceNavigation()
    {
        CatalogWorkspace.Visibility = nativeWorkspaceRequested || edhLabRequested
            ? Visibility.Collapsed : Visibility.Visible;
        NativeWorkspaceHost.Visibility = nativeWorkspaceRequested && !edhLabRequested
            ? Visibility.Visible : Visibility.Collapsed;
        EdhLabHost.Visibility = edhLabRequested
            ? Visibility.Visible : Visibility.Collapsed;

        var active = (Style)FindResource("VaultNavPrimary");
        var inactive = (Style)FindResource("VaultNavButtonDepth");
        CatalogNavigationButton.Style = nativeWorkspaceRequested || edhLabRequested
            ? inactive : active;
        EdhLabButton.Style = edhLabRequested ? active : inactive;
        OpenNativeButton.Style = nativeWorkspaceRequested &&
            nativeWorkspaceSelectedTab is not (2 or 3) ? active : inactive;
        DeckLibraryNavigationButton.Style = nativeWorkspaceRequested &&
            nativeWorkspaceSelectedTab == 2 ? active : inactive;
        SetProgressNavigationButton.Style = nativeWorkspaceRequested &&
            nativeWorkspaceSelectedTab == 3 ? active : inactive;
    }

    private void OnShowCatalogClicked(object sender, RoutedEventArgs e)
    {
        nativeWorkspaceRequested = false;
        edhLabRequested = false;
        UpdateWorkspaceNavigation();
    }

    private NativeCollectionWorkspace.NavigationState? pendingNativeUiState;

    private void InvalidateNativeWorkspace()
    {
        // A true source-data change must be reverified, but filter/search
        // choices and stable selection IDs can safely cross that refresh.
        pendingNativeUiState = nativeWorkspaceView?.CaptureNavigationState() ??
            pendingNativeUiState;
        nativeWorkspaceGeneration++;
        // Explicit data changes invalidate the view; normal sidebar switches
        // do not. Cancel work against the old verified SHA before detaching it.
        nativeWorkspaceView?.InvalidateForVerifiedWorkspaceRefresh();
        nativeWorkspaceView = null;
        NativeWorkspaceHost.Content = null;
        if (nativeWorkspaceRequested && !closing)
            NativeWorkspaceHost.Content = new TextBlock
            {
                Text = "Reloading your verified local collection…",
                Foreground = (System.Windows.Media.Brush)FindResource("VaultMuted"),
                Margin = new Thickness(22),
                TextWrapping = TextWrapping.Wrap
            };
    }

    // A rendering failure in Set Progress must not silently take down the entire
    // editor. Detach the affected view without writing any collection data.
    // The application-level dispatcher guard invokes this only for an otherwise
    // unhandled exception while the Set Progress tab is active.
    internal bool RecoverFromSetProgressFault()
    {
        if (nativeWorkspaceView?.SelectedSection != 3) return false;
        InvalidateNativeWorkspace();
        pendingNativeUiState = null; // don't restore the view that just faulted
        nativeWorkspaceRequested = false;
        UpdateWorkspaceNavigation();
        FilterStatus.Text = "Set Progress could not render. The view was closed; your collection is unchanged. See Diagnostics/native-crash.log.";
        return true;
    }

    private async void OnOpenNativeClicked(object sender, RoutedEventArgs e) =>
        await NavigateToNativeWorkspaceAsync(0);

    private async void OnOpenDeckLibraryClicked(object sender, RoutedEventArgs e) =>
        await NavigateToNativeWorkspaceAsync(2);

    private async void OnOpenSetProgressClicked(object sender, RoutedEventArgs e) =>
        await NavigateToNativeWorkspaceAsync(3);

    private async Task NavigateToNativeWorkspaceAsync(int section)
    {
        edhLabRequested = false;
        UpdateWorkspaceNavigation();
        if (!System.IO.File.Exists(NativeWorkspace.DefaultPath))
        {
            FilterStatus.Text = "Create or import a native collection before opening this section.";
            nativeWorkspaceRequested = false;
            UpdateWorkspaceNavigation();
            return;
        }
        nativeWorkspaceRequested = true;
        nativeWorkspaceSelectedTab = section;
        if (nativeWorkspaceView is not null)
        {
            nativeWorkspaceView.NavigateTo(section);
            UpdateWorkspaceNavigation();
            return; // no native backup reopen on ordinary sidebar navigation
        }
        NativeWorkspaceHost.Content = new TextBlock
            {
                Text = "Opening and verifying your local workspace…",
                Foreground = (System.Windows.Media.Brush)FindResource("VaultMuted"),
                Margin = new Thickness(22),
                TextWrapping = TextWrapping.Wrap
            };
        UpdateWorkspaceNavigation();
        await OpenNativeCollectionAsync();
    }

    private async Task OpenNativeCollectionAsync()
    {
        if (nativeWorkspaceView is not null)
        {
            if (nativeWorkspaceRequested)
                nativeWorkspaceView.NavigateTo(nativeWorkspaceSelectedTab);
            UpdateWorkspaceNavigation();
            return;
        }
        if (nativeWorkspaceLoading) return;
        if (!System.IO.File.Exists(NativeWorkspace.DefaultPath))
        {
            catalogOwnership = null;
            FilterStatus.Text = "No native collection yet. Choose Import backup once.";
            _ = FilterAsync();
            return;
        }
        var activeCatalog = catalogSnapshot;
        int version = modeVersion;
        int generation = nativeWorkspaceGeneration;
        nativeWorkspaceLoading = true;
        OpenNativeButton.IsEnabled = false;
        FilterStatus.Text = "Verifying native SQLite workspace and loading local collection…";
        try
        {
            var bundle = await Task.Run(() =>
            {
                var workspace = NativeWorkspace.Open(NativeWorkspace.DefaultPath);
                var verification = CollectionCatalogReview.Examine(
                    workspace.Collection, activeCatalog?.ReadOnlySnapshotPath);
                var progress = activeCatalog is null ? null :
                    NativeSetProgressCache.Physical(verification,
                        NativeWorkspace.CatalogPath, workspace.BackupSha256);
                var decks = NativeWorkspaceSafety.ListDecks(NativeWorkspace.DefaultPath);
                var proxies = NativeWorkspaceSafety.ListProxies(workspace.DatabasePath);
                return (workspace, verification, progress, decks, proxies);
            });
            if (closing || version != modeVersion ||
                generation != nativeWorkspaceGeneration ||
                !ReferenceEquals(activeCatalog, catalogSnapshot)) return;

            catalogOwnership = NativeCatalogOwnership.From(
                bundle.workspace.Collection, bundle.proxies);
            _ = FilterAsync(); // reflect new edits/restores/CSV imports in catalog owned status
            var view = new NativeCollectionWorkspace(bundle.workspace.Collection,
                bundle.verification, activeCatalog?.ReadOnlySnapshotPath,
                bundle.progress, nativeWorkspace: true,
                nativePath: bundle.workspace.DatabasePath,
                expectedBackupSha: bundle.workspace.BackupSha256,
                reopenNative: RefreshNativeWorkspaceAsync,
                nativeDecks: bundle.decks,
                editInEmbeddedEdhLab: OpenSavedDeckInEmbeddedEdhLabAsync);
            view.NavigateTo(nativeWorkspaceSelectedTab);
            nativeWorkspaceView = view;
            view.SectionChanged += section =>
            {
                if (!ReferenceEquals(nativeWorkspaceView, view)) return;
                nativeWorkspaceSelectedTab = section;
                UpdateWorkspaceNavigation();
            };
            NativeWorkspaceHost.Content = view;
            UpdateWorkspaceNavigation();
            // The reverified model is authoritative. Restore only view state
            // and stable identities; never resurrect removed owned cards.
            var stateToRestore = pendingNativeUiState;
            pendingNativeUiState = null;
            if (stateToRestore is not null)
            {
                try
                {
                    // A sidebar click made during the refresh wins over the
                    // section active when the old snapshot was captured.
                    await view.RestoreNavigationStateAsync(stateToRestore with
                    {
                        Section = nativeWorkspaceSelectedTab
                    });
                }
                catch (Exception ex)
                {
                    FilterStatus.Text = "Collection verified; previous filters could not be restored: " +
                        ex.Message;
                }
            }
            FilterStatus.Text =
                $"Native collection: {bundle.workspace.Collection.PhysicalCopies:N0} physical + " +
                $"{bundle.workspace.Collection.ArenaCopies:N0} Arena copies · restored from local SQLite";
            ModeStatus.Text = $"Private native workspace: {bundle.workspace.OriginalFileName} · " +
                $"imported {bundle.workspace.ImportedAtUtc} · editable native collection with verified recovery";
        }
        catch (Exception ex)
        {
            if (!closing && generation == nativeWorkspaceGeneration)
            {
                FilterStatus.Text =
                    "Native workspace verification failed. Data was NOT reset; preserve the file and use backup preview.";
                if (nativeWorkspaceRequested)
                    NativeWorkspaceHost.Content = new TextBlock
                    {
                        Text = "Collection could not be loaded safely: " + ex.Message +
                               "\n\nYour original native data was not reset. Use backup recovery or import a verified backup.",
                        Foreground = (System.Windows.Media.Brush)FindResource("VaultWarning"),
                        Margin = new Thickness(22),
                        TextWrapping = TextWrapping.Wrap
                    };
            }
        }
        finally
        {
            bool invalidatedWhileLoading = generation != nativeWorkspaceGeneration;
            nativeWorkspaceLoading = false;
            if (!closing && invalidatedWhileLoading && nativeWorkspaceRequested &&
                System.IO.File.Exists(NativeWorkspace.DefaultPath))
                _ = OpenNativeCollectionAsync(); // new catalog/backup won the race
            if (!closing)
            {
                OpenNativeButton.IsEnabled =
                    System.IO.File.Exists(NativeWorkspace.DefaultPath);
                DeckLibraryNavigationButton.IsEnabled = OpenNativeButton.IsEnabled;
        SetProgressNavigationButton.IsEnabled = OpenNativeButton.IsEnabled;
                ImportNativeButton.IsEnabled = !OpenNativeButton.IsEnabled;
                MigrateLegacyButton.IsEnabled = true;
            }
        }
    }

    private async Task RefreshNativeWorkspaceAsync()
    {
        if (closing) return;
        if (nativeWorkspaceView is not null)
            nativeWorkspaceSelectedTab = nativeWorkspaceView.SelectedSection;
        InvalidateNativeWorkspace();
        await OpenNativeCollectionAsync();
    }


    /// <summary>
    /// Catalog browsing never modifies ownership. Single-click inspects
    /// in-place; double-click explicitly opens the selected printing's full
    /// details/printings without changing the underlying virtualized list.
    /// </summary>
    private void OnCatalogCardRowDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (CatalogViewMode.SelectedIndex == 2 ||
            sender is not ListView list ||
            NativeCardRowActivation.SelectClickedRow(list, e.OriginalSource) is not
                CatalogSnapshot.PrintingRow card) return;
        e.Handled = true;
        _ = ShowCardInInspectorAsync(card);
        OnCatalogCardDetailsClicked(sender, new RoutedEventArgs());
    }

    private async void OnCatalogCardDetailsClicked(object sender, RoutedEventArgs e)
    {
        var snapshot = catalogSnapshot;
        if (closing || snapshot is null ||
            (inspectorSelection ?? SelectedCatalogPrinting()) is not CatalogSnapshot.PrintingRow selected)
        {
            FilterStatus.Text = "Select a catalog printing to view its card details.";
            return;
        }
        int version = modeVersion;
        CollectionCatalogReview.CardDetails? info;
        try
        {
            info = await Task.Run(() =>
                CollectionCatalogReview.ReadDetails(
                    snapshot.ReadOnlySnapshotPath, selected.Name));
        }
        catch (Exception)
        {
            info = null; // A stale/minimal catalog still gets a safe local fallback.
        }
        if (closing || version != modeVersion ||
            !ReferenceEquals(snapshot, catalogSnapshot)) return;
        var dialog = new NativeCardDetailsWindow(selected.Name,
            (selected.ArenaPresentation ? "Arena card (name-owned). " :
                $"Catalog: {selected.SetCode} #{selected.CollectorNumber}. ") +
            (catalogOwnership is null
                ? "No native collection loaded; ownership unknown."
                : selected.PhysicalOwned > 0 || selected.ArenaOwned > 0
                    ? $"Verified physical copies: {selected.PhysicalOwned}; Arena name-owned: {selected.ArenaOwned}. " +
                      $"Unresolved physical copies by this name: {selected.UnresolvedName}."
                    : $"Exact printing not verified owned; Arena copies: {selected.ArenaOwned}. " +
                      $"Unresolved physical copies by this name: {selected.UnresolvedName}."), info,
            snapshot.ReadOnlySnapshotPath,
            System.IO.File.Exists(NativeWorkspace.DefaultPath)
                ? NativeWorkspace.DefaultPath : null,
            selected.SetCode, selected.CollectorNumber,
            arenaScope: ArenaOnlyBox.IsChecked == true ||
                OwnershipFilterBox.SelectedIndex is 3 or 4,
            selectedArtworkUrl: selected.ImageUrl)
        { Owner = this };
        dialog.Show();
    }

    private void OnCatalogViewModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CardsList is null || CatalogColumnHeaders is null ||
            catalogDetailedTemplate is null || catalogDetailedPanel is null) return;
        var previouslySelected = CardsList.SelectedItem as CatalogSnapshot.PrintingRow ?? selectedTilePrinting;
        int mode = CatalogViewMode.SelectedIndex;

        // Preserve row virtualization in all modes. Card-art mode groups several
        // cards into each virtualized row, instead of using a nonvirtual WrapPanel.
        CardsList.ItemsPanel = catalogDetailedPanel;
        CardsList.ItemTemplate = mode switch
        {
            1 => (DataTemplate)FindResource("CatalogCompactTemplate"),
            2 => (DataTemplate)FindResource("CatalogTileRowTemplate"),
            _ => catalogDetailedTemplate
        };
        CatalogColumnHeaders.Visibility = mode == 2
            ? Visibility.Collapsed : Visibility.Visible;
        activeTileColumns = 0;
        ApplyCatalogLayout();
        if (mode != 2 && previouslySelected is not null)
            CardsList.SelectedItem = previouslySelected;
        else if (mode == 2)
            selectedTilePrinting = previouslySelected;
        // Cached Scryfall artwork controls exist only in the visible tile rows.
    }

    private void CancelPendingSearch()
    {
        var old = searchCancellation;
        searchCancellation = null;
        if (old is null) return;
        try { old.Cancel(); } catch (ObjectDisposedException) { }
        // The owning async query will dispose its own token in finally.
    }

    private async void OnOpenCatalogClicked(object sender, RoutedEventArgs e)
    {
        var expected = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ArcaneVault", "scryfall-catalog.sqlite");
        var picker = new OpenFileDialog
        {
            Title = "Select a Scryfall catalog to snapshot (read-only)",
            Filter = "SQLite catalogs (*.sqlite;*.sqlite3;*.db)|*.sqlite;*.sqlite3;*.db|All files (*.*)|*.*",
            CheckFileExists = true,
            InitialDirectory = System.IO.Directory.Exists(System.IO.Path.GetDirectoryName(expected))
                ? System.IO.Path.GetDirectoryName(expected)
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            FileName = System.IO.File.Exists(expected) ? "scryfall-catalog.sqlite" : ""
        };
        if (picker.ShowDialog(this) != true) return;

        OpenCatalogButton.IsEnabled = false;
        SyntheticButton.IsEnabled = false;
        FilterStatus.Text = "Copying SQLite snapshot…";
        try
        {
            // SQLite's online backup API makes a consistent temporary copy,
            // even when the installed app uses a WAL journal.
            var loaded = await Task.Run(() => CatalogSnapshot.Open(picker.FileName));
            if (closing) { loaded.Dispose(); return; }
            CancelPendingSearch();
            ClearEmbeddedEdhLab();
            collectionWindow?.Close();
            collectionWindow = null;
            InvalidateNativeWorkspace();
            var previous = catalogSnapshot;
            EdhLabReadOnlyCache.Clear();
                catalogSnapshot = loaded;
            syntheticMode = false;
            EdhLabButton.IsEnabled = true;
            SaveNativeCatalogButton.IsEnabled =
                !System.IO.File.Exists(NativeWorkspace.CatalogPath);
            modeVersion++;
            previous?.Dispose();
            ModeStatus.Text = $"{loaded.TotalPrintings:N0} catalog printings in isolated snapshot · Browse in pages of 200 · No live edits";
            SearchBox.Text = "";
            _ = FilterAsync();
            if (nativeWorkspaceRequested) _ = OpenNativeCollectionAsync();
        }
        catch (Exception)
        {
            if (!closing)
            {
                FilterStatus.Text = "Could not open a valid Scryfall catalog snapshot";
                ModeStatus.Text = "The existing sample or snapshot is unchanged. Check that you selected scryfall-catalog.sqlite.";
            }
        }
        finally
        {
            if (!closing)
            {
                OpenCatalogButton.IsEnabled = true;
                SyntheticButton.IsEnabled = true;
            }
        }
    }

    private async void OnCollectionBackupClicked(object sender, RoutedEventArgs e)
    {
        if (collectionWindow is not null)
        {
            collectionWindow.Activate();
            return;
        }

        var picker = new OpenFileDialog
        {
            Title = "Select an exported full Windows backup (read-only)",
            Filter = "Arcane Vault backup (*.json)|*.json|All files (*.*)|*.*",
            CheckFileExists = true,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };
        if (picker.ShowDialog(this) != true) return;

        CollectionBackupButton.IsEnabled = false;
        OpenCatalogButton.IsEnabled = false;
        SyntheticButton.IsEnabled = false;
        FilterStatus.Text = "Reading backup and checking trusted printing IDs…";
        var activeCatalog = catalogSnapshot;
        var version = modeVersion;
        try
        {
            var result = await Task.Run(() =>
            {
                var view = NativeBackupReader.Read(picker.FileName);
                var verification = CollectionCatalogReview.Examine(
                    view, activeCatalog?.ReadOnlySnapshotPath);
                var setProgress = activeCatalog is null ? null :
                    NativeSetProgress.Build(verification, activeCatalog.ReadOnlySnapshotPath);
                return (view, verification, setProgress);
            });
            if (closing || version != modeVersion ||
                !ReferenceEquals(activeCatalog, catalogSnapshot)) return;
            var dialog = new NativeCollectionWindow(
                result.view, result.verification,
                activeCatalog?.ReadOnlySnapshotPath, result.setProgress)
            { Owner = this };
            collectionWindow = dialog;
            dialog.Closed += (_, _) =>
            {
                if (ReferenceEquals(collectionWindow, dialog)) collectionWindow = null;
            };
            dialog.Show();
            FilterStatus.Text = result.verification.CatalogVerified
                ? $"Verified {result.verification.ConfirmedCopies:N0} physical copies · " +
                  $"{result.verification.UnresolvedCopies:N0} unresolved (read-only)"
                : $"Backup: {result.view.PhysicalCopies:N0} physical, " +
                  $"{result.view.ArenaCopies:N0} Arena copies · catalog not loaded";
        }
        catch (Exception)
        {
            if (!closing)
                FilterStatus.Text =
                    "Backup preview failed or file unsupported. Existing collection remains untouched.";
        }
        finally
        {
            if (!closing)
            {
                CollectionBackupButton.IsEnabled = true;
                OpenCatalogButton.IsEnabled = true;
                SyntheticButton.IsEnabled = true;
            }
        }
    }

    private void ShowEmbeddedEdhDraft()
    {
        if (embeddedEdhDraft is null) return;
        EdhLabSurface.Content = embeddedEdhDraft;
        EdhReturnToDraftButton.IsEnabled = true;
        nativeWorkspaceRequested = false;
        edhLabRequested = true;
        UpdateWorkspaceNavigation();
    }

    private void OpenEmbeddedEdhDraft(NativeDeckEditorView editor)
    {
        if (embeddedEdhDraft is not null)
            throw new InvalidOperationException("A draft is already open.");
        embeddedEdhDraft = editor;
        editor.Saved += OnEmbeddedEdhDraftSaved;
        ShowEmbeddedEdhDraft();
    }

    private async void OnEmbeddedEdhDraftSaved(object? sender,
        NativeWorkspaceSafety.Replacement saved)
    {
        if (sender is not NativeDeckEditorView editor ||
            !ReferenceEquals(embeddedEdhDraft, editor)) return;
        DiscardEmbeddedEdhDraft();
        nativeWorkspaceSelectedTab = 2;
        nativeWorkspaceRequested = true;
        edhLabRequested = false;
        UpdateWorkspaceNavigation();
        await RefreshNativeWorkspaceAsync();
    }

    private void DiscardEmbeddedEdhDraft()
    {
        if (embeddedEdhDraft is null) return;
        var old = embeddedEdhDraft;
        if (ReferenceEquals(EdhLabSurface.Content, old))
            EdhLabSurface.Content = null;
        old.Saved -= OnEmbeddedEdhDraftSaved;
        old.DisposeEditor();
        embeddedEdhDraft = null;
        EdhReturnToDraftButton.IsEnabled = false;
    }

    private void OnEdhReturnToDraftClicked(object sender, RoutedEventArgs e) =>
        ShowEmbeddedEdhDraft();

    private async void OnEdhShowCommandersClicked(object sender, RoutedEventArgs e) =>
        await OpenEdhLabAsync(preferDraft: false);

    private async Task OpenSavedDeckInEmbeddedEdhLabAsync(
        NativeDeckEditing.DeckRow selected, EdhLabFormat format,
        string colors, bool focusShortages, string? preferredShortageName)
    {
        var active = catalogSnapshot ??
            throw new InvalidOperationException("Sync the Scryfall catalog before editing.");
        if (embeddedEdhDraft is not null)
        {
            if (NativeInAppDialog.Show(this,
                    "A draft is already open. Discard its unsaved changes and edit the selected deck?",
                    "Switch EDH Lab deck", MessageBoxButton.YesNo,
                    MessageBoxImage.Warning, MessageBoxResult.No)
                != MessageBoxResult.Yes)
            {
                ShowEmbeddedEdhDraft();
                return;
            }
            DiscardEmbeddedEdhDraft();
        }
        // Always verify the CURRENT SHA and catalog at the moment of editing.
        var workspace = await Task.Run(() =>
            NativeWorkspace.Open(NativeWorkspace.DefaultPath));
        if (closing || !ReferenceEquals(active, catalogSnapshot)) return;
        var match = NativeWorkspaceSafety.ListDecks(NativeWorkspace.DefaultPath)
            .FirstOrDefault(x => x.Id == selected.Id);
        if (match is null || match.SourceSha256 != selected.SourceSha256)
            throw new InvalidDataException(
                "The saved deck changed. Refresh the Deck Library before editing.");
        var editor = new NativeDeckEditorView(
            NativeWorkspace.DefaultPath, workspace.BackupSha256,
            workspace.Collection, active.ReadOnlySnapshotPath, match,
            savedLabFormat: format, savedLabColors: colors,
            focusShortageReview: focusShortages,
            preferredShortageName: preferredShortageName);
        OpenEmbeddedEdhDraft(editor);
    }

    private void ClearEmbeddedEdhLab()
    {
        DiscardEmbeddedEdhDraft();
        edhLabRequested = false;
        edhLabWindow = null;
        if (EdhLabSurface is not null)
            EdhLabSurface.Content = null;
        if (EdhLabHost is not null && CatalogWorkspace is not null)
            UpdateWorkspaceNavigation();
    }

    private async void OnEdhLabClicked(object sender, RoutedEventArgs e) =>
        await OpenEdhLabAsync(preferDraft: true);

    private async Task OpenEdhLabAsync(bool preferDraft)
    {
        // EDH sidebar prefers the live draft; explicit Commander browser
        // must work even when saved-deck editing preceded discovery.
        if (preferDraft && embeddedEdhDraft is not null)
        {
            ShowEmbeddedEdhDraft();
            return;
        }
        var active = catalogSnapshot;
        if (active is null) return;
        if (edhLabWindow is not null)
        {
            nativeWorkspaceRequested = false;
            edhLabRequested = true;
            if (preferDraft && embeddedEdhDraft is not null)
                EdhLabSurface.Content = embeddedEdhDraft;
            else EdhLabSurface.Content = edhLabWindow;
            UpdateWorkspaceNavigation();
            return;
        }

        var currentVersion = modeVersion;
        EdhLabButton.IsEnabled = false;
        FilterStatus.Text = "Opening native EDH Lab from isolated catalog…";
        try
        {
            var loaded = await Task.Run(() =>
            {
                var index = EdhLabReadOnlyCache.GetCommanderIndex(active.ReadOnlySnapshotPath);
                // The authoritative native workspace is optional: EDH Lab
                // remains useful as a read-only commander browser without it.
                // Never mutate catalog or user collection during discovery.
                if (!File.Exists(NativeWorkspace.DefaultPath))
                    return (Index: index, Inventory: CommanderInventory.Empty,
                            HasCollection: false);
                try
                {
                    var workspace = NativeWorkspace.Open(NativeWorkspace.DefaultPath);
                    return (Index: index,
                        Inventory: new CommanderInventory(workspace.Collection),
                        HasCollection: true);
                }
                catch (Exception ex) when (ex is IOException or
                    Microsoft.Data.Sqlite.SqliteException or InvalidDataException or
                    UnauthorizedAccessException)
                {
                    return (Index: index, Inventory: CommanderInventory.Empty,
                            HasCollection: false);
                }
            });
            if (closing || currentVersion != modeVersion ||
                !ReferenceEquals(active, catalogSnapshot)) return;
            var view = new CommanderBrowserView(loaded.Index,
                loaded.Inventory, loaded.HasCollection,
                async (commander, format) =>
                {
                    if (closing || !File.Exists(NativeWorkspace.DefaultPath))
                        throw new InvalidOperationException(
                            "Create or import a native collection before saving an EDH Lab draft.");
                    if (currentVersion != modeVersion ||
                        !ReferenceEquals(active, catalogSnapshot))
                        throw new InvalidOperationException(
                            "The catalog changed since this commander list opened. Reopen EDH Lab to build from the current catalog.");
                    // Open a FRESH verified workspace at the moment the user
                    // chooses Build, not the possibly stale browse-time view.
                    var workspace = await Task.Run(() =>
                        NativeWorkspace.Open(NativeWorkspace.DefaultPath));
                    if (closing) return;
                    if (embeddedEdhDraft is not null)
                    {
                        if (NativeInAppDialog.Show(this,
                                "A draft is already open. Discard that unsaved draft and start a new one?",
                                "Change EDH Lab draft", MessageBoxButton.YesNo,
                                MessageBoxImage.Warning, MessageBoxResult.No)
                            != MessageBoxResult.Yes)
                        {
                            ShowEmbeddedEdhDraft();
                            return;
                        }
                        DiscardEmbeddedEdhDraft();
                    }
                    var editor = new NativeDeckEditorView(
                        NativeWorkspace.DefaultPath, workspace.BackupSha256,
                        workspace.Collection, active.ReadOnlySnapshotPath,
                        draftCommander: commander.Name,
                        draftFormat: format, draftColors: commander.ColorIdentity);
                    OpenEmbeddedEdhDraft(editor);
                }, catalogPath: active.ReadOnlySnapshotPath);
            edhLabWindow = view;
            EdhLabSurface.Content = view;
            nativeWorkspaceRequested = false;
            edhLabRequested = true;
            UpdateWorkspaceNavigation();
            EdhLabButton.IsEnabled = true;
            FilterStatus.Text = $"{loaded.Index.LoadedCardIdentities:N0} read-only commander candidates loaded" +
                (loaded.HasCollection ? " · inventory name counts available" :
                    " · no native inventory loaded");
        }
        catch (Exception)
        {
            if (!closing)
                FilterStatus.Text = "EDH Lab needs a fully indexed Scryfall card catalog. Current collection data is unchanged.";
        }
        finally
        {
            if (!closing)
                EdhLabButton.IsEnabled = catalogSnapshot is not null;
        }
    }

    private void OnSyntheticClicked(object sender, RoutedEventArgs e)
    {
        CancelPendingSearch();
        ClearEmbeddedEdhLab();
        collectionWindow?.Close();
        collectionWindow = null;
            InvalidateNativeWorkspace();
        EdhLabButton.IsEnabled = false;
        SaveNativeCatalogButton.IsEnabled = false;
        modeVersion++;
        var previous = catalogSnapshot;
        EdhLabReadOnlyCache.Clear();
                catalogSnapshot = null;
        syntheticMode = true;
        previous?.Dispose();
        nativeWorkspaceRequested = false; // demo is a catalog-only mode
        UpdateWorkspaceNavigation();
        ModeStatus.Text = "Synthetic mode · No catalog file open. Nothing is imported, saved, or synced.";
        SearchBox.Text = "";
        _ = FilterAsync();
    }

    private async void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        if (sampleCards is null) return; // can fire inside InitializeComponent
        await FilterAsync();
    }
    private void OnCatalogFilterChanged(object sender, RoutedEventArgs e)
    {
        if (sampleCards is null || SearchBox is null) return;
        if (sender == ColorlessOnlyBox && ColorlessOnlyBox.IsChecked == true)
        {
            // Mutually exclusive choices: colorless isn't a sixth mana color.
            foreach (System.Windows.Controls.Primitives.ToggleButton box in
                     new[] { ColorWBox, ColorUBox, ColorBBox, ColorRBox, ColorGBox })
                box.IsChecked = false;
        }
        else if (sender is System.Windows.Controls.Primitives.ToggleButton box &&
                 box.IsChecked == true &&
                 (sender == ColorWBox || sender == ColorUBox || sender == ColorBBox ||
                  sender == ColorRBox || sender == ColorGBox) && ColorlessOnlyBox is not null)
            ColorlessOnlyBox.IsChecked = false;
        if (sender == OwnershipFilterBox)
        {
            bool physicalMode = OwnershipFilterBox?.SelectedIndex == 1;
            if (CatalogIncludeProxiesBox is not null)
            {
                CatalogIncludeProxiesBox.IsEnabled = physicalMode;
                if (!physicalMode) CatalogIncludeProxiesBox.IsChecked = false;
            }
        }
        // ComboBox/checkbox selections are complete values; unlike typing,
        // they do not need the search box's 140 ms debounce.
        _ = FilterAsync(debounce: false);
    }

    private CatalogSnapshot.SearchOptions SelectedCatalogFilters()
    {
        string colors =
            (ColorWBox?.IsChecked == true ? "W" : "") +
            (ColorUBox?.IsChecked == true ? "U" : "") +
            (ColorBBox?.IsChecked == true ? "B" : "") +
            (ColorRBox?.IsChecked == true ? "R" : "") +
            (ColorGBox?.IsChecked == true ? "G" : "");
        return new CatalogSnapshot.SearchOptions(
            SetFilterBox?.Text ?? "",
            (RarityFilterBox?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "",
            TypeFilterBox?.Text ?? "",
            colors,
            (CatalogSnapshot.ColorMatching)Math.Clamp(
                ColorMatchBox?.SelectedIndex ?? 0, 0, 2),
            ColorlessOnlyBox?.IsChecked == true,
            ArenaOnlyBox?.IsChecked == true,
            (CatalogSnapshot.OwnershipMode)Math.Clamp(
                OwnershipFilterBox?.SelectedIndex ?? 0, 0, 4),
            (RoleFilterBox?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "",
            CatalogIncludeProxiesBox?.IsChecked == true &&
                OwnershipFilterBox?.SelectedIndex == 1);
    }

    private async Task FilterAsync(bool debounce = true, bool resetPage = true)
    {
        if (resetPage) catalogPageIndex = 0;
        CancelPendingSearch();
        SetCatalogPageBusy();
        var cts = new CancellationTokenSource();
        searchCancellation = cts;
        var token = cts.Token;
        var filter = SearchBox.Text.Trim();
        var filters = SelectedCatalogFilters();
        var ownership = catalogOwnership;
        var active = catalogSnapshot;
        var version = modeVersion;
        try
        {
            if (debounce) await Task.Delay(140, token);
            if (active is null)
            {
                if (!syntheticMode)
                {
                    if (closing || version != modeVersion ||
                        !ReferenceEquals(searchCancellation, cts)) return;
                    SetCatalogResults(Array.Empty<object>());
                    SetCatalogPageState(0, false);
                    FilterStatus.Text = "No Scryfall catalog loaded — native ownership is separate";
                    return;
                }
                var started = Stopwatch.GetTimestamp();
                var list = await Task.Run(() => sampleCards.Where(c =>
                    c.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    c.SetCode.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    c.CollectorNumber.Contains(filter, StringComparison.OrdinalIgnoreCase))
                    .Skip(checked(catalogPageIndex * CatalogPageSize))
                    .Take(CatalogPageSize + 1).ToArray(), token);
                token.ThrowIfCancellationRequested();
                if (closing || version != modeVersion || !ReferenceEquals(searchCancellation, cts)) return;
                bool more = list.Length > CatalogPageSize;
                SetCatalogResults(list.Take(CatalogPageSize).Cast<object>());
                SetCatalogPageState(Math.Min(list.Length, CatalogPageSize), more);
                FilterStatus.Text = $"Synthetic catalog · Page {catalogPageIndex + 1:N0} · {Stopwatch.GetElapsedTime(started).TotalMilliseconds:F1} ms";
            }
            else
            {
                var results = await Task.Run(() =>
                    active.SearchFiltered(filter, filters, cancellationToken: token,
                        ownership: ownership,
                        offset: checked(catalogPageIndex * CatalogPageSize)), token);
                token.ThrowIfCancellationRequested();
                if (closing || version != modeVersion || !ReferenceEquals(searchCancellation, cts)) return;
                SetCatalogResults(results.Rows.Cast<object>());
                SetCatalogPageState(results.Rows.Count, results.MayHaveMore);
                FilterStatus.Text = $"{results.Rows.Count:N0} printings on page {catalogPageIndex + 1:N0} · SQL {results.SqlMilliseconds:F1} ms" +
                    (results.MayHaveMore ? " · More catalog pages available" : " · Last page") +
                    (ownership is not null
                        ? " · Physical by verified printing ID; Arena by card name"
                        : " · No native ownership loaded");
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { } // old snapshot closed after switching modes
        catch (InvalidDataException ex)
        {
            if (!closing && version == modeVersion && ReferenceEquals(searchCancellation, cts))
            {
                SetCatalogPageState(0, false);
                FilterStatus.Text = ex.Message;
            }
        }
        catch (Exception)
        {
            if (!closing && version == modeVersion && ReferenceEquals(searchCancellation, cts))
            {
                SetCatalogPageState(0, false);
                FilterStatus.Text = "Catalog query failed; existing snapshot was not modified";
            }
        }
        finally
        {
            if (ReferenceEquals(searchCancellation, cts))
            {
                searchCancellation = null;
                if (catalogPageBusy) SetCatalogPageState(0, false);
            }
            cts.Dispose();
        }
    }
}
