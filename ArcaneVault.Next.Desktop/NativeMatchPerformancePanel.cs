using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ArcaneVault.Next.Desktop;

/// <summary>Read-only, recorded-match performance and deck comparison.</summary>
internal sealed class NativeMatchPerformancePanel : UserControl
{
    private const string AllDecks = "All assigned decks";
    private const string NoComparison = "No comparison";
    private const string AllFormats = "All formats";
    private readonly ComboBox deckFilter = new() { Width = 215, Margin = new Thickness(0,0,8,5) };
    private readonly ComboBox compareFilter = new() { Width = 215, Margin = new Thickness(0,0,8,5) };
    private readonly ComboBox formatFilter = new() { Width = 190, Margin = new Thickness(0,0,8,5) };
    private readonly ComboBox rangeFilter = new() { Width = 125, Margin = new Thickness(0,0,8,5) };
    private readonly TextBlock headline = new() { FontSize = 18, FontWeight = FontWeights.SemiBold,
        Foreground = Brushes.White, Margin = new Thickness(0,10,0,6), TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock comparison = new() { Foreground = Brushes.LightGray,
        TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,8) };
    private readonly TextBlock recent = new() { Foreground = Brushes.LightGray,
        TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,10) };
    private readonly TextBlock coverage = new() { Foreground = Brushes.LightGray,
        TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,6,0,10) };
    private readonly Canvas chart = new() { Height = 110, MinWidth = 280,
        Background = new SolidColorBrush(Color.FromRgb(25,32,40)) };
    private readonly ListView formats = new() { Height = 145, Margin = new Thickness(0,6,0,10) };
    private readonly ListView timeline = new() { Height = 190, Margin = new Thickness(0,6,0,0) };
    private readonly ListView weeks = new() { Height = 154, Margin = new Thickness(0,6,0,10) };
    private readonly TextBlock streaks = new() { Foreground = Brushes.LightGray,
        TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,2,0,8) };
    private IReadOnlyList<MatchPerformanceAnalytics.Entry> records =
        Array.Empty<MatchPerformanceAnalytics.Entry>();
    private IReadOnlyList<MatchPerformanceAnalytics.Point> primaryTrend =
        Array.Empty<MatchPerformanceAnalytics.Point>();
    private IReadOnlyList<MatchPerformanceAnalytics.Point> secondaryTrend =
        Array.Empty<MatchPerformanceAnalytics.Point>();
    private bool changingChoices;

    internal NativeMatchPerformancePanel()
    {
        var root = new StackPanel { Margin = new Thickness(10) };
        Content = root;
        root.Children.Add(new TextBlock { Text = "DECK PERFORMANCE", FontSize = 18,
            FontWeight = FontWeights.Bold, Foreground = Brushes.White,
            Margin = new Thickness(0,0,0,8) });
        root.Children.Add(new TextBlock { Text = "Primary deck  •  Compare with  •  Format  •  Period",
            Foreground = Brushes.LightGray, Margin = new Thickness(0,0,0,4) });
        var controls = new WrapPanel();
        controls.Children.Add(deckFilter);
        controls.Children.Add(compareFilter);
        controls.Children.Add(formatFilter);
        rangeFilter.ItemsSource = new[] { "Last 30 days", "Last 90 days", "All time" };
        rangeFilter.SelectedIndex = 2;
        controls.Children.Add(rangeFilter);
        root.Children.Add(controls);
        root.Children.Add(headline);
        root.Children.Add(comparison);
        root.Children.Add(recent);
        root.Children.Add(streaks);
        root.Children.Add(new TextBlock {
            Text = "Rolling 10-match win rate • last 20 confirmed matches per deck",
            Foreground = Brushes.LightGray, Margin = new Thickness(0,0,0,5) });
        root.Children.Add(chart);
        root.Children.Add(new TextBlock {
            Text = "Orange: primary deck     •     Blue: comparison deck",
            Foreground = Brushes.LightGray, Margin = new Thickness(0,5,0,0) });
        root.Children.Add(coverage);
        root.Children.Add(new TextBlock { Text = "BY FORMAT", FontSize = 13,
            FontWeight = FontWeights.SemiBold, Foreground = Brushes.White });
        var formatView = new GridView();
        formatView.Columns.Add(Column("Format", "Format", 220));
        formatView.Columns.Add(Column("Matches", "Matches", 75));
        formatView.Columns.Add(Column("W–L", "Record", 75));
        formatView.Columns.Add(Column("Win %", "WinRate", 80));
        formats.View = formatView;
        root.Children.Add(formats);
        root.Children.Add(new TextBlock { Text = "WEEKLY PERFORMANCE (UTC)", FontSize = 13,
            FontWeight = FontWeights.SemiBold, Foreground = Brushes.White });
        var weekView = new GridView();
        weekView.Columns.Add(Column("Week of", "Week", 140));
        weekView.Columns.Add(Column("Matches", "Matches", 80));
        weekView.Columns.Add(Column("W–L", "Record", 80));
        weekView.Columns.Add(Column("Win %", "WinRate", 90));
        weeks.View = weekView;
        root.Children.Add(weeks);
        root.Children.Add(new TextBlock { Text = "RECENT CONFIRMED MATCHES",
            FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White });
        var timelineView = new GridView();
        timelineView.Columns.Add(Column("When", "When", 140));
        timelineView.Columns.Add(Column("Deck", "Deck", 140));
        timelineView.Columns.Add(Column("Format", "Format", 150));
        timelineView.Columns.Add(Column("Result", "Result", 70));
        timelineView.Columns.Add(Column("Rolling win %", "RollingRate", 110));
        timeline.View = timelineView;
        root.Children.Add(timeline);
        deckFilter.SelectionChanged += (_,_) => Refresh();
        compareFilter.SelectionChanged += (_,_) => Refresh();
        formatFilter.SelectionChanged += (_,_) => Refresh();
        rangeFilter.SelectionChanged += (_,_) => Refresh();
        SizeChanged += (_,_) => DrawTrend();
    }

    private static GridViewColumn Column(string label, string key, double width) =>
        new() { Header = label, Width = width,
            DisplayMemberBinding = new System.Windows.Data.Binding(key) };

    internal void SetMatches(IEnumerable<MatchPerformanceAnalytics.Entry> matches)
    {
        var priorDeck = deckFilter.SelectedItem as string;
        var priorCompare = compareFilter.SelectedItem as string;
        var priorFormat = formatFilter.SelectedItem as string;
        records = matches.ToArray();
        changingChoices = true;
        try
        {
            var names = records.Where(x => !string.IsNullOrWhiteSpace(x.DeckName))
                .Select(x => x.DeckName.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
            var decks = new[] { AllDecks }.Concat(names).ToArray();
            deckFilter.ItemsSource = decks;
            deckFilter.SelectedItem = Find(decks, priorDeck) ?? decks[0];
            var compares = new[] { NoComparison }.Concat(names).ToArray();
            compareFilter.ItemsSource = compares;
            compareFilter.SelectedItem = Find(compares, priorCompare) ?? compares[0];
            var formats = new[] { AllFormats }.Concat(records
                .Where(x => !string.IsNullOrWhiteSpace(x.DeckName))
                .Select(x => string.IsNullOrWhiteSpace(x.Format) ? "Unknown format" : x.Format.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)).ToArray();
            formatFilter.ItemsSource = formats;
            formatFilter.SelectedItem = Find(formats, priorFormat) ?? formats[0];
        }
        finally { changingChoices = false; }
        Refresh();
    }
    private static string? Find(IEnumerable<string> choices, string? value) =>
        choices.FirstOrDefault(x => x.Equals(value, StringComparison.OrdinalIgnoreCase));

    private void Refresh()
    {
        if (changingChoices || deckFilter.SelectedItem is not string primary) return;
        int days = rangeFilter.SelectedIndex switch { 0 => 30, 1 => 90, _ => 0 };
        string? format = formatFilter.SelectedItem is string f && f != AllFormats ? f : null;
        string? selected = primary == AllDecks ? null : primary;
        var report = MatchPerformanceAnalytics.Build(records, selected, days,
            DateTimeOffset.Now, format);
        headline.Text = report.Matches == 0
            ? "No confirmed matches for this selection"
            : $"{primary}: {report.Wins} W / {report.Losses} L  •  {report.Rate:0.#}% win rate";
        recent.Text = report.Matches == 0 ? "No recorded results to chart." :
            $"Last {Math.Min(report.Matches,10)}: {report.LastTenWins} W – {report.LastTenLosses} L" +
            (report.CurrentStreak > 0 ? $"  •  {report.CurrentStreak} {report.StreakResult} streak" : "");
        var compareName = compareFilter.SelectedItem as string;
        bool hasComparison = !string.IsNullOrWhiteSpace(compareName) &&
            compareName != NoComparison && !compareName.Equals(primary, StringComparison.OrdinalIgnoreCase);
        var other = hasComparison
            ? MatchPerformanceAnalytics.Build(records, compareName, days, DateTimeOffset.Now, format)
            : null;
        comparison.Text = other is null ? "Select a second deck to compare win rates." :
            $"{compareName}: {other.Wins} W / {other.Losses} L • {other.Rate:0.#}% win rate" +
            $"    |    Difference: {report.Rate - other.Rate:+0.#;-0.#;0} percentage points";
        coverage.Text = $"{report.Unresolved} assigned primary match(es) have unknown results (excluded)." +
            (other is null ? "" : $" Comparison: {other.Unresolved} excluded.") +
            " Unassigned matches are never included.";
        formats.ItemsSource = report.Formats;
        weeks.ItemsSource = report.Weeks;
        streaks.Text = $"Longest winning streak: {report.BestWinStreak}    •    Longest losing streak: {report.WorstLossStreak}" +
            (report.Matches < 10 ? $"    •    Small sample ({report.Matches} confirmed matches)" : "");
        timeline.ItemsSource = report.Timeline;
        primaryTrend = report.Trend;
        secondaryTrend = other?.Trend ?? Array.Empty<MatchPerformanceAnalytics.Point>();
        DrawTrend();
    }

    private void DrawTrend()
    {
        chart.Children.Clear();
        double width = Math.Max(280, ActualWidth - 30);
        const double height = 110;
        chart.Children.Add(new Line { X1=0, Y1=height/2, X2=width, Y2=height/2,
            Stroke = Brushes.Gray, StrokeThickness = 0.5, Opacity = 0.4 });
        AddLine(primaryTrend, width, height, Brushes.Orange);
        AddLine(secondaryTrend, width, height, Brushes.CornflowerBlue);
    }
    private void AddLine(IReadOnlyList<MatchPerformanceAnalytics.Point> trend,
        double width, double height, Brush color)
    {
        if (trend.Count < 2) return;
        var line = new Polyline { Stroke = color, StrokeThickness = 3 };
        for (int i = 0; i < trend.Count; i++)
            line.Points.Add(new Point(5 + (width-10) * i/(trend.Count-1),
                5 + (height-10) * (100-trend[i].Rate)/100));
        chart.Children.Add(line);
    }
}
