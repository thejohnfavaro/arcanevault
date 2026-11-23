using ArcaneVault.Next.Collection;
using ArcaneVault.Next.Domain;
using System.IO;
using ArcaneVault.Next.EDHLab;
using System.Windows;
using System.Windows.Controls;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// The EDH mainboard's visual manager is a projection of the normal editable
/// MainboardBox text; it has no independent mutable deck state or write path.
/// </summary>
public partial class NativeDeckEditorWindow
{
    private bool updatingMainboardManager;
    // Visual-only section state, mirrored from Android's grouped mainboard.
    // Never changes the editable MainboardBox text or collection ownership.
    private readonly HashSet<string> collapsedMainboardGroups =
        new(StringComparer.OrdinalIgnoreCase);

    private void OnClearMainboardSearchClicked(object sender, RoutedEventArgs e)
    {
        if (MainboardManagerSearchBox is null) return;
        MainboardManagerSearchBox.Clear();
        MainboardManagerSearchBox.Focus();
    }

    private void OnPreviousMainboardCardClicked(object sender, RoutedEventArgs e) =>
        MoveMainboardCard(-1);

    private void OnNextMainboardCardClicked(object sender, RoutedEventArgs e) =>
        MoveMainboardCard(1);

    private void MoveMainboardCard(int direction)
    {
        if (busy || MainboardManagerList is null) return;
        var displayed = MainboardManagerList.Items
            .OfType<EdhLabMainboardView.Row>().ToArray();
        int position = EdhLabMainboardView.NextCardIndex(displayed,
            MainboardManagerList.SelectedIndex, direction);
        if (position < 0) return;
        MainboardManagerList.SelectedIndex = position;
        MainboardManagerList.ScrollIntoView(displayed[position]);
        MainboardManagerList.Focus();
    }

