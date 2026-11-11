using ArcaneVault.Next.Collection;
using ArcaneVault.Next.Domain;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// Saved legacy-compatible card shortlist with independently verified
/// physical and Arena status. Never equates a bookmark with ownership.
/// </summary>
public partial class NativeShortlistWindow : Window
{
    private readonly string nativePath;
    private readonly string? catalogPath;
    private IReadOnlyList<NativeShortlist.Entry> all = Array.Empty<NativeShortlist.Entry>();
    private IReadOnlyList<string> savedNames = Array.Empty<string>();
    private string? expectedSha;
    private bool busy;
    private bool closing;
    private int version;

    public bool ShortlistChanged { get; private set; }

    public NativeShortlistWindow(string nativePath, string? catalogPath)
    {
        this.nativePath = nativePath;
        this.catalogPath = catalogPath;
        InitializeComponent();
        Loaded += async (_, _) => await ReloadAsync();
        Closed += (_, _) => { closing = true; version++; };
    }

    private async Task ReloadAsync()
    {
        if (busy || closing) return;
        busy = true;
        int generation = ++version;
        RemoveButton.IsEnabled = false;
        CopyAllButton.IsEnabled = false;
        ViewDetailsButton.IsEnabled = false;
        StatusText.Text = "Verifying native shortlist and ownership; no files are being changed…";
        try
        {
            var result = await Task.Run(() =>
            {
                var workspace = NativeWorkspace.Open(nativePath);
                var names = NativeWorkspaceSafety.ListShortlist(nativePath);
                var review = CollectionCatalogReview.Examine(
                    workspace.Collection, catalogPath);
                var entries = NativeShortlist.Summarize(
                    names, workspace.Collection, review);
                // An independent writer could have replaced the workspace while
                // we verified it. Don't show records from mixed revisions.
                var refreshed = NativeWorkspace.Open(nativePath);
                if (refreshed.BackupSha256 != workspace.BackupSha256)
                    throw new InvalidDataException(
                        "The collection changed while loading; refresh My Shortlist.");
                return (entries, names, sha: refreshed.BackupSha256,
                    verified: review.CatalogVerified);
            });
            if (closing || version != generation) return;
            all = result.entries;
            savedNames = result.names;
            expectedSha = result.sha;
            ApplyFilter();
            CopyAllButton.IsEnabled = savedNames.Count > 0;
            StatusText.Text = $"{savedNames.Count:N0} saved card names. " +
                (result.verified
                    ? "Physical counts require exact paper Scryfall verification. "
                    : "No official catalog loaded: physical copies are unverified. ") +
                "Bookmarks never increase Arena or physical inventory. " +
                "A full recovery point is retained for each bookmark edit.";
        }
        catch (Exception ex)
        {
            if (!closing)
            {
                expectedSha = null;
                StatusText.Text = "Could not safely read My Shortlist: " + ex.Message +
                    ". Nothing was changed.";
            }
        }
        finally
        {
            busy = false;
            if (!closing) RefreshSelection();
        }
    }

    private void ApplyFilter()
    {
        if (ShortlistList is null || NotConfirmedOnly is null || SearchBox is null)
            return;
        string search = SearchBox.Text.Trim();
        var previous = ShortlistList.SelectedItem as NativeShortlist.Entry;
        var filtered = all.Where(x =>
            (search.Length == 0 ||
             x.Name.Contains(search, StringComparison.OrdinalIgnoreCase)) &&
            (NotConfirmedOnly.IsChecked != true || !x.ConfirmedOwned)).ToArray();
        ShortlistList.ItemsSource = filtered;
        if (previous is not null && filtered.Contains(previous))
            ShortlistList.SelectedItem = previous;
        RefreshSelection();
    }

    private void RefreshSelection()
    {
        bool hasSelection = !busy && expectedSha is not null &&
            ShortlistList?.SelectedItem is NativeShortlist.Entry;
        RemoveButton.IsEnabled = hasSelection;
        ViewDetailsButton.IsEnabled = !busy &&
            ShortlistList?.SelectedItem is NativeShortlist.Entry;
    }

    private void OnFilterChanged(object sender, RoutedEventArgs e) => ApplyFilter();

    private void OnCardSelected(object sender, SelectionChangedEventArgs e) =>
        RefreshSelection();

    private void OnCardDoubleClicked(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListView list ||
            NativeCardRowActivation.SelectClickedRow(list, e.OriginalSource)
                is not NativeShortlist.Entry)
            return;
        e.Handled = true;
        OnDetailsClicked(sender, e);
    }

    private async void OnDetailsClicked(object sender, RoutedEventArgs e)
    {
        if (busy || ShortlistList.SelectedItem is not NativeShortlist.Entry card)
            return;
        CollectionCatalogReview.CardDetails? info = null;
        if (!string.IsNullOrWhiteSpace(catalogPath))
        {
            try
            {
                info = await Task.Run(() =>
                    CollectionCatalogReview.ReadDetails(catalogPath, card.Name));
            }
            catch (Exception) { /* Details can open with only the saved name. */ }
        }
        if (closing) return;
        var details = new NativeCardDetailsWindow(card.Name,
            $"Shortlist only (not ownership). Physical verified: {card.VerifiedPhysicalCopies:N0}; " +
            $"unresolved: {card.UnresolvedPhysicalCopies:N0}; Arena: {card.ArenaCopies:N0}.",
            info, catalogPath, nativePath) { Owner = this };
        details.Closed += async (_, _) =>
        {
            if (!closing && (details.ShortlistChanged || details.PreferenceChanged))
                await ReloadAsync();
        };
        details.Show();
    }

    private async void OnRemoveClicked(object sender, RoutedEventArgs e)
    {
        if (busy || expectedSha is null ||
            ShortlistList.SelectedItem is not NativeShortlist.Entry entry)
            return;
        if (MessageBox.Show(this,
            $"Remove \"{entry.Name}\" from My Shortlist?\n\n" +
            "This removes the bookmark only. Owned cards, saved decks and " +
            "artwork preferences are unchanged, and a full recovery file is kept.",
            "Remove saved card", MessageBoxButton.YesNo,
            MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        busy = true;
        RemoveButton.IsEnabled = false;
        try
        {
            var changed = await Task.Run(() =>
                NativeWorkspaceSafety.SetShortlistCard(nativePath,
                    entry.Name, false, expectedSha));
            if (closing) return;
            expectedSha = changed.Current.BackupSha256;
            ShortlistChanged = true;
        }
        catch (Exception ex)
        {
            if (!closing)
                MessageBox.Show(this,
                    "Bookmark was not changed:\n" + ex.Message +
                    "\n\nRefresh to load the newest native collection before editing.",
                    "Shortlist not changed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { busy = false; }
        if (!closing) await ReloadAsync();
    }

    private void OnCopyClicked(object sender, RoutedEventArgs e)
    {
        if (savedNames.Count == 0) return;
        try
        {
            Clipboard.SetText(NativeShortlist.ArenaText(savedNames));
            StatusText.Text = $"Copied {savedNames.Count:N0} saved card names, " +
                "one copy of each, as an Arena-style import list. No ownership was changed.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Could not copy the Arena-style list: " + ex.Message;
        }
    }

    private async void OnRefreshClicked(object sender, RoutedEventArgs e) =>
        await ReloadAsync();

    private void OnCloseClicked(object sender, RoutedEventArgs e) => Close();
}
