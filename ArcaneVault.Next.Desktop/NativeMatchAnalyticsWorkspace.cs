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
        FontSize = 13, Margin = new Thickness(0,5,0,12) };
    private readonly TextBlock playDrawStatus = new() {
        Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap,
        FontSize = 13, Margin = new Thickness(0,5,0,12) };
    private readonly TextBlock gameplayStatus = new() {
        Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap,
        FontSize = 13, Margin = new Thickness(0,5,0,12) };
    private readonly ComboBox deckScope = new() {
        MinWidth = 260, MaxWidth = 380, HorizontalAlignment = HorizontalAlignment.Left,
        Margin = new Thickness(0,5,0,12), Padding = new Thickness(9,6,9,6) };
    private readonly ListView telemetryRows = new() {
        Height = 190, Margin = new Thickness(0,4,0,12),
        HorizontalContentAlignment = HorizontalAlignment.Stretch };
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
        // One page-level scroller: the analytics header is no longer fixed
        // above a cramped dashboard viewport at ordinary window heights.
        var root = new StackPanel { Margin = new Thickness(24,18,24,18) };
        Content = new ScrollViewer {
            Content = root,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        var header = new StackPanel();
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
        header.Children.Add(new TextBlock { Text = "DECK SCOPE",
            Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, FontSize = 14,
            Margin = new Thickness(0,8,0,3) });
        header.Children.Add(deckScope);
        deckScope.SelectionChanged += OnScopedDeckSelectionChanged;

        // Put the most useful high-level evidence side by side on wide screens.
        var summaries = new Grid { Margin = new Thickness(0,4,0,10) };
        summaries.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        summaries.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var gameplayColumn = new StackPanel { Margin = new Thickness(0,0,20,0) };
        var resultsColumn = new StackPanel { Margin = new Thickness(20,0,0,0) };
        Grid.SetColumn(gameplayColumn, 0);
        Grid.SetColumn(resultsColumn, 1);
        summaries.Children.Add(gameplayColumn);
        summaries.Children.Add(resultsColumn);
        header.Children.Add(summaries);
        gameplayColumn.Children.Add(new TextBlock { Text = "GAMEPLAY TELEMETRY COVERAGE",
            Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, FontSize = 14,
            Margin = new Thickness(0,3,0,3) });
        gameplayColumn.Children.Add(telemetryStatus);
        gameplayColumn.Children.Add(new TextBlock { Text = "OBSERVED GAMEPLAY",
            Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, FontSize = 14,
            Margin = new Thickness(0,3,0,3) });
        gameplayColumn.Children.Add(gameplayStatus);
        resultsColumn.Children.Add(new TextBlock { Text = "VERIFIED PLAY / DRAW",
            Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, FontSize = 14,
            Margin = new Thickness(0,3,0,3) });
        resultsColumn.Children.Add(playDrawStatus);
        header.Children.Add(new TextBlock { Text = "RECENT GAMEPLAY OBSERVATIONS",
            Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, FontSize = 14,
            Margin = new Thickness(0,5,0,3) });
        var telemetryGrid = new GridView();
        telemetryGrid.Columns.Add(TelemetryColumn("Played (UTC)", "PlayedUtc", 175));
        telemetryGrid.Columns.Add(TelemetryColumn("Deck", "DeckName", 170));
        telemetryGrid.Columns.Add(TelemetryColumn("Format", "Format", 140));
        telemetryGrid.Columns.Add(TelemetryColumn("Events", "Events", 70));
        telemetryGrid.Columns.Add(TelemetryColumn("Games observed", "GamesWithNumbers", 115));
        telemetryGrid.Columns.Add(TelemetryColumn("Highest turn", "HighestTurn", 105));
        telemetryRows.View = telemetryGrid;
        header.Children.Add(telemetryRows);
        dashboard.Margin = new Thickness(0,16,0,8);
        root.Children.Add(dashboard);
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

    private sealed record SavedDeckOption(string Id, string Name)
    {
        public override string ToString() => Name;
    }

    private async void OnScopedDeckSelectionChanged(object sender,
        SelectionChangedEventArgs e) => await ReloadAsync();

    internal async Task ReloadAsync()
    {
        if (reading) return;
        reading=true;
        refresh.IsEnabled=false;
        status.Text="Reading locally recorded Arena matches…";
        try
        {
            string selectedDeckId = (deckScope.SelectedItem as SavedDeckOption)?.Id ?? "";
            var data = await Task.Run(() => {
                var allMatches = NativeMatchHistoryView.ReadPerformance();
                var savedDecks = new List<SavedDeckOption> {
                    new("", "All Decks — aggregate") };
                var knownDecks = new List<MatchDeckAttribution.Deck>();
                if (File.Exists(ArcaneVault.Next.Collection.NativeWorkspace.DefaultPath))
                {
                    try
                    {
                        var rows = ArcaneVault.Next.Collection.NativeWorkspaceSafety.ListDecks(
                            ArcaneVault.Next.Collection.NativeWorkspace.DefaultPath)
                            .Where(d => !string.IsNullOrWhiteSpace(d.Id)).ToArray();
                        savedDecks.AddRange(rows
                            .Select(d => new SavedDeckOption(d.Id, d.Name))
                            .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase));
                        knownDecks.AddRange(rows.Select(d =>
                            new MatchDeckAttribution.Deck(d.Id, d.Name, d.ArenaDeckId)));
                    }
                    catch (Exception ex) when (ex is IOException or SqliteException or
                        InvalidDataException or UnauthorizedAccessException or ArgumentException) { }
                }
                using var history = new MatchHistory();
                // Reuse the previously validated, read-only match attribution
                // rules. A name alone cannot override an existing native ID or
                // a conflicting Arena ID mapping. Ambiguous IDs stay excluded.
                var candidateMappings = history.Mappings()
                    .Select(m => (Arena: m.ArenaId, Native: m.NativeId))
                    .Concat(knownDecks.Where(d => !string.IsNullOrWhiteSpace(d.ArenaId))
                        .Select(d => (Arena: d.ArenaId, Native: d.Id)))
                    .Where(m => !string.IsNullOrWhiteSpace(m.Arena) &&
                        !string.IsNullOrWhiteSpace(m.Native))
                    .GroupBy(m => m.Arena.Trim(), StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                var blocked = candidateMappings
                    .Where(g => g.Select(m => m.Native).Distinct(StringComparer.Ordinal).Count() != 1)
                    .Select(g => g.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var approved = candidateMappings.Where(g => !blocked.Contains(g.Key))
                    .ToDictionary(g => g.Key, g => g.First().Native,
                        StringComparer.OrdinalIgnoreCase);
                string? Resolve(MatchPerformanceAnalytics.Entry m) =>
                    MatchDeckAttribution.Resolve(new MatchDeckAttribution.Match(
                        m.MatchId, m.DeckName, m.ArenaDeckId, m.NativeDeckId,
                        m.AttributionNote), knownDecks, approved, blocked);
                var matches = selectedDeckId.Length == 0 ? allMatches :
                    allMatches.Where(m => Resolve(m) == selectedDeckId).ToArray();
                IReadOnlySet<string>? selectedMatchIds = selectedDeckId.Length == 0 ? null :
                    matches.Select(m => m.MatchId).ToHashSet(StringComparer.Ordinal);
                return (matches, savedDecks,
                    recovered: selectedDeckId.Length == 0 ? 0 :
                        matches.Count(m => !m.NativeDeckId.Equals(selectedDeckId,
                            StringComparison.Ordinal)),
                    coverage: history.ReadTelemetryOverviewForDeck(null, selectedMatchIds),
                    gameplay: history.ReadGameplayObservationForDeck(null, selectedMatchIds),
                    playDraw: history.ReadVerifiedPlayDrawForDeck(null, selectedMatchIds));
            });
            dashboard.SetMatches(data.matches,
                singleDeck: selectedDeckId.Length > 0);
            // Preserve the current deck by stable ID, not by row position or
            // name. A missing exact ID means no verified deck-scoped totals.
            deckScope.SelectionChanged -= OnScopedDeckSelectionChanged;
            deckScope.ItemsSource = data.savedDecks;
            deckScope.SelectedItem = data.savedDecks.FirstOrDefault(d => d.Id == selectedDeckId)
                ?? data.savedDecks[0];
            deckScope.SelectionChanged += OnScopedDeckSelectionChanged;
            string scopeLabel = data.savedDecks.FirstOrDefault(d => d.Id == selectedDeckId)?.Name
                ?? "All Decks — aggregate";
            status.Text = $"{scopeLabel}: {data.matches.Count} recorded match row(s). " +
                (selectedDeckId.Length == 0
                    ? "Aggregate across assigned decks; unconfirmed outcomes are excluded from W/L totals."
                    : $"{data.recovered} historical match(es) included using verified Arena mappings " +
                      "or unambiguous recorded assignments. Conflicting and unverified entries remain excluded.");
            var coverage = data.coverage;
            telemetryStatus.Text = $"{scopeLabel}: {coverage.MatchesWithTelemetry} of {coverage.RecordedMatches} recorded matches " +
                $"have GRE observations ({coverage.ArchivedEvents} retained events). " +
                "Only events linked to an explicitly identified match are archived. " +
                "Game counts and highest turns are observed lower bounds, not complete game replays. " +
                "The table shows the 20 most recent matches with telemetry.";
            telemetryRows.ItemsSource = coverage.Recent;
            var playDraw = data.playDraw;
            string Rate(int wins,int losses) => wins+losses == 0 ? "—" :
                $"{100d*wins/(wins+losses):0.#}%";
            playDrawStatus.Text = $"{scopeLabel} · On play: {playDraw.OnPlayWins} W / {playDraw.OnPlayLosses} L " +
                $"({Rate(playDraw.OnPlayWins,playDraw.OnPlayLosses)}). " +
                $"On draw: {playDraw.OnDrawWins} W / {playDraw.OnDrawLosses} L " +
                $"({Rate(playDraw.OnDrawWins,playDraw.OnDrawLosses)}). " +
                $"{playDraw.Unattributed} recorded results excluded (missing or ambiguous evidence). " +
                "Only single-game matches with an authenticated room seat and an explicit " +
                "turn-one active seat are attributed; multi-game matches are excluded.";
            var gameplay = data.gameplay;
            gameplayStatus.Text = gameplay.GamesWithTurnData == 0
                ? "No game-numbered turn observations yet. Enable Arena Detailed Logs to collect future evidence."
                : $"Highest observed turn per game: {gameplay.AverageHighestObservedTurn:0.#} average " +
                  $"across {gameplay.GamesWithTurnData} games with turn data. " +
                  $"Mulligan data observed in {gameplay.GamesWithMulliganData} games; " +
                  $"{gameplay.SeatsWithMulligans} game/seat observations show at least one mulligan. " +
                  $"Highest observed seat mulligan count: " +
                  (gameplay.HighestObservedMulligans?.ToString() ?? "unknown") + ". " +
                  "These are partial, seat-anonymous observations, not final game lengths " +
                  "or player-specific mulligan rates. Only the latest 10,000 telemetry events " +
                  "are scanned for mulligan statistics; play/draw attribution remains unavailable.";
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
