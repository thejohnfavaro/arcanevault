using ArcaneVault.Next.Collection;
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
                MainboardManagerSearchBox?.Text);
            updatingMainboardManager = true;
            try
            {
                MainboardManagerList.ItemsSource = rows;
                if (selectedName is not null)
                    MainboardManagerList.SelectedItem = rows.FirstOrDefault(x =>
                        !x.IsHeader && x.Name.Equals(selectedName,
                            StringComparison.OrdinalIgnoreCase));
            }
            finally { updatingMainboardManager = false; }

            int visible = rows.Count(x => !x.IsHeader);
            int all = entries.Count;
            int unverified = entries.Count(x => edhCatalog?.Find(x.Name) is null);
            MainboardManagerStatus.Text =
                (visible == 0 && all > 0 ? "No matching mainboard cards · " : "") +
                $"{visible:N0} shown · {all:N0} names · " +
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
        bool usable = !busy && selected is { IsHeader: false };
        MainboardManagerMinus.IsEnabled = usable;
        MainboardManagerRemove.IsEnabled = usable;
        var verified = selected is null ? null : edhCatalog?.Find(selected.Name);
        MainboardManagerPlus.IsEnabled = usable &&
            verified is not null && selected!.Quantity < verified.SuggestedCopyLimit;
        MainboardManagerSelectedText.Text = usable
            ? $"{selected!.Name} · {selected.Quantity:N0} in mainboard" +
              (verified is null ? " · catalog eligibility unverified" :
                $" · locally supported maximum {verified.SuggestedCopyLimit:N0}")
            : "Select a card to view its details and adjust its unsaved quantity.";
    }

    private void OnMainboardManagerSelected(object sender,
        SelectionChangedEventArgs e)
    {
        if (updatingMainboardManager) return;
        UpdateMainboardManagerSelection();
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
            InDraftCopies: selected.Quantity));
    }

    private void ChangeManagedMainboardQuantity(int change, bool removeAll = false)
    {
        if (busy || edhLabCommander is null ||
            MainboardManagerList.SelectedItem is not
                EdhLabMainboardView.Row { IsHeader: false } selected)
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
