using ArcaneVault.Next.Collection;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// Read-only, selection-driven Deck Library inspector. This does not edit
/// the deck or infer exact owned printings from its name-level mainboard.
/// Every catalog read is guarded against stale selection and stale views.
/// </summary>
public partial class NativeCollectionWorkspace
{
    private int deckInspectVersion;
    private NativeDeckEditing.DeckRow? inspectedSavedDeck;
    private NativeDeckEditing.Entry? inspectedDeckCard;

    private void ShowDeckInspector(NativeDeckEditing.DeckRow? deck)
    {
        ++deckInspectVersion;
        inspectedSavedDeck = deck;
        inspectedDeckCard = null;
        if (DeckInspectorPanel is null) return;
        DeckInspectorCardsList.ItemsSource = null;
        DeckInspectorArt.ArtUrl = "";
        DeckInspectorCardName.Text = "Choose a card to inspect";
        DeckInspectorCardType.Text = "";
        DeckInspectorColorIdentity.ItemsSource = null;
        DeckInspectorOracle.Document = NativeOracleReading.Build("");
        DeckInspectorFullDetailsButton.IsEnabled = false;

        if (deck is null)
        {
            DeckInspectorPanel.Visibility = Visibility.Collapsed;
            return;
        }

        DeckInspectorPanel.Visibility = Visibility.Visible;
        DeckInspectorTitle.Text = deck.Name;
        DeckInspectorSummary.Text =
            $"{deck.Format} · {deck.TotalCopies:N0} copies · {deck.EntryCount:N0} names" +
            (deck.Editable ? "" : " · imported list requires review");
        IReadOnlyList<NativeDeckEditing.Entry> cards;
        try
        {
            cards = NativeDeckEditing.ParseMainboard(deck.MainboardText);
        }
        catch (Exception ex) when (ex is InvalidDataException or OverflowException or ArgumentException)
        {
            DeckInspectorStatus.Text =
                "This saved deck has unsupported entries. Open the deck editor to review. Nothing was changed.";
            return;
        }
        DeckInspectorCardsList.ItemsSource = cards;
        DeckInspectorStatus.Text = cards.Count == 0
            ? "Empty mainboard · no cards to preview."
            : "Select a mainboard card for an in-place preview. These are saved quantities, not proof of ownership.";
        if (cards.Count > 0)
            DeckInspectorCardsList.SelectedIndex = 0;
    }

    private void OnHideDeckInspectorClicked(object sender, RoutedEventArgs e)
    {
        ++deckInspectVersion;
        inspectedSavedDeck = null;
        inspectedDeckCard = null;
        DeckInspectorPanel.Visibility = Visibility.Collapsed;
        DeckInspectorArt.ArtUrl = "";
        DeckInspectorCardsList.ItemsSource = null;
    }

    private async void OnDeckInspectorCardSelected(object sender, SelectionChangedEventArgs e)
    {
        if (DeckInspectorCardsList is null) return;
        var selected = DeckInspectorCardsList.SelectedItem as NativeDeckEditing.Entry;
        inspectedDeckCard = selected;
        int version = ++deckInspectVersion;
        var deck = inspectedSavedDeck;
        DeckInspectorFullDetailsButton.IsEnabled = selected is not null;
        DeckInspectorArt.ArtUrl = "";
        DeckInspectorColorIdentity.ItemsSource = null;
        DeckInspectorCardType.Text = "";
        DeckInspectorOracle.Document = NativeOracleReading.Build("");
        if (selected is null || deck is null) return;

        DeckInspectorCardName.Text = $"{selected.Quantity} × {selected.Name}";
        DeckInspectorStatus.Text =
            "Loading local catalog rules · saved deck and inventory are unchanged.";
        if (string.IsNullOrWhiteSpace(catalogPath))
        {
            DeckInspectorStatus.Text =
                "No locally synced Scryfall catalog. The saved mainboard is still available offline.";
            return;
        }

        CollectionCatalogReview.CardDetails? details;
        try
        {
            details = await Task.Run(() =>
                CollectionCatalogReview.ReadDetails(catalogPath, selected.Name));
        }
        catch (Exception) { details = null; }
        if (closing || version != deckInspectVersion ||
            !ReferenceEquals(deck, inspectedSavedDeck) ||
            !ReferenceEquals(selected, inspectedDeckCard)) return;

        if (details is null)
        {
            DeckInspectorStatus.Text =
                "No local card metadata for this name. The saved deck and inventory are unchanged.";
            return;
        }
        DeckInspectorArt.ArtUrl = NativeCardArtwork.IsTrustedArtworkUrl(details.ArtworkUrl)
            ? details.ArtworkUrl : "";
        DeckInspectorCardType.Text = details.TypeLine;
        DeckInspectorColorIdentity.ItemsSource =
            NativeManaIdentity.ParseColorIdentity(details.ColorIdentity);
        DeckInspectorOracle.Document =
            NativeOracleReading.Build(details.OracleText);
        DeckInspectorStatus.Text =
            "Read-only card identity · illustration is not verification of your owned printing.";
    }

    private void OnDeckInspectorCardDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not FrameworkElement
            { DataContext: NativeDeckEditing.Entry entry } ||
            !ReferenceEquals(entry, inspectedDeckCard)) return;
        e.Handled = true;
        OnDeckInspectorFullDetailsClicked(sender, new RoutedEventArgs());
    }

    private async void OnDeckInspectorFullDetailsClicked(object sender, RoutedEventArgs e)
    {
        if (closing || inspectedSavedDeck is null || inspectedDeckCard is null) return;
        int version = deckInspectVersion;
        var deck = inspectedSavedDeck;
        var card = inspectedDeckCard;
        CollectionCatalogReview.CardDetails? details = null;
        if (!string.IsNullOrWhiteSpace(catalogPath))
        {
            try
            {
                details = await Task.Run(() =>
                    CollectionCatalogReview.ReadDetails(catalogPath, card.Name));
            }
            catch (Exception) { /* The full window has an honest offline fallback. */ }
        }
        if (closing || version != deckInspectVersion ||
            !ReferenceEquals(deck, inspectedSavedDeck) ||
            !ReferenceEquals(card, inspectedDeckCard)) return;

        var dialog = new NativeCardDetailsWindow(card.Name,
            $"{card.Quantity} saved in {deck.Name} · saved deck entry, not verified ownership",
            details, catalogPath)
        { Owner = Window.GetWindow(this) };
        dialog.Show();
    }
}
