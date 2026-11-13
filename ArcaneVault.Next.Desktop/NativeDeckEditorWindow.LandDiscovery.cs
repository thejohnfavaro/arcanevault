using ArcaneVault.Next.Collection;
using ArcaneVault.Next.EDHLab;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// Local, opt-in land discovery. Uses the existing verified, cached EDH
/// candidate catalog + independent real/Proxy counts. No network or catalog
/// rescan on search/filter/sort, no automatic draft edits.
/// </summary>
public partial class NativeDeckEditorWindow
{
    private void OnLandDiscoveryFilterChanged(object sender, RoutedEventArgs e)
    {
        if (EdhLandDiscoveryList is null || EdhLandSearchBox is null ||
            EdhLandAvailabilityBox is null) return;
        RefreshLandDiscovery();
    }

    private void RefreshLandDiscovery()
    {
        if (EdhLandDiscoveryList is null || EdhLandDiscoveryStatus is null ||
            EdhLandAddButton is null || MainboardBox is null ||
            EdhLandCommanderColors is null || edhLabCommander is null)
            return;

        EdhLandCommanderColors.Value = edhLabColors ?? "";
        if (edhCatalog is null)
        {
            EdhLandDiscoveryList.ItemsSource = null;
            EdhLandAddButton.IsEnabled = false;
            EdhLandDiscoveryStatus.Text = "The local commander-legal catalog is loading. " +
                "No land suggestion is treated as verified until it finishes.";
            return;
        }

        string? selectedName =
            (EdhLandDiscoveryList.SelectedItem as EdhLabLandAdvisor.Land)?.Name;
        try
        {
            var counts = NativeDeckEditing.ParseMainboard(MainboardBox.Text)
                .ToDictionary(x => x.Name, x => x.Quantity,
                    StringComparer.OrdinalIgnoreCase);
            bool arena = OwnedPoolBox?.SelectedIndex == 1;
            var filter = (EdhLandAvailabilityBox?.SelectedIndex ?? 0) switch
            {
                1 => EdhLabLandAdvisor.Availability.RealOwned,
                2 => EdhLabLandAdvisor.Availability.Proxy,
                3 => EdhLabLandAdvisor.Availability.All,
                _ => EdhLabLandAdvisor.Availability.OwnedOrProxy
            };
            // Physical proxies never represent Arena-owned card styles.
            var proxies = arena ? null : proxyCounts;
            var result = EdhLabLandAdvisor.Recommend(
                edhCatalog.AllEligibleCards, edhLabColors ?? "", counts,
                proxies, filter, EdhLandIncludeBasics?.IsChecked == true,
                EdhLandSearchBox?.Text, limit: 120);
            EdhLandDiscoveryList.ItemsSource = result.Rows;
            if (selectedName is not null)
                EdhLandDiscoveryList.SelectedItem = result.Rows.FirstOrDefault(x =>
                    x.Name.Equals(selectedName, StringComparison.OrdinalIgnoreCase));
            EdhLandDiscoveryStatus.Text = result.Summary +
                (arena ? " Arena collection only; paper proxies aren't available." :
                    " Owned and Proxy quantities are shown separately.");
            EdhLandAddButton.IsEnabled =
                EdhLandDiscoveryList.SelectedItem is EdhLabLandAdvisor.Land
                    { CanQuickAdd: true } && !busy;
            if (EdhLandDiscoveryList.SelectedItem is null)
                EdhLandSelectedNotes.Text =
                    "Single-click to preview; double-click or use Add for one eligible " +
                    "copy. Fetchlands depend on their targets. Nothing is auto-added.";
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException or
            OverflowException)
        {
            EdhLandDiscoveryList.ItemsSource = null;
            EdhLandAddButton.IsEnabled = false;
            EdhLandDiscoveryStatus.Text =
                "Correct the unsaved mainboard or search to refresh legal land suggestions: " +
                ex.Message;
        }
    }

    private async void OnLandDiscoverySelected(object sender,
        SelectionChangedEventArgs e)
    {
        if (EdhLandDiscoveryList is null || EdhLandSelectedNotes is null ||
            EdhLandAddButton is null) return;
        var selected = EdhLandDiscoveryList.SelectedItem as EdhLabLandAdvisor.Land;
        EdhLandAddButton.IsEnabled = selected is { CanQuickAdd: true } && !busy;
        if (selected is null) return;
        EdhLandSelectedNotes.Text = selected.Explanation +
            (selected.ProxyCopies > 0
                ? " · Proxy copies are not owned printings or priced." : "");
        // No selection event may modify the mainboard.
        OwnedCardsList?.UnselectAll();
        await ShowCardPreviewAsync(new OwnedDisplay(
            selected.Name, selected.RealOwned, "Land",
            LabSuggestion: true,
            SynergyExplanation: selected.Explanation,
            InDraftCopies: selected.InDeckCopies,
            Proxied: selected.ProxyCopies));
    }

    private void OnLandDiscoveryPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        // Only the second left click on an actual land suggestion may add it;
        // never a scrollbar, empty list, or selection-change event.
        if (e.ChangedButton != MouseButton.Left || e.ClickCount != 2 ||
            EdhLandDiscoveryList is null ||
            e.OriginalSource is not DependencyObject source ||
            ItemsControl.ContainerFromElement(EdhLandDiscoveryList, source)
                is not ListBoxItem row ||
            row.DataContext is not EdhLabLandAdvisor.Land selected)
            return;
        if (!ReferenceEquals(selected, EdhLandDiscoveryList.SelectedItem))
            EdhLandDiscoveryList.SelectedItem = selected;
        TryAddLandSuggestion(selected, "double-click");
        e.Handled = true;
    }

    private void OnLandDiscoveryAddClicked(object sender, RoutedEventArgs e)
    {
        if (EdhLandDiscoveryList?.SelectedItem is EdhLabLandAdvisor.Land selected)
            TryAddLandSuggestion(selected, "Add button");
    }

    private void TryAddLandSuggestion(EdhLabLandAdvisor.Land selected,
        string source)
    {
        if (busy || edhLabCommander is null || edhCatalog is null ||
            !selected.CanQuickAdd ||
            EdhLandDiscoveryList?.Items.Contains(selected) != true ||
            edhCatalog.Find(selected.Name) is not { } eligible ||
            !eligible.Type.Contains("Land", StringComparison.OrdinalIgnoreCase))
        {
            StatusText.Text = "That land is already in the mainboard, was filtered out, " +
                "or is not locally eligible. Nothing was added.";
            return;
        }
        try
        {
            // Recheck the authoritative unsaved Text mainboard, not a cached
            // row. Basic lands need the regular Add (+1) button to add more
            // than one copy via the existing copy-limit rules.
            if (NativeDeckEditing.ParseMainboard(MainboardBox.Text).Any(x =>
                    x.Name.Equals(selected.Name, StringComparison.OrdinalIgnoreCase)))
            {
                StatusText.Text = selected.Name + " is already in this mainboard. " +
                    "Use Cards → Add (+1) for additional basic-land copies.";
                RefreshLandDiscovery();
                return;
            }
            AddEligibleCardToMainboard(selected.Name,
                singletonDoubleClick: true, source: "land " + source);
            RefreshLandDiscovery();
        }
        catch (Exception ex) when (ex is InvalidDataException or
            ArgumentException or OverflowException)
        {
            StatusText.Text = "Land add not applied: " + ex.Message;
        }
    }
}
