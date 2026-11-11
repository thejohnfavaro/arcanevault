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
    private readonly IReadOnlyList<NativeDeckEditing.DeckRow> deckRows;
    private bool deckBusy;

    public NativeCollectionWindow(NativeBackupView backup,
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
        DeckLibraryList.ItemsSource = deckRows;
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
        AddArenaButton.IsEnabled = nativeWorkspace &&
            !string.IsNullOrWhiteSpace(nativePath) &&
            !string.IsNullOrWhiteSpace(expectedBackupSha);
        EditArenaButton.IsEnabled = AddArenaButton.IsEnabled;
        if (nativeWorkspace)
        {
            Title = "Arcane Vault Next — Native Collection";
            DataSourceLabel.Text =
                "Native SQLite — physical, Arena and saved-deck edits each preserve a complete rollback";
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

    private void OnDeckSelected(object sender, SelectionChangedEventArgs e)
    {
        if (DeckLibraryList is null || EditDeckButton is null ||
            DeleteDeckButton is null || DuplicateDeckButton is null) return;
        bool editable = DeckLibraryList.SelectedItem is NativeDeckEditing.DeckRow row &&
            row.Editable && !deckBusy &&
            !string.IsNullOrWhiteSpace(expectedBackupSha);
        EditDeckButton.IsEnabled = editable;
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
        var dialog = new NativeDeckEditorWindow(nativePath, expectedBackupSha)
        { Owner = this };
        if (dialog.ShowDialog() != true) return;
        Close();
        if (reopenNative is not null) await reopenNative();
    }

    private async void OnEditDeckClicked(object sender, RoutedEventArgs e)
    {
        if (closing || string.IsNullOrWhiteSpace(nativePath) ||
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
            nativePath, expectedBackupSha, selected) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        Close();
        if (reopenNative is not null) await reopenNative();
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
        if (MessageBox.Show(this,
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
            MessageBox.Show(this,
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
        if (MessageBox.Show(this,
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
            MessageBox.Show(this, "Deck deleted from the active native library.\n\n" +
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

    private async void OnAddArenaClicked(object sender, RoutedEventArgs e)
    {
        if (closing || string.IsNullOrWhiteSpace(nativePath) ||
            string.IsNullOrWhiteSpace(expectedBackupSha))
        {
            StatusText.Text = "Arena additions require an active native collection.";
            return;
        }

        var editor = new NativeArenaEditorWindow(nativePath, expectedBackupSha)
        { Owner = this };
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
            expectedBackupSha, ordinal, selected) { Owner = this };
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
            expectedBackupSha) { Owner = this };
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
            PhysicalList.SelectedItem is not CollectionCatalogReview.PrintingRow selected)
        {
            StatusText.Text = "Switch to Physical Collection and select the exact row to adjust.";
            return;
        }

        // The displayed row is an element of the original, ordered review.
        // Reference equality disambiguates even duplicate name/finish lines.
        int ordinal = -1;
        for (int i = 0; i < review.Rows.Count; i++)
            if (ReferenceEquals(review.Rows[i], selected))
            {
                ordinal = i;
                break;
            }
        if (ordinal < 0 || ordinal >= backup.PhysicalCards.Count ||
            backup.PhysicalCards[ordinal] != selected.Original)
        {
            StatusText.Text = "Selected row is stale. Close and reopen this collection.";
            return;
        }

        var dialog = new NativeAdjustPhysicalWindow(nativePath,
            expectedBackupSha, ordinal, selected.Original) { Owner = this };
        if (dialog.ShowDialog() != true) return;

        // Refresh the entire collection and verified set counts after an edit.
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
        else if (CollectionTabs.SelectedIndex == 3 &&
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
