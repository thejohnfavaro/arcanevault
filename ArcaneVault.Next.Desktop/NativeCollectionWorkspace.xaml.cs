using ArcaneVault.Next.Collection;
using Microsoft.Win32;
using System.IO;
using ArcaneVault.Next.Domain;
using ArcaneVault.Next.EDHLab;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// Read-only backup viewer with optional verification against the same isolated
/// SQLite catalog snapshot used by the native Card Browser.
/// </summary>
public partial class NativeCollectionWorkspace : UserControl
{
    private readonly NativeBackupView backup;
    private readonly CollectionCatalogReview.Result review;
    private readonly string? catalogPath;
    private readonly NativeSetProgress.Result? progress;
    private NativeSetProgress.Result? arenaProgress;
    private bool arenaProgressLoading;
    private sealed record SetTileRow(IReadOnlyList<NativeSetProgress.SetRow> Cards);
    private NativeSetProgress.SetRow? selectedSetRow;
    private IReadOnlyList<NativeSetProgress.SetSlot> allSetSlots = Array.Empty<NativeSetProgress.SetSlot>();
    private int setColumns;
    private int setQueryVersion;
    private bool closing;
    private readonly string? nativePath;
    private readonly string? expectedBackupSha;
    private readonly Func<Task>? reopenNative;
    private IReadOnlyList<NativeDeckEditing.DeckRow> deckRows;
    private bool deckCoversLoading;
    private bool deckCoversReady;
    private bool deckBusy;
    private GridView? physicalDetailedView;
    private bool excelBusy;
    private bool arenaImportBusy;
    private bool valuationBusy;
    private NativePhysicalValuation.Result? valuation;
    private int valuationVersion;

    public NativeCollectionWorkspace(NativeBackupView backup,
        CollectionCatalogReview.Result review, string? catalogPath,
        NativeSetProgress.Result? progress, bool nativeWorkspace = false,
        string? nativePath = null, string? expectedBackupSha = null,
        Func<Task>? reopenNative = null,
        IReadOnlyList<NativeDeckEditing.DeckRow>? nativeDecks = null)
    {
        this.backup = backup ?? throw new ArgumentNullException(nameof(backup));
        this.review = review ?? throw new ArgumentNullException(nameof(review));
        this.catalogPath = catalogPath;
        this.progress = progress;
        this.nativePath = nativePath;
        this.expectedBackupSha = expectedBackupSha;
        this.reopenNative = reopenNative;
        deckRows = nativeDecks ?? Array.Empty<NativeDeckEditing.DeckRow>();
        InitializeComponent();
        CollectionTabs.SelectionChanged += (_, e) =>
        {
            if (ReferenceEquals(e.OriginalSource, CollectionTabs))
                SectionChanged?.Invoke(CollectionTabs.SelectedIndex);
        };
        physicalDetailedView = PhysicalList.View as GridView;
        RefreshDeckRows();
        AddDeckButton.IsEnabled = nativeWorkspace &&
            !string.IsNullOrWhiteSpace(nativePath) &&
            !string.IsNullOrWhiteSpace(expectedBackupSha);
        DeckLibraryStatus.Text = nativeWorkspace
            ? $"Saved decks: {deckRows.Count:N0}. Select a deck to edit or delete. " +
              "Saved deck cards do not change ownership; no format legality is implied."
            : "Read-only backup preview: create or open a native workspace to edit its saved decks.";
        AddPhysicalButton.IsEnabled = nativeWorkspace &&
            !string.IsNullOrWhiteSpace(nativePath) &&
            !string.IsNullOrWhiteSpace(expectedBackupSha) &&
            !string.IsNullOrWhiteSpace(catalogPath);
        AdjustPhysicalButton.IsEnabled = nativeWorkspace &&
            !string.IsNullOrWhiteSpace(nativePath) &&
            !string.IsNullOrWhiteSpace(expectedBackupSha);
        ImportExcelButton.IsEnabled = nativeWorkspace &&
            !string.IsNullOrWhiteSpace(nativePath) &&
            !string.IsNullOrWhiteSpace(expectedBackupSha);
        AddArenaButton.IsEnabled = nativeWorkspace &&
            !string.IsNullOrWhiteSpace(nativePath) &&
            !string.IsNullOrWhiteSpace(expectedBackupSha);
        EditArenaButton.IsEnabled = AddArenaButton.IsEnabled;
        ImportArenaButton.IsEnabled = AddArenaButton.IsEnabled;
        if (nativeWorkspace)
        {
            DataSourceLabel.Text =
                "Native SQLite — physical, Arena and saved-deck edits each preserve a complete rollback";
        }
        Unloaded += (_, _) =>
        {
            closing = true;
            setQueryVersion++;
            valuationVersion++;
        };
        Loaded += async (_, _) =>
        {
            _ = LoadDeckCoversAsync();
            await CalculateValuationAsync();
        };
        TotalsText.Text =
            $"Physical: {backup.PhysicalCopies:N0} copies · {backup.PhysicalCards.Count:N0} lines · " +
            $"{backup.DistinctPhysicalNames:N0} names  |  " +
            $"Arena: {backup.ArenaCopies:N0} copies  |  " +
            $"Decks: {backup.Decks:N0} ({backup.DeckRevisions:N0} revisions)  |  " +
            $"Preferred artwork: {backup.PreferredArtworks:N0}";
        VerificationText.Text = review.CatalogVerified
            ? $"Catalog-verified: {review.ConfirmedCopies:N0} physical copies across " +
              $"{review.ConfirmedDistinctPrintings:N0} distinct printings and {review.ConfirmedSets.Count:N0} sets  |  " +
              $"Unresolved: {review.UnresolvedCopies:N0} copies"
            : $"No catalog selected — none of the {review.TotalCopies:N0} physical copies " +
              "can be credited to a confirmed set yet.";
        RefreshSetList();
        RefreshLists();
    }

