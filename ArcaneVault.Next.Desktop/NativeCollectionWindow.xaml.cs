using ArcaneVault.Next.Collection;
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

    public NativeCollectionWindow(NativeBackupView backup,
        CollectionCatalogReview.Result review, string? catalogPath)
    {
        this.backup = backup ?? throw new ArgumentNullException(nameof(backup));
        this.review = review ?? throw new ArgumentNullException(nameof(review));
        this.catalogPath = catalogPath;
        InitializeComponent();
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
        SetSummaryList.ItemsSource = review.ConfirmedSets;
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
            "Sets show verified copies only, never guessed completion percentages.";
    }

    private void OnFilterChanged(object sender, RoutedEventArgs e) => RefreshLists();

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
        else
        {
            StatusText.Text = "Select a card in Physical Collection or Arena Collection to view details.";
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
