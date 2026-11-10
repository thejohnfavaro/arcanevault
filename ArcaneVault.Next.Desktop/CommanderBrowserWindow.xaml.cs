using ArcaneVault.Next.Domain;
using System.Windows;
using System.Windows.Controls;

namespace ArcaneVault.Next.Desktop;

public partial class CommanderBrowserWindow : Window
{
    private sealed record CommanderRow(EdhLabCommanderCard Card)
    {
        public string Name => Card.Name;
        public string TypeLine => Card.TypeLine;
        public string ColorText => string.IsNullOrEmpty(Card.ColorIdentity)
            ? "Colorless" : EdhLabCommanderRules.CanonicalColors(Card.ColorIdentity);
    }

    private readonly EdhLabCommanderIndex index;
    private readonly EdhLabRandomPicker picker = new();
    private int page;
    private bool initialized;
    private bool updatingColors;

    public CommanderBrowserWindow(EdhLabCommanderIndex index)
    {
        this.index = index;
        InitializeComponent();
        initialized = true;
        RenderPage();
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
        var result = index.Browse(GetQuery());
        CommanderList.ItemsSource = result.Cards.Select(c => new CommanderRow(c)).ToArray();
        var pages = Math.Max(1, result.TotalPages);
        ResultStatus.Text = $"{result.Total:N0} eligible identities · page {page + 1} of {pages}" +
            (roll is null ? "" : $" · Random: {roll}");
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
        var result = index.Browse(GetQuery());
        if (page + 1 >= result.TotalPages) return;
        page++;
        RenderPage();
    }

    private void OnRandomClicked(object sender, RoutedEventArgs e)
    {
        var query = GetQuery();
        var selected = picker.Roll(index, query, AvoidRepeats.IsChecked == true);
        if (selected is null)
        {
            ResultStatus.Text = "No eligible commanders match these filters. Clear colors or search.";
            return;
        }
        var filtered = index.Filter(query);
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
