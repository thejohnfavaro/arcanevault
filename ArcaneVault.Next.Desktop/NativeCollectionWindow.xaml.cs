using ArcaneVault.Next.Collection;
using System.IO;
using ArcaneVault.Next.Domain;
using System.Windows;
using System.Windows.Controls;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// Read-only backup viewer with optional verification against the same isolated
/// SQLite catalog snapshot used by the native Card Browser.
/// </summary>
public partial class NativeCollectionWindow : Window
{
    private readonly NativeBackupView backup;
    private readonly CollectionCatalogReview.Result review;
    private readonly string? catalogPath;
    private readonly NativeSetProgress.Result? progress;
    private int setQueryVersion;
    private bool closing;
    private readonly string? nativePath;
    private readonly string? expectedBackupSha;
    private readonly Func<Task>? reopenNative;

    public NativeCollectionWindow(NativeBackupView backup,
        CollectionCatalogReview.Result review, string? catalogPath,
        NativeSetProgress.Result? progress, bool nativeWorkspace = false,
        string? nativePath = null, string? expectedBackupSha = null,
        Func<Task>? reopenNative = null)
    {
        this.backup = backup ?? throw new ArgumentNullException(nameof(backup));
        this.review = review ?? throw new ArgumentNullException(nameof(review));
        this.catalogPath = catalogPath;
        this.progress = progress;
        this.nativePath = nativePath;
        this.expectedBackupSha = expectedBackupSha;
        this.reopenNative = reopenNative;
        InitializeComponent();
        AddPhysicalButton.IsEnabled = nativeWorkspace &&
            !string.IsNullOrWhiteSpace(nativePath) &&
            !string.IsNullOrWhiteSpace(expectedBackupSha) &&
            !string.IsNullOrWhiteSpace(catalogPath);
        if (nativeWorkspace)
        {
            Title = "Arcane Vault Next — Native Collection";
            DataSourceLabel.Text =
                "Native SQLite collection — verified paper-printing additions require explicit confirmation and retain recovery; other edits are locked";
        }
        Closed += (_, _) => { closing = true; setQueryVersion++; };
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

    private void RefreshLists()
    {
        if (SearchBox is null || MissingOnly is null || PhysicalList is null ||
            ArenaList is null || StatusText is null) return;

        var search = SearchBox.Text.Trim();
        var unresolved = MissingOnly.IsChecked == true;
        var physical = review.Rows.Where(x =>
            (!unresolved || !x.Confirmed) &&
            (search.Length == 0 ||
             x.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
             x.Original.SetCode.Contains(search, StringComparison.OrdinalIgnoreCase) ||
             x.Original.CollectorNumber.Contains(search, StringComparison.OrdinalIgnoreCase) ||
             x.Status.Contains(search, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        var arena = backup.ArenaCards.Where(x =>
            search.Length == 0 || x.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        PhysicalList.ItemsSource = physical;
        ArenaList.ItemsSource = arena;
        StatusText.Text = $"{physical.Length:N0} physical lines and {arena.Length:N0} Arena names shown. " +
            "Double-click a card for offline details. Arena ownership has no printing IDs. " +
            "Set Progress counts only verified paper collector-number slots; missing/invalid print IDs never count.";
    }

    private void OnFilterChanged(object sender, RoutedEventArgs e) => RefreshLists();

    private void RefreshSetList()
    {
        if (SetSummaryList is null || ShowEmptySets is null ||
            SetSearch is null || SetDetailsHeader is null || SetSlotsList is null) return;

        string query = SetSearch.Text.Trim();
        bool includeEmpty = ShowEmptySets.IsChecked == true;
        var items = (progress?.Sets ?? Array.Empty<NativeSetProgress.SetRow>())
            .Where(x => (includeEmpty || x.OwnedSlots > 0) &&
                (query.Length == 0 ||
                 x.SetCode.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                 x.SetName.Contains(query, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        setQueryVersion++;
        SetSummaryList.ItemsSource = items;
        SetSlotsList.ItemsSource = null;

        if (progress is null)
            SetDetailsHeader.Text = "Load the local Scryfall catalog snapshot before opening the backup to calculate Set Progress.";
        else if (items.Length == 0)
            SetDetailsHeader.Text = "No matching sets with verified owned collector slots. Choose 'Include sets with 0 verified slots' to inspect the catalog.";
        else
            SetDetailsHeader.Text = $"Showing {items.Length:N0} sets. Select a set to inspect its catalog collector-number checklist.";
    }

    private void OnSetFilterChanged(object sender, RoutedEventArgs e) => RefreshSetList();

    private async void OnSetSelected(object sender, SelectionChangedEventArgs e)
    {
        if (SetSummaryList is null || SetSlotsList is null ||
            SetDetailsHeader is null || progress is null ||
            string.IsNullOrWhiteSpace(catalogPath)) return;

        int version = ++setQueryVersion;
        SetSlotsList.ItemsSource = null;
        if (SetSummaryList.SelectedItem is not NativeSetProgress.SetRow selected) return;

        SetDetailsHeader.Text =
            $"{selected.SetName} ({selected.SetCode}) · loading numbered paper catalog slots…";
        try
        {
            var slots = await Task.Run(() =>
                NativeSetProgress.LoadSetSlots(review, catalogPath, selected.SetCode));
            if (closing || version != setQueryVersion) return;
            if (slots.Count != selected.TotalSlots)
                throw new InvalidDataException("Set denominator differs from its collector-number checklist.");
            SetSlotsList.ItemsSource = slots;
            SetDetailsHeader.Text =
                $"{selected.SetName} · {selected.SlotsLabel} verified slots " +
                $"({selected.PercentLabel}), {selected.VerifiedCopies:N0} physical copies · " +
                "double-click a card for offline details.";
        }
        catch (Exception)
        {
            if (!closing && version == setQueryVersion)
                SetDetailsHeader.Text =
                    "Unable to display this catalog set. Backup data remains unchanged.";
        }
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
            expectedBackupSha) { Owner = this };
        if (dialog.ShowDialog() != true) return;

        // Reopen from newly verified SQLite rather than retaining stale rows,
        // catalog-set counts or the old optimistic-concurrency checksum.
        Close();
        if (reopenNative is not null)
            await reopenNative();
    }

    private void OnDetailsClicked(object sender, RoutedEventArgs e)
    {
        string name;
        string ownership;
        if (CollectionTabs.SelectedIndex == 0 &&
            PhysicalList.SelectedItem is CollectionCatalogReview.PrintingRow physical)
        {
            name = physical.Name;
            ownership = $"Physical: {physical.Quantity} copies · {physical.SetAndNumber} · " +
                        $"{physical.Finish} · {physical.Status}" +
                        (physical.Confirmed ? $" · verified set {physical.ConfirmedSet}" : "");
        }
        else if (CollectionTabs.SelectedIndex == 1 &&
                 ArenaList.SelectedItem is NativeArenaRow arena)
        {
            name = arena.Name;
            ownership = $"Arena: {arena.Quantity} copies · name-only ownership, not set-verified";
        }
        else if (CollectionTabs.SelectedIndex == 2 &&
                 SetSlotsList.SelectedItem is NativeSetProgress.SetSlot slot)
        {
            name = slot.CardName;
            ownership = $"Catalog collector slot #{slot.CollectorNumber} · " +
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
        var details = new NativeCardDetailsWindow(name, ownership, info) { Owner = this };
        details.Show();
    }
}
