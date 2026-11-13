using ArcaneVault.Next.Collection;
using ArcaneVault.Next.Domain;
using ArcaneVault.Next.EDHLab;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace ArcaneVault.Next.Desktop;

public partial class CommanderBrowserWindow : Window
{
    private sealed record CommanderRow(EdhLabCommanderCard Card)
    {
        public string Name => Card.Name;
        public string TypeLine => Card.TypeLine;
        public string InventoryText { get; init; } = "";

    }

    private readonly EdhLabCommanderIndex index;
    private readonly CommanderInventory inventory;
    private readonly bool hasCollection;
    private readonly Func<EdhLabCommanderCard, EdhLabFormat, Task>? startDraft;
    private readonly string? catalogPath;
    private bool drafting;
    private readonly EdhLabRandomPicker picker = new();
    private int page;
    private bool initialized;
    private bool updatingColors;
    private int commanderCostVersion;

    public CommanderBrowserWindow(EdhLabCommanderIndex index,
        CommanderInventory? inventory = null, bool hasCollection = false,
        Func<EdhLabCommanderCard, EdhLabFormat, Task>? startDraft = null,
        string? catalogPath = null)
    {
        this.index = index;
        this.inventory = inventory ?? CommanderInventory.Empty;
        this.hasCollection = hasCollection;
        this.startDraft = startDraft;
        this.catalogPath = catalogPath;
        InitializeComponent();
        initialized = true;
        RecordedOnly.IsEnabled = hasCollection;
        if (!hasCollection)
            RecordedOnly.ToolTip = "Open or import a native collection to filter by recorded inventory.";
        RenderPage();
        UpdateDraftButton();
    }

    private void UpdateDraftButton()
    {
        if (BuildDeckButton is null) return;
        BuildDeckButton.IsEnabled = !drafting && hasCollection &&
            startDraft is not null && CommanderList.SelectedItem is CommanderRow;
    }

    private void OnCommanderSelected(object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        UpdateDraftButton();
        if (CommanderInspectorPanel is null) return;
        ++commanderCostVersion;
        CommanderInspectorManaCost.Value = "";
        if (CommanderList.SelectedItem is not CommanderRow selected)
        {
            CommanderInspectorPanel.Visibility = Visibility.Collapsed;
            CommanderInspectorArt.ArtUrl = "";
            return;
        }
        var card = selected.Card;
        CommanderInspectorPanel.Visibility = Visibility.Visible;
        CommanderInspectorName.Text = card.Name;
        CommanderInspectorArt.ArtUrl = NativeCardArtwork.IsTrustedArtworkUrl(card.ImageUrl)
            ? card.ImageUrl : "";
        CommanderInspectorType.Text = card.TypeLine;
        CommanderInspectorOwnership.Text = selected.InventoryText +
            " · name-level, not verified printing ownership";
        CommanderInspectorIdentity.ItemsSource =
            NativeManaIdentity.ParseColorIdentity(card.ColorIdentity);
        CommanderInspectorOracle.Document =
            NativeOracleReading.Build(card.OracleText);
        _ = LoadCommanderCostAsync(card.Name, commanderCostVersion);
        CommanderInspectorStatus.Text =
            "Format eligibility uses cached legalities, not live bans. Selecting a commander does not modify your deck.";
    }

