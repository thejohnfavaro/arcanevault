using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ArcaneVault.Next.Desktop;

/// <summary>Read-only match analytics. Never writes match or collection state.</summary>
internal sealed class NativeMatchPerformancePanel : UserControl
{
    private readonly ComboBox deckFilter = new() { Width = 260, Margin = new Thickness(0,0,12,0) };
    private readonly ComboBox rangeFilter = new() { Width = 120 };
    private readonly TextBlock headline = new() { FontSize = 19, FontWeight = FontWeights.SemiBold,
        Foreground = Brushes.White, Margin = new Thickness(0,12,0,6), TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock recent = new() { Foreground = Brushes.LightGray, Margin = new Thickness(0,0,0,12) };
    private readonly TextBlock coverage = new() { Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0,6,0,10) };
    private readonly Canvas chart = new() { Height = 95, MinWidth = 280, Background =
        new SolidColorBrush(Color.FromRgb(25,32,40)) };
    private readonly ListView formats = new() { Height = 130, Margin = new Thickness(0,8,0,0) };
    private IReadOnlyList<MatchPerformanceAnalytics.Entry> records =
        Array.Empty<MatchPerformanceAnalytics.Entry>();

    internal NativeMatchPerformancePanel()
    {
        var root = new StackPanel { Margin = new Thickness(10) };
        Content = root;
        root.Children.Add(new TextBlock { Text = "DECK PERFORMANCE", FontSize = 18,
            FontWeight = FontWeights.Bold, Foreground = Brushes.White,
            Margin = new Thickness(0,0,0,8) });
        var controls = new WrapPanel { Orientation = Orientation.Horizontal };
        controls.Children.Add(deckFilter);
        rangeFilter.ItemsSource = new[] { "Last 30 days", "Last 90 days", "All time" };
        rangeFilter.SelectedIndex = 2;
        controls.Children.Add(rangeFilter);
        root.Children.Add(controls);
        root.Children.Add(headline);
        root.Children.Add(recent);
        root.Children.Add(new TextBlock { Text = "Rolling 10-match win rate (last 20 confirmed results)",
            Foreground = Brushes.LightGray, Margin = new Thickness(0,0,0,5) });
        root.Children.Add(chart);
        root.Children.Add(coverage);
        root.Children.Add(new TextBlock { Text = "BY FORMAT", FontSize = 13,
            FontWeight = FontWeights.SemiBold, Foreground = Brushes.White });
        var view = new GridView();
        view.Columns.Add(new GridViewColumn { Header = "Format", Width = 240,
            DisplayMemberBinding = new System.Windows.Data.Binding("Format") });
        view.Columns.Add(new GridViewColumn { Header = "Matches", Width = 85,
            DisplayMemberBinding = new System.Windows.Data.Binding("Matches") });
        view.Columns.Add(new GridViewColumn { Header = "W–L", Width = 85,
            DisplayMemberBinding = new System.Windows.Data.Binding("Record") });
        view.Columns.Add(new GridViewColumn { Header = "Win %", Width = 90,
            DisplayMemberBinding = new System.Windows.Data.Binding("WinRate") });
        formats.View = view;
        root.Children.Add(formats);
        deckFilter.SelectionChanged += (_,_) => Refresh();
        rangeFilter.SelectionChanged += (_,_) => Refresh();
        SizeChanged += (_,_) => DrawTrend(lastTrend);
    }

    private IReadOnlyList<MatchPerformanceAnalytics.Point> lastTrend =
        Array.Empty<MatchPerformanceAnalytics.Point>();

    internal void SetMatches(IEnumerable<MatchPerformanceAnalytics.Entry> matches)
    {
        var selected = deckFilter.SelectedItem as string;
        records = matches.ToArray();
        var choices = new[] { "All assigned decks" }.Concat(
            records.Where(x => !string.IsNullOrWhiteSpace(x.DeckName))
                .Select(x => x.DeckName.Trim()).Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)).ToArray();
        deckFilter.ItemsSource = choices;
        deckFilter.SelectedItem = choices.Contains(selected, StringComparer.OrdinalIgnoreCase)
            ? choices.First(x => x.Equals(selected, StringComparison.OrdinalIgnoreCase))
            : choices[0];
        Refresh();
    }
    private void Refresh()
    {
        if (deckFilter.SelectedItem is not string name) return;
        string? selected = name == "All assigned decks" ? null : name;
        int days = rangeFilter.SelectedIndex switch { 0 => 30, 1 => 90, _ => 0 };
        var report = MatchPerformanceAnalytics.Build(records, selected, days,
            DateTimeOffset.Now);
        headline.Text = report.Matches == 0
            ? "No confirmed wins or losses for this selection"
            : $"{report.Wins} W  /  {report.Losses} L     •     {report.Rate:0.#}% win rate";
        recent.Text = report.Matches == 0 ? "No recorded results to chart." :
            $"Last {Math.Min(report.Matches,10)}: {report.LastTenWins} W – {report.LastTenLosses} L" +
            (report.CurrentStreak > 0 ? $"     •     {report.CurrentStreak} {report.StreakResult} streak" : "");
        coverage.Text = report.Unresolved > 0
            ? $"{report.Unresolved} assigned match(es) have an unknown or unconfirmed result and are excluded from win rate."
            : "Only recorded, assigned wins and losses are counted. Unassigned matches are excluded.";
        formats.ItemsSource = report.Formats;
        lastTrend = report.Trend;
        DrawTrend(lastTrend);
    }
    private void DrawTrend(IReadOnlyList<MatchPerformanceAnalytics.Point> trend)
    {
        chart.Children.Clear();
        if (trend.Count < 2) return;
        double width = Math.Max(280, ActualWidth - 30);
        double height = 95;
        var horizontal = new Line { X1=0, Y1=height/2, X2=width, Y2=height/2,
            Stroke = Brushes.Gray, StrokeThickness = 0.5, Opacity = 0.4 };
        chart.Children.Add(horizontal);
        var curve = new Polyline { Stroke = Brushes.Orange, StrokeThickness = 3 };
        for (int i = 0; i < trend.Count; i++)
            curve.Points.Add(new Point(5 + (width-10) * i/(trend.Count-1),
                5 + (height-10) * (100-trend[i].Rate)/100));
        chart.Children.Add(curve);
    }
}