    /// <summary>Host-controlled sidebar selection; the data and edit actions
    /// are unchanged from the standalone collection view.</summary>
    public event Action<int>? SectionChanged;
    public int SelectedSection => CollectionTabs.SelectedIndex;
    public void NavigateTo(int tabIndex)
    {
        if (tabIndex is < 0 or > 3)
            throw new ArgumentOutOfRangeException(nameof(tabIndex));
        CollectionTabs.SelectedIndex = tabIndex;
    }

    /// <summary>
    /// Legacy editor flows call Close before reopening from the new SHA.
    /// In an embedded workspace, mark this view inactive; the host replaces
    /// it when the native workspace is reverified.
    /// </summary>
    private void Close()
    {
        closing = true;
        IsEnabled = false; // never allow stale collection edits during a SHA refresh
        setQueryVersion++;
        valuationVersion++;
    }

    private void RefreshLists()
    {
        if (SearchBox is null || MissingOnly is null || PhysicalList is null ||
            ArenaList is null || StatusText is null) return;

        var search = SearchBox.Text.Trim();
        var unresolved = MissingOnly.IsChecked == true;
        // Wrap exact original row references for price display; editing always
        // resolves the same source ordinal, regardless of price sort order.
        IEnumerable<NativePhysicalValuation.Line> physical =
            valuation?.Lines ?? review.Rows.Select((row, ordinal) =>
                new NativePhysicalValuation.Line(
                    ordinal, row, null, null, "Prices not calculated yet")).ToArray();
        physical = physical.Where(x =>
            (!unresolved || !x.Source.Confirmed) &&
            (search.Length == 0 ||
             x.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
             x.Source.Original.SetCode.Contains(search, StringComparison.OrdinalIgnoreCase) ||
             x.Source.Original.CollectorNumber.Contains(search, StringComparison.OrdinalIgnoreCase) ||
             x.Status.Contains(search, StringComparison.OrdinalIgnoreCase)));
        int sort = PhysicalPriceSort?.SelectedIndex ?? 0;
        physical = sort switch
        {
            1 => physical.OrderBy(x => !x.UnitUsd.HasValue)
                .ThenByDescending(x => x.UnitUsd).ThenBy(x => x.Ordinal),
            2 => physical.OrderBy(x => !x.UnitUsd.HasValue)
                .ThenBy(x => x.UnitUsd).ThenBy(x => x.Ordinal),
            3 => physical.OrderBy(x => !x.LineUsd.HasValue)
                .ThenByDescending(x => x.LineUsd).ThenBy(x => x.Ordinal),
            4 => physical.OrderBy(x => !x.LineUsd.HasValue)
                .ThenBy(x => x.LineUsd).ThenBy(x => x.Ordinal),
            _ => physical.OrderBy(x => x.Ordinal)
        };
        var shownPhysical = physical.ToArray();
        var arena = backup.ArenaCards.Where(x =>
            search.Length == 0 || x.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        PhysicalList.ItemsSource = shownPhysical;
        ArenaList.ItemsSource = arena;
        StatusText.Text = $"{shownPhysical.Length:N0} physical lines and {arena.Length:N0} Arena names shown. " +
            "Prices are read-only Scryfall estimates; missing data is not zero. " +
            "Double-click for offline details. Only verified paper printings count in Set Progress.";
    }

    private void OnPhysicalSortChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PhysicalList is null) return; // InitializeComponent triggers selection.
        RefreshLists();
    }

    private async void OnRecalculatePricesClicked(object sender, RoutedEventArgs e)
        => await CalculateValuationAsync();

    private async Task CalculateValuationAsync()
    {
        if (closing || valuationBusy || PhysicalValuationSummary is null) return;
        valuationBusy = true;
        int generation = ++valuationVersion;
        RecalculatePricesButton.IsEnabled = false;
        PhysicalValuationSummary.Text = "Calculating verified physical printing and finish prices…";
        try
        {
            var result = await Task.Run(() =>
                NativePhysicalValuation.Calculate(review, catalogPath));
            if (closing || generation != valuationVersion) return;
            valuation = result;
            PhysicalValuationSummary.Text = result.Summary;
            PhysicalValuationTimestamp.Text = result.SnapshotStatus +
                " • Estimated resale/market reference only; not a live shop quote or offer.";
            RefreshLists();
        }
        catch (Exception ex)
        {
            if (!closing && generation == valuationVersion)
            {
                PhysicalValuationSummary.Text = "Valuation unavailable; ownership was not changed.";
                PhysicalValuationTimestamp.Text =
                    "Could not safely calculate cached prices: " + ex.Message;
            }
        }
        finally
        {
            valuationBusy = false;
            if (!closing) RecalculatePricesButton.IsEnabled = true;
        }
    }

    private void OnPhysicalViewModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PhysicalList is null || physicalDetailedView is null ||
            PhysicalViewMode is null) return;
        object? selected = PhysicalList.SelectedItem;
        if (PhysicalViewMode.SelectedIndex == 1)
        {
            var compact = new GridView();
            compact.Columns.Add(new GridViewColumn
            { Header = "Card", Width = 325, DisplayMemberBinding = new System.Windows.Data.Binding("Name") });
            compact.Columns.Add(new GridViewColumn
            { Header = "Qty", Width = 55, DisplayMemberBinding = new System.Windows.Data.Binding("Quantity") });
            compact.Columns.Add(new GridViewColumn
            { Header = "Set / #", Width = 115, DisplayMemberBinding = new System.Windows.Data.Binding("SetAndNumber") });
            compact.Columns.Add(new GridViewColumn
            { Header = "Unit USD", Width = 90,
              DisplayMemberBinding = new System.Windows.Data.Binding("UnitLabel") });
            compact.Columns.Add(new GridViewColumn
            { Header = "Line USD", Width = 95,
              DisplayMemberBinding = new System.Windows.Data.Binding("LineLabel") });
            PhysicalList.View = compact;
        }
        else PhysicalList.View = physicalDetailedView;
        PhysicalList.SelectedItem = selected;
    }

    private async void OnImportExcelClicked(object sender, RoutedEventArgs e)
    {
        if (excelBusy || closing || string.IsNullOrWhiteSpace(nativePath) ||
            string.IsNullOrWhiteSpace(expectedBackupSha)) return;
        var picker = new OpenFileDialog
        {
            Title = "Import physical MTG collection quantities from CSV or Excel",
            Filter = "Collection files (*.csv;*.xlsx)|*.csv;*.xlsx|CSV files (*.csv)|*.csv|Excel workbooks (*.xlsx)|*.xlsx",
            CheckFileExists = true,
            Multiselect = false,
            InitialDirectory = Environment.GetFolderPath(
                Environment.SpecialFolder.MyDocuments)
        };
        if (picker.ShowDialog(Window.GetWindow(this)) != true) return;
        excelBusy = true;
        ImportExcelButton.IsEnabled = false;
        StatusText.Text = "Reading CSV/Excel and comparing existing physical printing quantities…";
        try
        {
            var (rows, diff) = await Task.Run(() =>
            {
                var source = Path.GetExtension(picker.FileName).Equals(
                    ".csv", StringComparison.OrdinalIgnoreCase)
                    ? NativeCsvCollectionImport.Read(picker.FileName)
                    : NativeExcelCollectionImport.Read(picker.FileName);
                var comparison = NativeWorkspaceSafety.PreviewPhysicalReconciliation(
                    nativePath, catalogPath, source, expectedBackupSha);
                return (source, comparison);
            });
            if (closing) return;
            var review = new NativeImportDiffWindow(
                Path.GetFileName(picker.FileName), diff)
            { Owner = Window.GetWindow(this) };
            if (review.ShowDialog() != true || !diff.HasChanges)
            {
                StatusText.Text = diff.HasChanges
                    ? "Physical reconciliation canceled; collection unchanged."
                    : "Physical collection already matches all imported quantities. No writes or backups created.";
                return;
            }

            StatusText.Text = "Applying reviewed physical differences with rollback verification…";
            var outcome = await Task.Run(() =>
                NativeWorkspaceSafety.ReconcilePhysicalImport(
                    nativePath, catalogPath, rows, expectedBackupSha,
                    diff.CandidateSha256));
            if (closing) return;
            MessageBox.Show(Window.GetWindow(this),
                "Physical reconciliation completed.\n" + diff.Summary + "\n\n" +
                "Complete prior collection recovery: " + outcome.RecoveryPath,
                "Physical collection reconciled", MessageBoxButton.OK,
                MessageBoxImage.Information);
            Close();
            if (reopenNative is not null) await reopenNative();
        }
        catch (Exception ex)
        {
            if (!closing)
            {
                StatusText.Text = "Collection import stopped: " + ex.Message;
                MessageBox.Show(Window.GetWindow(this),
                    "Collection import could not be verified. The active collection " +
                    "was not intentionally changed.\n\n" + ex.Message,
                    "Collection import stopped", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        finally
        {
            excelBusy = false;
            if (!closing) ImportExcelButton.IsEnabled =
                !string.IsNullOrWhiteSpace(nativePath) &&
                !string.IsNullOrWhiteSpace(expectedBackupSha);
        }
    }

    private void OnFilterChanged(object sender, RoutedEventArgs e) => RefreshLists();

    private bool ArenaSetScope => SetScopeBox?.SelectedIndex == 1;

    private void SetDetailsMode(bool show)
    {
        if (SetSummaryList is null || SetSlotsList is null ||
            SetDetailsControls is null || SetBrowseRow is null ||
            SetChecklistRow is null) return;
        SetBrowseRow.Height = show ? new GridLength(0) :
            new GridLength(1, GridUnitType.Star);
        SetChecklistRow.Height = show ? new GridLength(1, GridUnitType.Star) :
            new GridLength(0);
        SetSummaryList.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
        SetDetailsControls.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        SetSlotsList.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        FindSetLabel.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
        SetSearch.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
        ShowEmptySets.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
        if (show)
            HideUnownedSetSlots.Content = ArenaSetScope
                ? "Hide unowned Arena card names"
                : "Hide unowned physical printings";
    }

    private void OnBackToSetsClicked(object sender, RoutedEventArgs e) =>
        RefreshSetList();

    private void OnSetOwnedFilterChanged(object sender, RoutedEventArgs e) =>
        RefreshSetSlotFilter();

    private void RefreshSetSlotFilter()
    {
        if (SetSlotsList is null || HideUnownedSetSlots is null) return;
        SetSlotsList.ItemsSource = HideUnownedSetSlots.IsChecked == true
            ? allSetSlots.Where(x => x.VerifiedCopies > 0).ToArray()
            : allSetSlots;
    }



    private void RefreshSetList(bool preserveDetails = false)
    {
        if (SetSummaryList is null || ShowEmptySets is null ||
            SetSearch is null || SetDetailsHeader is null || SetSlotsList is null) return;

        bool arena = ArenaSetScope;
        var active = arena ? arenaProgress : progress;
        string query = SetSearch.Text.Trim();
        bool includeEmpty = ShowEmptySets.IsChecked == true;
        var items = (active?.Sets ?? Array.Empty<NativeSetProgress.SetRow>())
            .Where(x => (includeEmpty || x.OwnedSlots > 0) &&
                (query.Length == 0 ||
                 x.SetCode.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                 x.SetName.Contains(query, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        if (!preserveDetails)
        {
            setQueryVersion++;
            selectedSetRow = null;
            allSetSlots = Array.Empty<NativeSetProgress.SetSlot>();
            SetSlotsList.ItemsSource = null;
            SetDetailsMode(false);
        }
        int cols = SetColumnsForWidth(SetSummaryList.ActualWidth);
        setColumns = cols;
        SetSummaryList.ItemsSource = items.Chunk(cols)
            .Select(group => new SetTileRow(group)).ToArray();

        if (active is null)
            SetDetailsHeader.Text = arena && arenaProgressLoading
                ? "Loading Arena-available card names from the local catalog…"
                : "Load the local Scryfall catalog before viewing this collection scope.";
        else if (items.Length == 0)
            SetDetailsHeader.Text = arena
                ? "No matching Arena name coverage. Include sets with 0 names to browse Arena-available sets."
                : "No matching sets with verified owned collector slots. Include sets with 0 verified slots to inspect the catalog.";
        else
            SetDetailsHeader.Text = arena
                ? $"Showing {items.Length:N0} Arena sets. This is CARD-NAME coverage, not ownership of specific Arena styles or set printings; reprinted names may appear in multiple sets."
                : $"Showing {items.Length:N0} sets. Select a set to inspect its verified paper collector-number checklist.";
    }

    private static int SetColumnsForWidth(double width) =>
        width >= 1240 ? 3 : width >= 760 ? 2 : 1;

    private void OnSetSummarySizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (SetSummaryList is null || SetSearch is null) return;
        int columns = SetColumnsForWidth(SetSummaryList.ActualWidth);
        if (columns != setColumns) RefreshSetList(preserveDetails: true);
    }

    private async void OnSetTileClicked(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement
            { DataContext: NativeSetProgress.SetRow selected }) return;
        e.Handled = true;
        selectedSetRow = selected;
        SetDetailsMode(true);
        await LoadSetDetailsAsync(selected);
    }

    private void OnSetFilterChanged(object sender, RoutedEventArgs e) => RefreshSetList();

    private async void OnSetScopeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SetScopeBox is null || SetSearch is null) return;
        setQueryVersion++;
        if (ArenaSetScope && arenaProgress is null && !arenaProgressLoading &&
            !string.IsNullOrWhiteSpace(catalogPath))
        {
            arenaProgressLoading = true;
            RefreshSetList();
            try
            {
                var snapshot = await Task.Run(() =>
                    expectedBackupSha is not null &&
                    System.IO.Path.GetFullPath(catalogPath).Equals(
                        System.IO.Path.GetFullPath(NativeWorkspace.CatalogPath),
                        StringComparison.OrdinalIgnoreCase)
                        ? NativeSetProgressCache.Arena(backup.ArenaCards,
                            catalogPath, expectedBackupSha)
                        : NativeSetProgress.BuildArena(backup.ArenaCards, catalogPath));
                if (closing) return;
                arenaProgress = snapshot;
            }
            catch (Exception ex)
            {
                if (!closing)
                    SetDetailsHeader.Text =
                        "Arena coverage unavailable: " + ex.Message + ". Collection unchanged.";
            }
            finally
            {
                arenaProgressLoading = false;
            }
        }
        if (!closing) RefreshSetList();
    }

    private async Task LoadSetDetailsAsync(NativeSetProgress.SetRow selected)
    {
        if (SetSlotsList is null || SetDetailsHeader is null ||
            string.IsNullOrWhiteSpace(catalogPath)) return;

        int version = ++setQueryVersion;
        allSetSlots = Array.Empty<NativeSetProgress.SetSlot>();
        SetSlotsList.ItemsSource = null;
        bool arena = selected.NameCoverage;

        SetDetailsHeader.Text = arena
            ? $"{selected.SetName} ({selected.SetCode}) · loading Arena-available card names…"
            : $"{selected.SetName} ({selected.SetCode}) · loading verified paper slots…";
        try
        {
            var slots = await Task.Run(() => arena
                ? NativeSetProgress.LoadArenaSetNames(
                    backup.ArenaCards, catalogPath, selected.SetCode)
                : NativeSetProgress.LoadSetSlots(review, catalogPath, selected.SetCode));
            if (closing || version != setQueryVersion) return;
            if (slots.Count != selected.TotalSlots)
                throw new InvalidDataException("Set counts differ from the catalog checklist.");
            allSetSlots = slots;
            RefreshSetSlotFilter();
            SetDetailsHeader.Text = arena
                ? $"{selected.SetName} · {selected.SlotsLabel} ({selected.PercentLabel}). " +
                  "Arena quantities are name-only: a match does not verify a style or printing. " +
                  "Collector numbers below are catalog references, not ownership evidence."
                : $"{selected.SetName} · {selected.SlotsLabel} verified slots " +
                  $"({selected.PercentLabel}), {selected.VerifiedCopies:N0} physical copies · " +
                  "double-click a card for offline details.";
        }
        catch (Exception ex)
        {
            if (!closing && version == setQueryVersion)
                SetDetailsHeader.Text =
                    "Unable to display this catalog set: " + ex.Message +
                    ". Backup data remains unchanged.";
        }
    }

    private async Task LoadDeckCoversAsync()
    {
        if (closing || deckCoversLoading || deckCoversReady ||
            string.IsNullOrWhiteSpace(catalogPath) ||
            string.IsNullOrWhiteSpace(expectedBackupSha)) return;
        string[] commanders = deckRows
            .Where(x => x.Format.Equals("Commander", StringComparison.OrdinalIgnoreCase) ||
                        x.Format.Equals("Arena Brawl", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Commander)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (commanders.Length == 0) { deckCoversReady = true; return; }
        deckCoversLoading = true;
        try
        {
            // Only local metadata is read here; the virtualized cover images
            // fetch bytes through the existing throttled on-screen image cache.
            var covers = await Task.Run(() => NativeDeckCoverCache.Get(
                commanders, catalogPath!, expectedBackupSha!));
            if (closing) return;
            deckRows = deckRows.Select(x =>
            {
                bool commanderFormat = x.Format.Equals("Commander",
                    StringComparison.OrdinalIgnoreCase) ||
                    x.Format.Equals("Arena Brawl", StringComparison.OrdinalIgnoreCase);
                return commanderFormat && covers.TryGetValue(x.Commander, out var url)
                    ? x with { CoverArtworkUrl = url }
                    : x;
            }).ToArray();
            deckCoversReady = true;
            RefreshDeckRows();
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or
            UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException or
            ArgumentException)
        {
            if (!closing)
                DeckLibraryStatus.Text = "Saved decks are available, but commander cover " +
                    "art could not be loaded: " + ex.Message;
        }
        finally { deckCoversLoading = false; }
    }

    private void OnDeckFilterChanged(object sender, RoutedEventArgs e) => RefreshDeckRows();

    private void RefreshDeckRows()
    {
        if (DeckLibraryList is null) return;
        string query = DeckSearchBox?.Text.Trim() ?? "";
        bool favorites = FavoriteDecksOnly?.IsChecked == true;
        var selected = DeckLibraryList.SelectedItem as NativeDeckEditing.DeckRow;
        var filtered = deckRows
            .Where(x => (!favorites || x.Favorite) &&
                (query.Length == 0 ||
                 x.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                 x.Commander.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                 x.Folder.Contains(query, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(x => x.Favorite)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        DeckLibraryList.ItemsSource = filtered;
        // Applying asynchronous cached cover metadata creates new display
        // record instances. Keep the deck selection by stable saved identity.
        if (selected is not null)
        {
            var current = filtered.FirstOrDefault(x =>
                x.Ordinal == selected.Ordinal &&
                x.SourceSha256.Equals(selected.SourceSha256,
                    StringComparison.OrdinalIgnoreCase));
            if (current is not null) DeckLibraryList.SelectedItem = current;
        }
    }

    private void OnDeckSelected(object sender, SelectionChangedEventArgs e)
    {
        if (DeckLibraryList is null || EditDeckButton is null ||
            DeleteDeckButton is null || DuplicateDeckButton is null) return;
        var selectedDeck = DeckLibraryList.SelectedItem as NativeDeckEditing.DeckRow;
        bool editable = selectedDeck?.Editable == true && !deckBusy &&
            !string.IsNullOrWhiteSpace(expectedBackupSha);
        EditDeckButton.IsEnabled = editable;
        EditDeckInEdhLabButton.IsEnabled = editable &&
            !string.IsNullOrWhiteSpace(catalogPath) &&
            File.Exists(catalogPath) &&
            selectedDeck is not null &&
            !string.IsNullOrWhiteSpace(selectedDeck.Commander) &&
            EdhLabSavedDeckAccess.FormatFor(selectedDeck.Format) is not null;
        DeleteDeckButton.IsEnabled = !deckBusy &&
            DeckLibraryList.SelectedItem is NativeDeckEditing.DeckRow &&
            !string.IsNullOrWhiteSpace(expectedBackupSha);
        DuplicateDeckButton.IsEnabled = DeleteDeckButton.IsEnabled;
        if (DeckLibraryList.SelectedItem is NativeDeckEditing.DeckRow selected &&
            !selected.Editable)
            DeckLibraryStatus.Text =
                "This imported deck contains unsupported or ambiguous entries. " +
                "It is shown read-only; you may delete it with recovery protection, " +
                "but cannot edit it until its entries are repaired.";
    }

    private async void OnAddDeckClicked(object sender, RoutedEventArgs e)
    {
        if (closing || string.IsNullOrWhiteSpace(nativePath) ||
            string.IsNullOrWhiteSpace(expectedBackupSha))
        {
            DeckLibraryStatus.Text = "Create a native collection before saving a deck.";
            return;
        }
        var dialog = new NativeDeckEditorWindow(nativePath, expectedBackupSha, backup, catalogPath)
        { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true) return;
        Close();
        if (reopenNative is not null) await reopenNative();
    }

    private async void OnEditDeckClicked(object sender, RoutedEventArgs e)
    {
        if (deckBusy || closing || string.IsNullOrWhiteSpace(nativePath) ||
            string.IsNullOrWhiteSpace(expectedBackupSha) ||
            DeckLibraryList.SelectedItem is not NativeDeckEditing.DeckRow selected)
        {
            DeckLibraryStatus.Text = "Select a saved native deck first.";
            return;
        }
        if (!selected.Editable)
        {
            DeckLibraryStatus.Text = "This imported deck contains unsupported entries; " +
                "editing is blocked to prevent data loss.";
            return;
        }
        var dialog = new NativeDeckEditorWindow(
            nativePath, expectedBackupSha, backup, catalogPath, selected) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true) return;
        Close();
        if (reopenNative is not null) await reopenNative();
    }

    private async void OnEditDeckInEdhLabClicked(object sender, RoutedEventArgs e)
    {
        if (deckBusy || closing || string.IsNullOrWhiteSpace(nativePath) ||
            string.IsNullOrWhiteSpace(expectedBackupSha) ||
            string.IsNullOrWhiteSpace(catalogPath) || !File.Exists(catalogPath) ||
            DeckLibraryList.SelectedItem is not NativeDeckEditing.DeckRow selected ||
            !selected.Editable ||
            EdhLabSavedDeckAccess.FormatFor(selected.Format) is not EdhLabFormat format ||
            string.IsNullOrWhiteSpace(selected.Commander))
        {
            DeckLibraryStatus.Text =
                "Select an editable Commander or Arena Brawl deck with a commander " +
                "and a synced local Scryfall catalog.";
            return;
        }

        deckBusy = true;
        EditDeckButton.IsEnabled = false;
        EditDeckInEdhLabButton.IsEnabled = false;
        AddDeckButton.IsEnabled = false;
        DeleteDeckButton.IsEnabled = false;
        DuplicateDeckButton.IsEnabled = false;
        DeckLibraryStatus.Text = "Verifying saved commander against local format rules…";
        try
        {
            // The saved format and commander name are untrusted user data.
            // Revalidate the EXACT name, commander eligibility and format
            // from the current read-only catalog before offering role guidance.
            var commander = await Task.Run(() =>
            {
                var index = CommanderCatalog.OpenReadOnly(catalogPath!);
                return EdhLabSavedDeckAccess.FindEligibleCommander(
                    index, selected.Commander, format);
            });
            if (closing) return;
            if (commander is null)
            {
                DeckLibraryStatus.Text =
                    "This saved commander is not verified as eligible in the current " +
                    "local catalog for this format. Refresh Scryfall metadata, or " +
                    "use Edit selected deck to make manual changes. No data changed.";
                return;
            }

            var dialog = new NativeDeckEditorWindow(
                nativePath, expectedBackupSha, backup, catalogPath, selected,
                savedLabFormat: format, savedLabColors: commander.ColorIdentity)
            { Owner = Window.GetWindow(this) };
            if (dialog.ShowDialog() != true) return;
            Close();
            if (reopenNative is not null) await reopenNative();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or
            UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException or
            ArgumentException)
        {
            if (!closing)
                DeckLibraryStatus.Text = "EDH Lab could not verify this saved deck: " +
                    ex.Message + ". Nothing was changed.";
        }
        finally
        {
            deckBusy = false;
            if (!closing)
            {
                AddDeckButton.IsEnabled = !string.IsNullOrWhiteSpace(expectedBackupSha);
                OnDeckSelected(DeckLibraryList,
                    new SelectionChangedEventArgs(
                        System.Windows.Controls.Primitives.Selector.SelectionChangedEvent,
                        Array.Empty<object>(), Array.Empty<object>()));
            }
        }
    }

    private async void OnDuplicateDeckClicked(object sender, RoutedEventArgs e)
    {
        if (deckBusy || closing || string.IsNullOrWhiteSpace(nativePath) ||
            string.IsNullOrWhiteSpace(expectedBackupSha) ||
            DeckLibraryList.SelectedItem is not NativeDeckEditing.DeckRow selected)
        {
            DeckLibraryStatus.Text = "Select the exact saved deck to duplicate.";
            return;
        }
        if (MessageBox.Show(Window.GetWindow(this),
            $"Duplicate saved deck \"{selected.Name}\"?\n\n" +
            "The copy will have its own ID and keep the same card list and metadata, " +
            "but start without revision history or an Arena sync link. " +
            "Physical/Arena ownership will not change. The complete previous " +
            "native database is retained as a recovery file.",
            "Confirm saved-deck duplication",
            MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        deckBusy = true;
        AddDeckButton.IsEnabled = false;
        EditDeckButton.IsEnabled = false;
        DeleteDeckButton.IsEnabled = false;
        DuplicateDeckButton.IsEnabled = false;
        DeckLibraryStatus.Text = "Cloning saved deck with verified native rollback…";
        try
        {
            var created = await Task.Run(() =>
                NativeWorkspaceSafety.DuplicateDeck(
                    nativePath, selected, expectedBackupSha));
            if (closing) return;
            MessageBox.Show(Window.GetWindow(this),
                "Saved deck copied to an independent new deck entry.\n\n" +
                "The complete prior native collection was saved at:\n" +
                created.RecoveryPath,
                "Deck copy verified", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
            if (reopenNative is not null) await reopenNative();
        }
        catch (Exception ex)
        {
            if (!closing)
                DeckLibraryStatus.Text = "Unable to duplicate selected deck: " +
                    ex.Message + ". No automatic data reset was performed.";
        }
        finally
        {
            deckBusy = false;
            if (!closing)
            {
                AddDeckButton.IsEnabled = !string.IsNullOrWhiteSpace(expectedBackupSha);
                var selectedDeck = DeckLibraryList.SelectedItem as NativeDeckEditing.DeckRow;
                EditDeckButton.IsEnabled = selectedDeck?.Editable == true;
                DeleteDeckButton.IsEnabled = selectedDeck is not null;
                DuplicateDeckButton.IsEnabled = selectedDeck is not null;
            }
        }
    }

    private async void OnDeleteDeckClicked(object sender, RoutedEventArgs e)
    {
        if (deckBusy || closing || string.IsNullOrWhiteSpace(nativePath) ||
            string.IsNullOrWhiteSpace(expectedBackupSha) ||
            DeckLibraryList.SelectedItem is not NativeDeckEditing.DeckRow selected)
        {
            DeckLibraryStatus.Text = "Select a saved native deck first.";
            return;
        }
        if (MessageBox.Show(Window.GetWindow(this),
            $"Delete saved deck \"{selected.Name}\" and its {selected.Revisions:N0} " +
            "revision(s) from the ACTIVE native library?\n\n" +
            "This does not remove physical or Arena cards. The complete previous " +
            "native workspace, including the deleted deck and all its revisions, " +
            "will be saved as a recovery point.",
            "Confirm selected deck deletion",
            MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        deckBusy = true;
        AddDeckButton.IsEnabled = false;
        EditDeckButton.IsEnabled = false;
        DeleteDeckButton.IsEnabled = false;
        DuplicateDeckButton.IsEnabled = false;
        DeckLibraryStatus.Text = "Deleting selected native deck with verified rollback…";
        try
        {
            var updated = await Task.Run(() => NativeWorkspaceSafety.DeleteDeck(
                nativePath, selected, expectedBackupSha));
            if (closing) return;
            MessageBox.Show(Window.GetWindow(this), "Deck deleted from the active native library.\n\n" +
                "The complete prior collection was saved at:\n" + updated.RecoveryPath,
                "Deck deletion verified", MessageBoxButton.OK,
                MessageBoxImage.Information);
            Close();
            if (reopenNative is not null) await reopenNative();
        }
        catch (Exception ex)
        {
            if (!closing)
                DeckLibraryStatus.Text = "Deck deletion failed or could not be verified. " +
                    "Existing collection data was not intentionally reset. " + ex.Message;
        }
        finally
        {
            deckBusy = false;
            if (!closing)
            {
                AddDeckButton.IsEnabled = !string.IsNullOrWhiteSpace(expectedBackupSha);
                var stillSelectedDeck = DeckLibraryList.SelectedItem as NativeDeckEditing.DeckRow;
                EditDeckButton.IsEnabled = stillSelectedDeck?.Editable == true;
                DeleteDeckButton.IsEnabled = stillSelectedDeck is not null;
                DuplicateDeckButton.IsEnabled = stillSelectedDeck is not null;
            }
        }
    }

    private async void OnImportArenaClicked(object sender, RoutedEventArgs e)
    {
        if (arenaImportBusy || closing || string.IsNullOrWhiteSpace(nativePath) ||
            string.IsNullOrWhiteSpace(expectedBackupSha)) return;
        var picker = new OpenFileDialog
        {
            Title = "Import Arena card names and quantities from CSV or Excel",
            Filter = "Collection files (*.csv;*.xlsx)|*.csv;*.xlsx|CSV files (*.csv)|*.csv|Excel workbooks (*.xlsx)|*.xlsx",
            CheckFileExists = true,
            Multiselect = false,
            InitialDirectory = Environment.GetFolderPath(
                Environment.SpecialFolder.MyDocuments)
        };
        if (picker.ShowDialog(Window.GetWindow(this)) != true) return;
        arenaImportBusy = true;
        ImportArenaButton.IsEnabled = false;
        StatusText.Text = "Comparing imported Arena card names and quantities with local collection…";
        try
        {
            var (rows, diff) = await Task.Run(() =>
            {
                var imported = Path.GetExtension(picker.FileName)
                    .Equals(".csv", StringComparison.OrdinalIgnoreCase)
                    ? NativeCsvCollectionImport.Read(picker.FileName)
                    : NativeExcelCollectionImport.Read(picker.FileName);
                var comparison = NativeWorkspaceSafety.PreviewArenaReconciliation(
                    nativePath, imported, expectedBackupSha);
                return (imported, comparison);
            });
            if (closing) return;
            var review = new NativeImportDiffWindow(
                Path.GetFileName(picker.FileName), diff)
            { Owner = Window.GetWindow(this) };
            if (review.ShowDialog() != true || !diff.HasChanges)
            {
                StatusText.Text = diff.HasChanges
                    ? "Arena reconciliation canceled; collection unchanged."
                    : "Arena collection already matches all imported quantities. No writes or backups created.";
                return;
            }
            StatusText.Text = "Applying reviewed Arena quantity differences with rollback…";
            var outcome = await Task.Run(() =>
                NativeWorkspaceSafety.ReconcileArenaImport(
                    nativePath, rows, expectedBackupSha, diff.CandidateSha256));
            if (closing) return;
            MessageBox.Show(Window.GetWindow(this),
                "Arena reconciliation completed.\n" + diff.Summary +
                "\n\nComplete prior collection recovery: " + outcome.RecoveryPath,
                "Arena collection reconciled", MessageBoxButton.OK,
                MessageBoxImage.Information);
            Close();
            if (reopenNative is not null) await reopenNative();
        }
        catch (Exception ex)
        {
            if (!closing)
            {
                StatusText.Text = "Arena import stopped: " + ex.Message;
                MessageBox.Show(Window.GetWindow(this),
                    "Arena import could not be applied. The existing collection " +
                    "was not intentionally changed.\n\n" + ex.Message,
                    "Arena import stopped", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        finally
        {
            arenaImportBusy = false;
            if (!closing) ImportArenaButton.IsEnabled =
                !string.IsNullOrWhiteSpace(nativePath) &&
                !string.IsNullOrWhiteSpace(expectedBackupSha);
        }
    }

    private async void OnAddArenaClicked(object sender, RoutedEventArgs e)
    {
        if (closing || string.IsNullOrWhiteSpace(nativePath) ||
            string.IsNullOrWhiteSpace(expectedBackupSha))
        {
            StatusText.Text = "Arena additions require an active native collection.";
            return;
        }

        var editor = new NativeArenaEditorWindow(nativePath, expectedBackupSha)
        { Owner = Window.GetWindow(this) };
        if (editor.ShowDialog() != true) return;

        Close();
        if (reopenNative is not null)
            await reopenNative();
    }

    private async void OnEditArenaClicked(object sender, RoutedEventArgs e)
    {
        if (closing || string.IsNullOrWhiteSpace(nativePath) ||
            string.IsNullOrWhiteSpace(expectedBackupSha))
        {
            StatusText.Text = "Arena edits require an active native collection.";
            return;
        }
        if (CollectionTabs.SelectedIndex != 1 ||
            ArenaList.SelectedItem is not NativeArenaRow selected)
        {
            StatusText.Text = "Select the exact Arena row to edit or remove.";
            return;
        }

        // Preserve identity through filtered views and duplicate name-only
        // legacy entries. Do not infer the row ordinal from display ordering.
        int ordinal = -1;
        for (int i = 0; i < backup.ArenaCards.Count; i++)
            if (ReferenceEquals(backup.ArenaCards[i], selected))
            {
                ordinal = i;
                break;
            }

        if (ordinal < 0)
        {
            StatusText.Text = "The selected Arena entry is stale. Reopen the native collection.";
            return;
        }
        var editor = new NativeArenaEditorWindow(nativePath,
            expectedBackupSha, ordinal, selected) { Owner = Window.GetWindow(this) };
        if (editor.ShowDialog() != true) return;

        Close();
        if (reopenNative is not null)
            await reopenNative();
    }

    private async void OnAddPhysicalClicked(object sender, RoutedEventArgs e)
    {
        if (closing || string.IsNullOrWhiteSpace(nativePath) ||
            string.IsNullOrWhiteSpace(expectedBackupSha) ||
            string.IsNullOrWhiteSpace(catalogPath))
        {
            StatusText.Text = "Physical additions require a native collection and verified local catalog.";
            return;
        }
        var dialog = new NativeAddPrintingWindow(catalogPath, nativePath,
            expectedBackupSha) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true) return;

        // Reopen from newly verified SQLite rather than retaining stale rows,
        // catalog-set counts or the old optimistic-concurrency checksum.
        Close();
        if (reopenNative is not null)
            await reopenNative();
    }

    private async void OnAdjustPhysicalClicked(object sender, RoutedEventArgs e)
    {
        if (closing || string.IsNullOrWhiteSpace(nativePath) ||
            string.IsNullOrWhiteSpace(expectedBackupSha))
        {
            StatusText.Text = "Quantity edits are only available for a native collection.";
            return;
        }
        if (CollectionTabs.SelectedIndex != 0 ||
            PhysicalList.SelectedItem is not NativePhysicalValuation.Line displayed)
        {
            StatusText.Text = "Switch to Physical Collection and select the exact row to adjust.";
            return;
        }

        // The displayed row is an element of the original, ordered review.
        // Reference equality disambiguates even duplicate name/finish lines.
        var selected = displayed.Source;
        int ordinal = displayed.Ordinal;
        if (ordinal < 0 || ordinal >= backup.PhysicalCards.Count ||
            !ReferenceEquals(review.Rows[ordinal], selected) ||
            backup.PhysicalCards[ordinal] != selected.Original)
        {
            StatusText.Text = "Selected row is stale. Close and reopen this collection.";
            return;
        }

        var dialog = new NativeAdjustPhysicalWindow(nativePath,
            expectedBackupSha, ordinal, selected.Original) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true) return;

        // Refresh the entire collection and verified set counts after an edit.
        Close();
        if (reopenNative is not null)
            await reopenNative();
    }

    private void OnCardRowDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // This applies only to actual physical, Arena, or set-slot card rows.
        // Empty list space and column headers must never open stale selection.
        if (sender is not ListView list ||
            NativeCardRowActivation.SelectClickedRow(list, e.OriginalSource) is null)
            return;
        e.Handled = true;
        OnDetailsClicked(sender, e);
    }

    private async void OnDetailsClicked(object sender, RoutedEventArgs e)
    {
        string name;
        string ownership;
        if (CollectionTabs.SelectedIndex == 0 &&
            PhysicalList.SelectedItem is NativePhysicalValuation.Line priced)
        {
            var physical = priced.Source;
            name = physical.Name;
            ownership = $"Physical: {physical.Quantity} copies · {physical.SetAndNumber} · " +
                        $"{physical.Finish} · {physical.Status}" +
                        (physical.Confirmed ? $" · verified set {physical.ConfirmedSet}" : "") +
                        $" · Unit {priced.UnitLabel}, line {priced.LineLabel} ({priced.PriceStatus})";
        }
        else if (CollectionTabs.SelectedIndex == 1 &&
                 ArenaList.SelectedItem is NativeArenaRow arena)
        {
            name = arena.Name;
            ownership = $"Arena: {arena.Quantity} copies · name-only ownership, not set-verified";
        }
        else if (CollectionTabs.SelectedIndex == 3 &&
                 SetSlotsList.SelectedItem is NativeSetProgress.SetSlot slot)
        {
            name = slot.CardName;
            ownership = slot.NameCoverage
                ? $"Arena set card-name coverage · {slot.Ownership}. " +
                  "No specific Arena printing or card style has been verified."
                : $"Catalog collector slot #{slot.CollectorNumber} · " +
                  $"{slot.Ownership} (verified physical copies only)";
        }
        else
        {
            StatusText.Text = "Select a card in Physical Collection, Arena Collection, or a set's checklist.";
            return;
        }

        CollectionCatalogReview.CardDetails? info = null;
        if (!string.IsNullOrWhiteSpace(catalogPath))
        {
            try { info = CollectionCatalogReview.ReadDetails(catalogPath, name); }
            catch (Exception) { /* Missing or stale catalog must not affect the backup view. */ }
        }
        string? set = null, collector = null;
        if (CollectionTabs.SelectedIndex == 0 &&
            PhysicalList.SelectedItem is NativePhysicalValuation.Line pricedOwned)
        {
            var owned = pricedOwned.Source;
            set = owned.Confirmed ? owned.ConfirmedSet : owned.Original.SetCode;
            collector = owned.Confirmed ? owned.CatalogCollector : owned.Original.CollectorNumber;
        }
        else if (CollectionTabs.SelectedIndex == 3 &&
                 SetSlotsList.SelectedItem is NativeSetProgress.SetSlot setSlot &&
                 !setSlot.NameCoverage)
        {
            set = selectedSetRow?.SetCode;
            collector = setSlot.CollectorNumber;
        }
        bool arenaScope = CollectionTabs.SelectedIndex == 1 ||
            (CollectionTabs.SelectedIndex == 3 &&
             SetSlotsList.SelectedItem is NativeSetProgress.SetSlot { NameCoverage: true });
        string? arenaArt = null;
        if (arenaScope && !string.IsNullOrWhiteSpace(catalogPath))
        {
            try
            {
                // Arena records hold card names, not exact printing IDs.
                // Prefer an Arena-digital style; never substitute paper-only art.
                var styles = await Task.Run(() =>
                    NativePrintingArtwork.Read(catalogPath, name, 400, arenaOnly: true));
                if (closing) return;
                var chosen = styles.Printings
                    .Where(p => NativeCardArtwork.IsTrustedArtworkUrl(p.ImageUrl))
                    .OrderBy(p => p.Paper)
                    .ThenByDescending(p => p.Released, StringComparer.Ordinal)
                    .FirstOrDefault();
                if (chosen is not null)
                {
                    arenaArt = chosen.ImageUrl;
                    set = chosen.SetCode;
                    collector = chosen.CollectorNumber;
                }
            }
            catch (Exception)
            {
                // A stale catalog never changes Arena ownership; use an empty
                // artwork placeholder instead of a misleading paper printing.
            }
        }
        if (closing) return;
        var details = new NativeCardDetailsWindow(name, ownership, info,
            catalogPath, nativePath, set, collector,
            arenaScope: arenaScope, selectedArtworkUrl: arenaArt)
        { Owner = Window.GetWindow(this) };
        details.Closed += async (_, _) =>
        {
            if ((!details.PreferenceChanged && !details.ShortlistChanged) || closing) return;
            // Artwork or shortlist writes advance the optimistic SHA. Reopen
            // this collection from verified SQLite before permitting edits.
            Close();
            if (reopenNative is not null) await reopenNative();
        };
        details.Show();
    }
}
