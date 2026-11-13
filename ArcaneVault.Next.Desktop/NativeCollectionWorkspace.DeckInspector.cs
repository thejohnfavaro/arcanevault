using ArcaneVault.Next.Collection;
using ArcaneVault.Next.EDHLab;
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
    // View-only projection: the commander is stored in the deck's separate
    // leader slot and must never be injected into MainboardText.
    private sealed record InspectorCardRow(string Name, int Quantity, bool IsCommander)
    {
        public string DisplayName => IsCommander ? Name + "  ·  Commander" : Name;
    }

    private InspectorCardRow? inspectedDeckCard;

    private NativeDeckOwnedPicker.Pool InspectorPool =>
        DeckInspectorOwnershipPool?.SelectedIndex == 1
            ? NativeDeckOwnedPicker.Pool.Arena
            : NativeDeckOwnedPicker.Pool.Physical;

    private static bool UsesSeparateCommander(string? format) =>
        string.Equals(format?.Trim(), "Commander", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(format?.Trim(), "Arena Brawl", StringComparison.OrdinalIgnoreCase);

    private void OnDeckInspectorOwnershipPoolChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DeckInspectorOwnershipPool is null ||
            !ReferenceEquals(e.OriginalSource, DeckInspectorOwnershipPool)) return;
        UpdateDeckInspectorCoverage();
    }

    // Do not infer a printing, legality or reservation in any other deck.
    // All counts are within ONE explicitly selected physical/Arena pool.
    private void UpdateDeckInspectorCoverage()
    {
        if (DeckInspectorCoverageSummary is null ||
            DeckInspectorCoverageGaps is null ||
            DeckInspectorCardAvailability is null) return;
        var deck = inspectedSavedDeck;
        ReviewDeckShortagesButton.IsEnabled = false;
        if (deck is null)
        {
            DeckInspectorCoverageSummary.Text = "";
            DeckInspectorCoverageGaps.Text = "";
            DeckInspectorCardAvailability.Text = "";
            return;
        }
        if (!deck.Editable)
        {
            DeckInspectorCoverageSummary.Text = "Coverage unavailable: saved deck needs review.";
            DeckInspectorCoverageGaps.Text =
                "Unsupported imported entries may be missing from the displayed mainboard.";
            DeckInspectorCardAvailability.Text = "";
            return;
        }
        try
        {
            var coverage = NativeDeckOwnedPicker.Review(
                backup, InspectorPool, deck.MainboardText, deck.Commander, deck.Format,
                proxyRows);
            string scope = InspectorPool == NativeDeckOwnedPicker.Pool.Arena
                ? "Arena" : "Physical";
            DeckInspectorCoverageSummary.Text =
                $"{scope}: {coverage.CoveredCopies:N0}/{coverage.TotalWithCommander:N0} " +
                $"copies covered · {coverage.MissingCopies:N0} missing" +
                (coverage.ProxyCoveredCopies > 0
                    ? $" · {coverage.ProxyCoveredCopies:N0} Proxy copies (not owned or valued)"
                    : "");
            ReviewDeckShortagesButton.IsEnabled = !deckBusy &&
                deck.Editable && coverage.MissingCopies > 0 &&
                !string.IsNullOrWhiteSpace(nativePath) &&
                !string.IsNullOrWhiteSpace(expectedBackupSha) &&
                !string.IsNullOrWhiteSpace(catalogPath) &&
                File.Exists(catalogPath) &&
                !string.IsNullOrWhiteSpace(deck.Commander) &&
                EdhLabSavedDeckAccess.FormatFor(deck.Format) is not null;
            DeckInspectorCoverageGaps.Text = coverage.Shortages.Count == 0
                ? "All required names covered by recorded copies. No copies are reserved; this is not a legality or printing check."
                : "Shortages (by name): " +
                  string.Join(" · ", coverage.Shortages.Take(4)
                      .Select(x => $"{x.Missing:N0} × {x.Name}")) +
                  (coverage.Shortages.Count > 4
                      ? $" · +{coverage.Shortages.Count - 4:N0} more names" : "") +
                  ". Counts may be shared with other saved decks.";
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException or OverflowException)
        {
            DeckInspectorCoverageSummary.Text = "Coverage unavailable for this saved deck.";
            DeckInspectorCoverageGaps.Text =
                "Review the mainboard in Deck Studio; collection counts were not changed.";
        }
        UpdateDeckInspectorCardAvailability();
    }

    private void UpdateDeckInspectorCardAvailability()
    {
        if (DeckInspectorCardAvailability is null) return;
        var deck = inspectedSavedDeck;
        var card = inspectedDeckCard;
        if (deck is null || card is null || !deck.Editable)
        {
            DeckInspectorCardAvailability.Text = "";
            return;
        }
        long recorded = NativeDeckOwnedPicker.CountOwned(backup, InspectorPool, card.Name);
        bool includesLeader = UsesSeparateCommander(deck.Format) &&
            string.Equals(deck.Commander?.Trim(), card.Name,
                StringComparison.OrdinalIgnoreCase);
        // A commander selected in the command zone is one required copy;
        // if the same name is also in the imported mainboard, count those
        // additional copies exactly once, not once per displayed row.
        int needed = card.IsCommander
            ? checked(1 + NativeDeckEditing.ParseMainboard(deck.MainboardText)
                .Where(x => x.Name.Equals(card.Name, StringComparison.OrdinalIgnoreCase))
                .Sum(x => x.Quantity))
            : checked(card.Quantity + (includesLeader ? 1 : 0));
        long proxy = InspectorPool == NativeDeckOwnedPicker.Pool.Physical
            ? proxyRows.FirstOrDefault(x =>
                x.Name.Equals(card.Name, StringComparison.OrdinalIgnoreCase))?.Quantity ?? 0
            : 0;
        long missing = Math.Max(0, needed - recorded - proxy);
        string scope = InspectorPool == NativeDeckOwnedPicker.Pool.Arena
            ? "Arena" : "Physical";
        DeckInspectorCardAvailability.Text =
            $"{scope}: {recorded:N0} real owned" +
            (proxy > 0 ? $" · {proxy:N0} Proxy (not valued)" : "") +
            $" · {needed:N0} required" +
            (includesLeader ? " (including commander)" : "") +
            (missing == 0 ? " · covered" : $" · {missing:N0} missing");
    }

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
        DeckInspectorCoverageSummary.Text = "";
        DeckInspectorCoverageGaps.Text = "";
        DeckInspectorCardAvailability.Text = "";

        if (deck is null)
        {
            DeckInspectorPanel.Visibility = Visibility.Collapsed;
            return;
        }

        DeckInspectorPanel.Visibility = Visibility.Visible;
        DeckInspectorTitle.Text = deck.Name;
        // Arena Brawl defaults to Arena; all other formats start Physical.
        // Either pool remains independently selectable for comparison.
        DeckInspectorOwnershipPool.SelectedIndex =
            string.Equals(deck.Format?.Trim(), "Arena Brawl",
                StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        bool hasCommander = UsesSeparateCommander(deck.Format) &&
            !string.IsNullOrWhiteSpace(deck.Commander);
        int displayedTotal = checked(deck.TotalCopies + (hasCommander ? 1 : 0));
        DeckInspectorSummary.Text =
            $"{deck.Format} · {displayedTotal:N0} " +
            (hasCommander ? "cards incl. commander" : "mainboard copies") + " · " +
            $"{deck.EntryCount:N0} mainboard names" +
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
            UpdateDeckInspectorCoverage();
            return;
        }
        // Commander is displayed first, but remains a separate saved deck
        // field rather than a fabricated mainboard entry or owned printing.
        var displayRows = new List<InspectorCardRow>(cards.Count + (hasCommander ? 1 : 0));
        if (hasCommander)
            displayRows.Add(new InspectorCardRow(deck.Commander.Trim(), 1, true));
        displayRows.AddRange(cards.Select(x =>
            new InspectorCardRow(x.Name, x.Quantity, false)));
        DeckInspectorCardsList.ItemsSource = displayRows;
        DeckInspectorStatus.Text = displayRows.Count == 0
            ? "Empty mainboard · no cards to preview."
            : "Select the commander or a mainboard card to preview. Saved quantities do not prove ownership.";
        if (displayRows.Count > 0)
            DeckInspectorCardsList.SelectedIndex = 0;
        UpdateDeckInspectorCoverage();
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
        var selected = DeckInspectorCardsList.SelectedItem as InspectorCardRow;
        inspectedDeckCard = selected;
        int version = ++deckInspectVersion;
        var deck = inspectedSavedDeck;
        DeckInspectorFullDetailsButton.IsEnabled = selected is not null;
        DeckInspectorArt.ArtUrl = "";
        DeckInspectorManaCost.Value = "";
        DeckInspectorColorIdentity.ItemsSource = null;
        DeckInspectorCardType.Text = "";
        DeckInspectorOracle.Document = NativeOracleReading.Build("");
        if (selected is null || deck is null) return;

        DeckInspectorCardName.Text = selected.IsCommander
            ? $"Commander · {selected.Name}"
            : $"{selected.Quantity} × {selected.Name}";
        UpdateDeckInspectorCardAvailability();
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
        DeckInspectorManaCost.Value = details.ManaCost;
        DeckInspectorColorIdentity.ItemsSource =
            NativeManaIdentity.ParseColorIdentity(details.ColorIdentity);
        DeckInspectorOracle.Document =
            NativeOracleReading.Build(details.OracleText);
        DeckInspectorStatus.Text =
            (selected.IsCommander ? "Command zone · " : "") +
            "Read-only card identity · illustration is not verification of your owned printing.";
    }

    private void OnDeckInspectorCardDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not FrameworkElement
            { DataContext: InspectorCardRow entry } ||
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
            (card.IsCommander ? "Commander in " : $"{card.Quantity} saved in ") +
            $"{deck.Name} · saved deck entry, not verified ownership",
            details, catalogPath)
        { Owner = Window.GetWindow(this) };
        dialog.Show();
    }
}
