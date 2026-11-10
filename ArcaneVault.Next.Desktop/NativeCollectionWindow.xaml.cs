using ArcaneVault.Next.Domain;
using System.Windows;
using System.Windows.Controls;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// Disposable in-memory view of an explicitly selected schema-2 full backup.
/// No collection writes, name-to-printing guesses or catalog mutations.
/// </summary>
public partial class NativeCollectionWindow : Window
{
    private readonly NativeBackupView snapshot;

    public NativeCollectionWindow(NativeBackupView snapshot)
    {
        this.snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        InitializeComponent();
        TotalsText.Text =
            $"Physical: {snapshot.PhysicalCopies:N0} copies · {snapshot.PhysicalCards.Count:N0} lines · " +
            $"{snapshot.DistinctPhysicalNames:N0} names  |  " +
            $"Arena: {snapshot.ArenaCopies:N0} copies  |  " +
            $"Decks: {snapshot.Decks:N0} ({snapshot.DeckRevisions:N0} revisions)  |  " +
            $"Preferred artwork: {snapshot.PreferredArtworks:N0}  |  " +
            $"Copies with missing/invalid print IDs: {snapshot.CopiesWithMissingOrInvalidIds:N0}";
        RefreshLists();
    }

    private void RefreshLists()
    {
        if (SearchBox is null || MissingOnly is null || PhysicalList is null || ArenaList is null || StatusText is null) return;
        var search = SearchBox.Text.Trim();
        var invalidOnly = MissingOnly.IsChecked == true;
        var physical = snapshot.PhysicalCards.Where(x =>
            (!invalidOnly || !x.HasValidPrintId) &&
            (search.Length == 0 ||
             x.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
             x.SetCode.Contains(search, StringComparison.OrdinalIgnoreCase) ||
             x.CollectorNumber.Contains(search, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        var arena = snapshot.ArenaCards.Where(x =>
            search.Length == 0 || x.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        PhysicalList.ItemsSource = physical;
        ArenaList.ItemsSource = arena;
        StatusText.Text =
            $"{physical.Length:N0} physical lines · {arena.Length:N0} Arena names shown. " +
            "Scryfall IDs in the export are unverified. No backup, installed data, or Set Progress changed.";
    }

    private void OnFilterChanged(object sender, RoutedEventArgs e) => RefreshLists();
}