    private void OnMainboardManagerSearchKeyDown(object sender,
        System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape &&
            MainboardManagerSearchBox is { Text.Length: > 0 })
        {
            MainboardManagerSearchBox.Clear();
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.F3)
        {
            MoveMainboardCard(
                System.Windows.Input.Keyboard.Modifiers.HasFlag(
                    System.Windows.Input.ModifierKeys.Shift) ? -1 : 1);
            e.Handled = true;
        }
    }

    private void OnMainboardManagerListKeyDown(object sender,
        System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.F &&
            System.Windows.Input.Keyboard.Modifiers ==
                System.Windows.Input.ModifierKeys.Control)
        {
            MainboardManagerSearchBox?.Focus();
            MainboardManagerSearchBox?.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.F3)
        {
            MoveMainboardCard(
                System.Windows.Input.Keyboard.Modifiers.HasFlag(
                    System.Windows.Input.ModifierKeys.Shift) ? -1 : 1);
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.Enter &&
                 MainboardManagerList.SelectedItem is EdhLabMainboardView.Row
                     { IsHeader: false })
        {
            OnMainboardManagerFullDetailsClicked(sender, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    private void OnCollapseMainboardGroupsClicked(object sender, RoutedEventArgs e)
    {
        foreach (var section in new[] { "Commander", "Creatures", "Instants",
            "Sorceries", "Artifacts", "Enchantments", "Planeswalkers",
            "Other", "Unverified", "Lands" })
            collapsedMainboardGroups.Add(section);
        RefreshMainboardManager();
    }

    private void OnExpandMainboardGroupsClicked(object sender, RoutedEventArgs e)
    {
        collapsedMainboardGroups.Clear();
        RefreshMainboardManager();
    }



    private EdhLabMainboardView.Section MainboardManagerSection =>
        (MainboardFilterBox?.SelectedItem as ComboBoxItem)?.Tag?.ToString() switch
        {
            "Lands" => EdhLabMainboardView.Section.Lands,
            "Creatures" => EdhLabMainboardView.Section.Creatures,
            "Artifacts" => EdhLabMainboardView.Section.Artifacts,
            "Enchantments" => EdhLabMainboardView.Section.Enchantments,
            "Spells" => EdhLabMainboardView.Section.Spells,
            "Other" => EdhLabMainboardView.Section.Other,
            _ => EdhLabMainboardView.Section.All
        };

    private EdhLabMainboardView.Order MainboardManagerOrder =>
        (MainboardSortBox?.SelectedItem as ComboBoxItem)?.Tag?.ToString() switch
        {
            "Name" => EdhLabMainboardView.Order.Name,
            "Quantity" => EdhLabMainboardView.Order.Quantity,
            _ => EdhLabMainboardView.Order.Type
        };

    private void OnMainboardManagerFilterChanged(object sender,
        SelectionChangedEventArgs e)
    {
        if (edhLabCommander is null || MainboardManagerList is null) return;
        RefreshMainboardManager();
    }

    private void OnMainboardManagerSearchChanged(object sender,
        TextChangedEventArgs e)
    {
        if (edhLabCommander is null || MainboardManagerList is null) return;
        // Read-only projection: typing never edits the current mainboard.
        RefreshMainboardManager();
    }

    private void RefreshMainboardManager()
    {
        if (edhLabCommander is null || MainboardManagerList is null ||
            MainboardBox is null || MainboardManagerStatus is null) return;

        string? selectedName =
            (MainboardManagerList.SelectedItem as EdhLabMainboardView.Row) is
                { IsHeader: false } previous ? previous.Name : null;
        try
        {
            var entries = NativeDeckEditing.ParseMainboard(MainboardBox.Text);
            var rows = EdhLabMainboardView.Build(edhCatalog,
                entries.Select(x => (x.Name, x.Quantity)).ToArray(),
                MainboardManagerSection, MainboardManagerOrder,
                MainboardManagerSearchBox?.Text,
                commander: edhLabCommander);
            // Type headings are interactive; keep them visible while hiding
            // children of collapsed groups. Name/quantity sorts contain no
            // headers and intentionally bypass section collapsing.
            var displayed = rows
                .Where(r => r.IsHeader || !collapsedMainboardGroups.Contains(r.Category))
                .Select(r => r.IsHeader ? r with {
                    Name = (collapsedMainboardGroups.Contains(r.Category) ? "▸ " : "▾ ") +
                        r.Name
                } : r).ToArray();
            updatingMainboardManager = true;
            try
            {
                MainboardManagerList.ItemsSource = displayed;
                if (selectedName is not null)
                    MainboardManagerList.SelectedItem = displayed.FirstOrDefault(x =>
                        !x.IsHeader && x.Name.Equals(selectedName,
                            StringComparison.OrdinalIgnoreCase));
            }
            finally { updatingMainboardManager = false; }

            MainboardCompositionSummary.Text = EdhLabMainboardView.CompositionSummary(
                edhCatalog, entries.Select(x => (x.Name, x.Quantity)).ToArray());
            int visible = displayed.Count(x => !x.IsHeader && !x.IsCommander);
            int hidden = rows.Count(x => !x.IsHeader && !x.IsCommander) - visible;
            int all = entries.Count;
            int unverified = entries.Count(x => edhCatalog?.Find(x.Name) is null);
            MainboardManagerStatus.Text =
                (visible == 0 && all > 0 && hidden == 0 ?
                    "No matching mainboard cards · " : "") +
                $"{visible:N0} shown" + (hidden > 0 ? $" · {hidden:N0} collapsed" : "") +
                $" · {all:N0} names · " +
                $"{entries.Sum(x => x.Quantity):N0} unsaved copies" +
                (unverified > 0 && edhCatalog is not null
                    ? $" · {unverified:N0} locally unverified" : "") +
                ". Select a card to inspect or change its count.";
            UpdateMainboardManagerSelection();
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException
            or OverflowException)
        {
            updatingMainboardManager = true;
            try { MainboardManagerList.ItemsSource =
                Array.Empty<EdhLabMainboardView.Row>(); }
            finally { updatingMainboardManager = false; }
            MainboardCompositionSummary.Text = "Composition unavailable until deck text is valid.";
            MainboardManagerStatus.Text =
                "Correct the Text tab before managing cards: " + ex.Message;
            UpdateMainboardManagerSelection();
        }
    }

    private void UpdateMainboardManagerSelection()
    {
        if (MainboardManagerSelectedText is null ||
            MainboardManagerMinus is null || MainboardManagerPlus is null ||
            MainboardManagerRemove is null) return;
        var selected = MainboardManagerList.SelectedItem as EdhLabMainboardView.Row;
        MainboardManagerFullDetails.IsEnabled =
            !busy && selected is { IsHeader: false };
        bool usable = !busy && selected is { IsHeader: false, IsCommander: false };
        MainboardManagerMinus.IsEnabled = usable;
        MainboardManagerRemove.IsEnabled = usable;
        var verified = selected is null ? null : edhCatalog?.Find(selected.Name);
        MainboardManagerPlus.IsEnabled = usable &&
            verified is not null && selected!.Quantity < verified.SuggestedCopyLimit;
        MainboardManagerSelectedText.Text = usable
            ? $"{selected!.Name} · {selected.Quantity:N0} in mainboard" +
              (verified is null ? " · catalog eligibility unverified" :
                $" · locally supported maximum {verified.SuggestedCopyLimit:N0}")
            : selected is { IsCommander: true }
                ? "Commander is shown for reference. Change it using the Commander field."
                : "Select a card to view its details and adjust its unsaved quantity.";
    }

    private void OnMainboardManagerSelected(object sender,
        SelectionChangedEventArgs e)
    {
        if (updatingMainboardManager) return;
        if (MainboardManagerList.SelectedItem is EdhLabMainboardView.Row
            { IsHeader: true } heading)
        {
            if (!collapsedMainboardGroups.Add(heading.Category))
                collapsedMainboardGroups.Remove(heading.Category);
            RefreshMainboardManager();
            return;
        }
        UpdateMainboardManagerSelection();
        RefreshSelectedMainboardPrintings();
        if (busy || edhLabCommander is null ||
            MainboardManagerList.SelectedItem is not
                EdhLabMainboardView.Row { IsHeader: false } selected ||
            OwnedPoolBox is null) return;
        // The Cards tab and raw text preview are independent of this view.
        // Do not keep an unrelated browser row selected for Add/Remove (+1).
        OwnedCardsList?.UnselectAll();
        if (previewedCard?.Name.Equals(selected.Name,
                StringComparison.OrdinalIgnoreCase) == true &&
            previewedPool == OwnedPoolBox.SelectedIndex)
            return;

        var pool = OwnedPoolBox.SelectedIndex == 1
            ? NativeDeckOwnedPicker.Pool.Arena
            : NativeDeckOwnedPicker.Pool.Physical;
        long owned = NativeDeckOwnedPicker.CountOwned(ownership, pool,
            selected.Name);
        var catalogCard = edhCatalog?.Find(selected.Name);
        var fit = edhCatalog?.SynergyFor(selected.Name);
        _ = ShowCardPreviewAsync(new OwnedDisplay(selected.Name, owned,
            catalogCard?.RoleHints ?? "",
            LabSuggestion: catalogCard is not null,
            SynergyExplanation: fit?.Why ?? "No verified commander mechanic match.",
            MechanicMatch: fit?.Matches == true,
            InDraftCopies: selected.Quantity,
            Proxied: CountProxy(selected.Name)));
    }

    private void OnMainboardManagerDoubleClick(object sender,
        System.Windows.Input.MouseButtonEventArgs e)
    {
        if (MainboardManagerList.SelectedItem is EdhLabMainboardView.Row
            { IsHeader: false })
        {
            e.Handled = true;
            OnMainboardManagerFullDetailsClicked(sender, new RoutedEventArgs());
        }
    }

    private async void OnMainboardManagerFullDetailsClicked(object sender,
        RoutedEventArgs e)
    {
        if (busy || MainboardManagerList.SelectedItem is not
            EdhLabMainboardView.Row { IsHeader: false } selected) return;
        string savedName = selected.Name;
        string identity = ArenaDeckCardName.Identity(savedName);
        string status = selected.IsCommander
            ? "Commander · reference in unsaved deck"
            : selected.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                " copies in unsaved mainboard · collection unchanged";
        bool arenaScope = OwnedPoolBox?.SelectedIndex == 1;
        var printing = NativeDeckCoverCache.ParseArenaPrintingHint(savedName);
        string? offlineCatalog = catalogPath;
        CollectionCatalogReview.CardDetails? details = null;
        string art = "";
        try
        {
            if (!string.IsNullOrWhiteSpace(offlineCatalog))
            {
                var result = await Task.Run(() =>
                {
                    var info = CollectionCatalogReview.ReadDetails(offlineCatalog!, identity);
                    string image = printing.SetCode.Length > 0
                        ? NativePrintingArtwork.ReadArenaPrintingImage(offlineCatalog!,
                            identity, printing.SetCode, printing.CollectorNumber)
                        : "";
                    if (!NativeCardArtwork.IsTrustedArtworkUrl(image))
                        image = info?.ArtworkUrl ?? "";
                    return (Info: info, Artwork: image);
                });
                details = result.Info;
                art = result.Artwork;
            }
            if (!IsLoaded) return;
            var dialog = new NativeCardDetailsWindow(identity, status, details,
                catalogPath: offlineCatalog, selectedSet: printing.SetCode,
                selectedCollector: printing.CollectorNumber, arenaScope: arenaScope,
                selectedArtworkUrl: art)
            {
                Owner = Window.GetWindow(this)
            };
            dialog.ShowDialog();
        }
        catch (Exception ex)
        {
            if (IsLoaded)
                MainboardManagerStatus.Text = "Unable to open card details: " + ex.Message;
        }
    }

        private void ChangeManagedMainboardQuantity(int change, bool removeAll = false)
    {
        if (busy || edhLabCommander is null ||
            MainboardManagerList.SelectedItem is not
                EdhLabMainboardView.Row { IsHeader: false, IsCommander: false } selected)
            return;
        try
        {
            // Never trust a potentially stale list row. Always resolve the
            // exact current quantity from the authoritative unsaved Text tab.
            var existing = NativeDeckEditing.ParseMainboard(MainboardBox.Text)
                .FirstOrDefault(x => x.Name.Equals(selected.Name,
                    StringComparison.OrdinalIgnoreCase));
            if (existing is null)
                throw new InvalidDataException(
                    "The selected card changed. Refresh the mainboard view.");
            int newCount = removeAll ? 0 : checked(existing.Quantity + change);
            if (newCount < 0)
                throw new InvalidDataException("This card is already absent.");
            if (newCount > existing.Quantity)
            {
                var card = edhCatalog?.Find(existing.Name);
                if (card is null)
                    throw new InvalidDataException(
                        "Increasing an unverified card requires a synced, locally legal EDH catalog.");
                if (newCount > card.SuggestedCopyLimit)
                    throw new InvalidDataException(existing.Name +
                        $" is limited to {card.SuggestedCopyLimit:N0} locally eligible copies.");
            }
            string updated = NativeDeckOwnedPicker.SetQuantity(
                MainboardBox.Text, existing.Name, newCount);
            if (updated == MainboardBox.Text) return;
            MainboardBox.Text = updated;
            RefreshMainboardManager();
            RefreshOwnedCards();
            StatusText.Text = newCount == 0
                ? $"Removed all {existing.Quantity:N0} copies of {existing.Name} from the unsaved mainboard."
                : $"Set {existing.Name} to {newCount:N0} unsaved mainboard copies. " +
                    "Ownership is unchanged; save still requires preflight.";
        }
        catch (Exception ex) when (ex is InvalidDataException or
            ArgumentException or OverflowException)
        {
            StatusText.Text = "Could not change mainboard card: " + ex.Message;
        }
    }

    private void OnMainboardManagerMinusClicked(object sender, RoutedEventArgs e) =>
        ChangeManagedMainboardQuantity(-1);

    private void OnMainboardManagerPlusClicked(object sender, RoutedEventArgs e) =>
        ChangeManagedMainboardQuantity(1);

    private void OnMainboardManagerRemoveClicked(object sender, RoutedEventArgs e) =>
        ChangeManagedMainboardQuantity(0, removeAll: true);
}
