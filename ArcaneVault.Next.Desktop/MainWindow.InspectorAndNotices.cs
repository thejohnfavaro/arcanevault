using ArcaneVault.Next.Collection;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Input;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// Persistent, read-only card inspector and native, nonmodal activity UI.
/// The catalog list stays virtualized and all card details are loaded on
/// selection with request-version guards so rapid browsing cannot show stale
/// metadata. Full Card Details stays optional via an explicit button.
/// </summary>
public partial class MainWindow
{
    private readonly NativeNoticeJournal activity = new();
    private readonly ObservableCollection<NativeNoticeJournal.Entry> recentActivity = new();
    private readonly ObservableCollection<NativeNoticeJournal.Entry> visibleNotices = new();
    private CatalogSnapshot.PrintingRow? inspectorSelection;
    private int inspectorRequestVersion;
    private bool inspectorExpanded;

    private void InitializeInAppNotices()
    {
        ActiveNoticeItems.ItemsSource = visibleNotices;
        ActivityHistoryItems.ItemsSource = recentActivity;
        ActivityHistoryButton.Content = "◉   Activity (0)";
        NativeNoticeHub.Published += PostAppNotice;
    }

    private void PostAppNotice(string title, string message, bool warning = false)
    {
        if (closing) return;
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => PostAppNotice(title, message, warning));
            return;
        }
        var entry = activity.Post(title, message, warning);
        recentActivity.Insert(0, entry);
        while (recentActivity.Count > NativeNoticeJournal.MaximumHistory)
            recentActivity.RemoveAt(recentActivity.Count - 1);
        visibleNotices.Insert(0, entry);
        while (visibleNotices.Count > 3) visibleNotices.RemoveAt(visibleNotices.Count - 1);
        ActivityHistoryButton.Content = $"◉   Activity ({recentActivity.Count})";
        _ = RetireNoticeAsync(entry.Id);
    }

    private async Task RetireNoticeAsync(Guid id)
    {
        await Task.Delay(6500);
        if (closing) return;
        var entry = visibleNotices.FirstOrDefault(n => n.Id == id);
        if (entry is not null) visibleNotices.Remove(entry);
    }

    private void DisconnectInAppNotices() => NativeNoticeHub.Published -= PostAppNotice;

    private void OnDismissNoticeClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: Guid id }) return;
        var entry = visibleNotices.FirstOrDefault(n => n.Id == id);
        if (entry is not null) visibleNotices.Remove(entry);
    }

    private void OnActivityHistoryClicked(object sender, RoutedEventArgs e) =>
        ActivityHistoryPanel.Visibility = ActivityHistoryPanel.Visibility == Visibility.Visible
            ? Visibility.Collapsed : Visibility.Visible;

    private void OnClearActivityClicked(object sender, RoutedEventArgs e)
    {
        activity.Clear();
        recentActivity.Clear();
        visibleNotices.Clear();
        ActivityHistoryButton.Content = "◉   Activity (0)";
        ActivityHistoryPanel.Visibility = Visibility.Collapsed;
    }

    private void OnInspectorCloseClicked(object sender, RoutedEventArgs e)
    {
        inspectorRequestVersion++;
        inspectorSelection = null;
        CardInspectorPanel.Visibility = Visibility.Collapsed;
        InspectorArt.ArtUrl = "";
        InspectorOracle.Document = new FlowDocument();
    }

    private void SetInspectorExpanded(bool expanded)
    {
        inspectorExpanded = expanded;
        CardInspectorPanel.Width = expanded
            ? Math.Min(460, Math.Max(342, CatalogWorkspace.ActualWidth * 0.46))
            : 342;
        InspectorExpandButton.Content = expanded ? "Narrow inspector" : "Widen inspector";
    }

    private void OnInspectorExpandClicked(object sender, RoutedEventArgs e) =>
        SetInspectorExpanded(!inspectorExpanded);

    private void OnInspectorFullDetailsClicked(object sender, RoutedEventArgs e)
    {
        if (inspectorSelection is not null)
            OnCatalogCardDetailsClicked(sender, e);
    }

    private void OnCatalogRowSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CatalogViewMode is null || CatalogViewMode.SelectedIndex == 2 ||
            CardsList.SelectedItem is not CatalogSnapshot.PrintingRow selected)
            return;
        _ = ShowCardInInspectorAsync(selected);
    }

    private static FlowDocument OraclePreview(string? raw)
    {
        var document = new FlowDocument
        {
            PagePadding = new Thickness(0),
            FontSize = 12,
            LineHeight = 18
        };
        foreach (string line in (raw ?? "").Replace("\r\n", "\n").Split('\n'))
        {
            var paragraph = new Paragraph { Margin = new Thickness(0, 0, 0, 3) };
            foreach (var part in NativeOracleSymbolTokenizer.Split(line))
            {
                if (part.IsSymbol && part.Text.Length >= 3)
                    paragraph.Inlines.Add(new InlineUIContainer(new NativeManaSymbol
                    {
                        Symbol = part.Text[1..^1],
                        Width = 17, Height = 17
                    }) { BaselineAlignment = BaselineAlignment.Center });
                else
                    paragraph.Inlines.Add(new Run(part.Text));
            }
            document.Blocks.Add(paragraph);
        }
        return document;
    }

    private async Task ShowCardInInspectorAsync(CatalogSnapshot.PrintingRow selected)
    {
        if (closing) return;
        var snapshot = catalogSnapshot;
        int version = ++inspectorRequestVersion;
        int catalogVersion = modeVersion;
        inspectorSelection = selected;
        CardInspectorPanel.Visibility = Visibility.Visible;
        InspectorName.Text = selected.Name;
        InspectorName.ToolTip = selected.Name;
        InspectorArt.ArtUrl = selected.ImageUrl;
        InspectorPrinting.Text = selected.ArenaPresentation
            ? "Arena available · one per card name"
            : $"{selected.SetCode.ToUpperInvariant()}  #{selected.CollectorNumber}  ·  {selected.RarityLabel}";
        InspectorOwnership.Text = selected.OwnershipLabel;
        InspectorType.Text = "Loading card details…";
        InspectorIdentityIcons.ItemsSource = null;
        InspectorOracle.Document = OraclePreview("Loading Oracle text…");
        InspectorStatus.Text = "Read-only catalog preview · choose another card anytime";
        if (snapshot is null)
        {
            InspectorType.Text = "Open a real Scryfall catalog to see card rules.";
            return;
        }
        CollectionCatalogReview.CardDetails? details;
        try
        {
            details = await Task.Run(() =>
                CollectionCatalogReview.ReadDetails(snapshot.ReadOnlySnapshotPath, selected.Name));
        }
        catch (Exception)
        {
            details = null;
        }
        if (closing || version != inspectorRequestVersion ||
            catalogVersion != modeVersion ||
            !ReferenceEquals(snapshot, catalogSnapshot))
            return;
        if (details is null)
        {
            InspectorType.Text = "No cached card metadata";
            InspectorOracle.Document = OraclePreview(
                "Card details are unavailable in the local catalog. No collection data was modified.");
            return;
        }
        InspectorType.Text = details.TypeLine;
        InspectorIdentityIcons.ItemsSource = NativeManaIdentity.ParseColorIdentity(details.ColorIdentity);
        InspectorOracle.Document = OraclePreview(details.OracleText);
        InspectorStatus.Text = "Select a different card to update this pane; full details are optional.";
    }
}