    private async Task LoadCommanderCostAsync(string name, int version)
    {
        if (string.IsNullOrWhiteSpace(catalogPath)) return;
        try
        {
            // Selection changes are cheap; avoid querying SQLite for
            // intermediate rows during rapid keyboard browsing.
            await Task.Delay(150);
            if (version != commanderCostVersion) return;
            string cost = await Task.Run(() =>
                CollectionCatalogReview.ReadDetails(catalogPath, name)?.ManaCost ?? "");
            if (version == commanderCostVersion &&
                CommanderList.SelectedItem is CommanderRow current &&
                current.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                CommanderInspectorManaCost.Value = cost;
        }
        catch (Exception ex) when (ex is IOException or
            Microsoft.Data.Sqlite.SqliteException or InvalidDataException or
            ArgumentException)
        {
            // Optional missing mana_cost metadata never blocks inspecting.
        }
    }

    private void OnCloseCommanderInspectorClicked(object sender, RoutedEventArgs e)
    {
        ++commanderCostVersion;
        CommanderInspectorManaCost.Value = "";
        CommanderInspectorPanel.Visibility = Visibility.Collapsed;
        CommanderInspectorArt.ArtUrl = "";
    }

    private async void OnCommanderInspectorFullDetailsClicked(object sender, RoutedEventArgs e)
    {
        if (CommanderList.SelectedItem is not CommanderRow selected) return;
        CollectionCatalogReview.CardDetails? details = null;
        if (!string.IsNullOrWhiteSpace(catalogPath))
        {
            try
            {
                details = await Task.Run(() =>
                    CollectionCatalogReview.ReadDetails(catalogPath, selected.Name));
            }
            catch (Exception)
            {
                // Older/offline catalogs still allow inspection of the
                // commander row without inventing metadata or ownership.
            }
        }
        if (!IsLoaded || CommanderList.SelectedItem is not CommanderRow current ||
            !string.Equals(current.Name, selected.Name, StringComparison.OrdinalIgnoreCase))
            return;
        var dialog = new NativeCardDetailsWindow(selected.Name,
            selected.InventoryText + " · recorded name counts only",
            details, catalogPath) { Owner = this };
        dialog.Show();
    }

    private async void OnBuildDeckClicked(object sender, RoutedEventArgs e)
    {
        if (drafting || !hasCollection || startDraft is null ||
            CommanderList.SelectedItem is not CommanderRow selected) return;
        drafting = true;
        UpdateDraftButton();
        try
        {
            await startDraft(selected.Card,
                FormatChoice.SelectedIndex == 1
                    ? EdhLabFormat.ArenaBrawl : EdhLabFormat.PaperCommander);
        }
        catch (Exception ex)
        {
            ResultStatus.Text = "Could not open deck draft: " + ex.Message +
                ". No deck or inventory was changed.";
        }
        finally
        {
            drafting = false;
            UpdateDraftButton();
        }
    }

    private IEnumerable<CheckBox> ManaBoxes()
    {
        return new[] { WhiteColor, BlueColor, BlackColor, RedColor, GreenColor };
    }

    private string SelectedColors()
    {
        if (Colorless.IsChecked == true) return "C";
        return string.Concat(ManaBoxes().Where(box => box.IsChecked == true)
            .Select(box => box.Tag?.ToString() ?? ""));
    }

    private EdhLabQuery GetQuery() => new(
        FormatChoice.SelectedIndex == 1 ? EdhLabFormat.ArenaBrawl : EdhLabFormat.PaperCommander,
        SelectedColors(),
        ColorMatchChoice.SelectedIndex switch
        {
            1 => EdhLabColorMatch.ContainsAll,
            2 => EdhLabColorMatch.ContainsAny,
            _ => EdhLabColorMatch.Exact
        },
        CommanderSearch.Text,
        page,
        50);

    private void RenderPage(string? roll = null)
    {
        if (!initialized) return;
        var query = GetQuery();
        var result = index.Filter(query);
        var cards = inventory.Filter(result, query.Format, RecordedOnly.IsChecked == true);
        var pages = Math.Max(1, (cards.Count + query.PageSize - 1) / query.PageSize);
        page = Math.Clamp(page, 0, pages - 1);
        CommanderList.ItemsSource = cards.Skip(page * query.PageSize)
            .Take(query.PageSize).Select(c => new CommanderRow(c)
            {
                InventoryText = hasCollection
                    ? (inventory.Count(c.Name, query.Format) is var count && count > 0
                        ? $"Recorded: {count}" : "Not in inventory")
                    : "Inventory not loaded"
            }).ToArray();
        ResultStatus.Text = $"{cards.Count:N0} eligible identities · page {page + 1} of {pages}" +
            (roll is null ? "" : $" · Random: {roll}");
        PreviousButton.IsEnabled = page > 0;
        NextButton.IsEnabled = page < pages - 1;
        UpdateDraftButton();
    }

    private void OnRecordedOnlyChanged(object sender, RoutedEventArgs e)
    {
        if (!initialized) return;
        page = 0;
        picker.Reset();
        RenderPage();
    }

    private void OnFiltersChanged(object sender, EventArgs e)
    {
        if (!initialized || updatingColors) return;
        page = 0;
        picker.Reset();
        RenderPage();
    }

    private void OnColorsChanged(object sender, RoutedEventArgs e)
    {
        if (!initialized || updatingColors) return;
        updatingColors = true;
        try
        {
            if (ReferenceEquals(sender, Colorless) && Colorless.IsChecked == true)
            {
                foreach (var box in ManaBoxes()) box.IsChecked = false;
            }
            else if (sender is CheckBox checkedColor && checkedColor.IsChecked == true &&
                     !ReferenceEquals(checkedColor, Colorless))
                Colorless.IsChecked = false;
        }
        finally { updatingColors = false; }
        page = 0;
        picker.Reset();
        RenderPage();
    }

    private void OnClearColors(object sender, RoutedEventArgs e)
    {
        updatingColors = true;
        try
        {
            foreach (var box in ManaBoxes()) box.IsChecked = false;
            Colorless.IsChecked = false;
        }
        finally { updatingColors = false; }
        page = 0;
        picker.Reset();
        RenderPage();
    }

    private void OnPrevious(object sender, RoutedEventArgs e)
    {
        if (page == 0) return;
        page--;
        RenderPage();
    }

    private void OnNext(object sender, RoutedEventArgs e)
    {
        var query = GetQuery();
        var candidates = inventory.Filter(index.Filter(query), query.Format,
            RecordedOnly.IsChecked == true);
        if ((page + 1) * query.PageSize >= candidates.Count) return;
        page++;
        RenderPage();
    }

    private void OnRandomClicked(object sender, RoutedEventArgs e)
    {
        var query = GetQuery();
        // Random must draw only from the SAME pool the list currently shows.
        var eligible = inventory.Filter(index.Filter(query), query.Format,
            RecordedOnly.IsChecked == true);
        var ownedIndex = new EdhLabCommanderIndex(eligible);
        var selected = picker.Roll(ownedIndex, query, AvoidRepeats.IsChecked == true);
        if (selected is null)
        {
            ResultStatus.Text = "No eligible commanders match these filters. Clear colors or search.";
            return;
        }
        var filtered = eligible;
        var position = -1;
        for (var i = 0; i < filtered.Count; i++)
        {
            if (string.Equals(filtered[i].Name, selected.Name, StringComparison.OrdinalIgnoreCase))
            {
                position = i;
                break;
            }
        }
        if (position < 0) return;
        page = position / query.PageSize;
        RenderPage(selected.Name);
        var row = (CommanderList.ItemsSource as IEnumerable<CommanderRow>)?
            .FirstOrDefault(item => item.Name.Equals(selected.Name, StringComparison.OrdinalIgnoreCase));
        if (row is not null)
        {
            CommanderList.SelectedItem = row;
            CommanderList.ScrollIntoView(row);
        }
    }
}
