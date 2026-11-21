using Microsoft.Data.Sqlite;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Diagnostics;

namespace ArcaneVault.Next.Desktop;

/// <summary>Dedicated read-only analytics workspace, separate from match corrections.</summary>
internal sealed class NativeMatchAnalyticsWorkspace : UserControl
{
    private readonly NativeMatchPerformancePanel dashboard = new();
    private readonly TextBlock status = new() { Foreground = Brushes.LightGray,
        TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,8,0,12) };
    private readonly TextBlock telemetryStatus = new() {
        Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0,5,0,8) };
    private readonly ListView telemetryRows = new() {
        Height = 180, Margin = new Thickness(0,0,0,12) };
    private readonly Button refresh = new() { Content = "Refresh match analytics",
        Padding = new Thickness(12,8,12,8), HorizontalAlignment = HorizontalAlignment.Left };
    private readonly TextBlock seasonStatus = new() {
        Foreground = Brushes.Wheat, TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0,9,0,5) };
    private readonly DispatcherTimer countdownTimer = new() {
        Interval = TimeSpan.FromMinutes(1) };
    private bool reading;
    internal NativeMatchAnalyticsWorkspace()
    {
        var root = new DockPanel { Margin = new Thickness(24,18,24,18) };
        Content = root;
        var header = new StackPanel();
        DockPanel.SetDock(header,Dock.Top);
        root.Children.Add(header);
        header.Children.Add(new TextBlock { Text = "DECK ANALYTICS",
            Foreground = Brushes.White, FontWeight = FontWeights.Bold, FontSize = 27 });
        header.Children.Add(new TextBlock {
            Text = "Confirmed Arena performance by deck, queue and date. " +
                   "Comparisons use recorded outcomes only; corrections belong in Match History.",
            Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0,5,0,8) });
        header.Children.Add(refresh);
        header.Children.Add(seasonStatus);
        var official = new Button { Content = "Wizards official ranked schedule",
            Padding = new Thickness(10,5,10,5),
            HorizontalAlignment = HorizontalAlignment.Left };
        official.Click += (_,_) => Process.Start(new ProcessStartInfo(
            ArenaRankSeason.OfficialUrl){ UseShellExecute=true });
        header.Children.Add(official);
        header.Children.Add(status);
        header.Children.Add(new TextBlock { Text = "GAMEPLAY TELEMETRY COVERAGE",
            Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, FontSize = 14,
            Margin = new Thickness(0,3,0,3) });
        header.Children.Add(telemetryStatus);
        var telemetryGrid = new GridView();
        telemetryGrid.Columns.Add(TelemetryColumn("Played (UTC)", "PlayedUtc", 175));
        telemetryGrid.Columns.Add(TelemetryColumn("Deck", "DeckName", 170));
        telemetryGrid.Columns.Add(TelemetryColumn("Format", "Format", 140));
        telemetryGrid.Columns.Add(TelemetryColumn("Events", "Events", 70));
        telemetryGrid.Columns.Add(TelemetryColumn("Games observed", "GamesWithNumbers", 115));
        telemetryGrid.Columns.Add(TelemetryColumn("Highest turn", "HighestTurn", 105));
        telemetryRows.View = telemetryGrid;
        header.Children.Add(telemetryRows);
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        scroll.Content = dashboard;
        root.Children.Add(scroll);
        refresh.Click += async (_,_) => await ReloadAsync();
        countdownTimer.Tick += (_,_) => RenderSeason();
        Loaded += async (_,_) => {
            countdownTimer.Start();
            RenderSeason();
            await ReloadAsync();
        };
        Unloaded += (_,_) => countdownTimer.Stop();
    }
    private static GridViewColumn TelemetryColumn(string title, string property, double width) =>
        new() { Header = title, Width = width,
            DisplayMemberBinding = new System.Windows.Data.Binding(property) };

    private void RenderSeason()
    {
        var now=DateTimeOffset.UtcNow;
        var verified=ArenaRankSeason.Read(ArenaRankSeason.CachePath,now);
        if(verified is null || !DateTimeOffset.TryParse(verified.endsUtc,out var end) ||
            end<=now)
        {
            seasonStatus.Text="Ranked season: awaiting a newly published Wizards schedule. " +
                "Arcane Vault checks again automatically while running.";
            return;
        }
        var left=end-now;
        string countdown=$"{(int)left.TotalDays}d {left.Hours}h {left.Minutes}m";
        string reset="";
        try
        {
            using var history=new MatchHistory();
            var rank=history.LatestRank();
            if(rank is not null)
            {
                var constructed=ArenaRankSeason.Predicted(verified,
                    rank.ConstructedClass,rank.ConstructedLevel,now);
                var limited=ArenaRankSeason.Predicted(verified,
                    rank.LimitedClass,rank.LimitedLevel,now);
                if(constructed is not null || limited is not null)
                    reset="  ·  If you finish at your observed rank: Constructed → "+
                        (constructed??"not published")+", Limited → "+
                        (limited??"not published");
            }
        }
        catch(Exception ex) when(ex is IOException or SqliteException or
            InvalidOperationException or UnauthorizedAccessException) { }
        string when=end.ToLocalTime().ToString("MMM d, yyyy h:mm tt");
        var checkedText=DateTimeOffset.TryParse(verified.checkedUtc,out var checkedAt)
            ? checkedAt.ToLocalTime().ToString("MMM d h:mm tt")
            : "unavailable";
        seasonStatus.Text=$"{verified.season} ranked season ends {when} "+
            $"({countdown} remaining). Last verified: {checkedText}. "+
            "Checked automatically every 12 hours while Arcane Vault is running."+
            reset;
    }

    internal async Task ReloadAsync()
    {
        if (reading) return;
        reading=true;
        refresh.IsEnabled=false;
        status.Text="Reading locally recorded Arena matches…";
        try
        {
            var data = await Task.Run(() => {
                var matches = NativeMatchHistoryView.ReadPerformance();
                using var history = new MatchHistory();
                return (matches, coverage: history.ReadTelemetryOverview());
            });
            dashboard.SetMatches(data.matches);
            status.Text=$"{data.matches.Count} distinct match row(s) available for review. " +
                "All calculations exclude unassigned rows and unconfirmed outcomes.";
            var coverage = data.coverage;
            telemetryStatus.Text = $"{coverage.MatchesWithTelemetry} of {coverage.RecordedMatches} recorded matches " +
                $"have GRE observations ({coverage.ArchivedEvents} retained events). " +
                "Only events linked to an explicitly identified match are archived. " +
                "Game counts and highest turns are observed lower bounds, not complete game replays. " +
                "The table shows the 20 most recent matches with telemetry.";
            telemetryRows.ItemsSource = coverage.Recent;
        }
        catch(Exception ex) when (ex is IOException or SqliteException or
            UnauthorizedAccessException or InvalidOperationException or InvalidDataException)
        {
            status.Text="Analytics unavailable: "+ex.Message+
                ". Your match data has not been modified.";
        }
        finally { reading=false; refresh.IsEnabled=true; }
    }
}
