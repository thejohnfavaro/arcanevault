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
    private readonly TextBlock playDrawStatus = new() {
        Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0,5,0,10) };
    private readonly TextBlock gameplayStatus = new() {
        Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0,5,0,10) };
    private readonly ComboBox deckScope = new() {
        MinWidth = 230, Margin = new Thickness(0,4,0,7) };
    private readonly TextBlock scopedPlayDrawStatus = new() {
        Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0,0,0,10) };
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
    private static IReadOnlyDictionary<string,string> ReadSavedDeckColors()
    {
        var result = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(ArcaneVault.Next.Collection.NativeWorkspace.DefaultPath) ||
            !File.Exists(ArcaneVault.Next.Collection.NativeWorkspace.CatalogPath))
            return result;
        var decks = ArcaneVault.Next.Collection.NativeWorkspaceSafety.ListDecks(
            ArcaneVault.Next.Collection.NativeWorkspace.DefaultPath);
        using var db = new SqliteConnection(new SqliteConnectionStringBuilder {
            DataSource = ArcaneVault.Next.Collection.NativeWorkspace.CatalogPath,
            Mode = SqliteOpenMode.ReadOnly, Pooling = false
        }.ToString());
        db.Open();
        using var query = db.CreateCommand();
        query.CommandText = @"SELECT colors FROM cards WHERE name=$name COLLATE NOCASE
            OR name LIKE $front ESCAPE '\' COLLATE NOCASE
            ORDER BY CASE WHEN name=$name COLLATE NOCASE THEN 0 ELSE 1 END LIMIT 1";
        var exact = query.Parameters.AddWithValue("$name","");
        var front = query.Parameters.AddWithValue("$front","");
        var colorByDeck = new List<(string Id,string Name,string Colors)>();
        foreach (var deck in decks.Where(d => !string.IsNullOrWhiteSpace(d.Commander)))
        {
            exact.Value = deck.Commander.Trim();
            string escape = deck.Commander.Trim().Replace(@"\",@"\\")
                .Replace("%",@"\%").Replace("_",@"\_");
            front.Value = escape + " // %";
            var raw = query.ExecuteScalar()?.ToString();
            if (raw is null) continue; // Unknown is not colorless.
            string colors;
            try
            {
                colors = ArcaneVault.Next.Domain.EdhLabCommanderRules.CanonicalColors(raw);
                if (colors.Length == 0) colors = "C";
            }
            catch (ArgumentException) { continue; }
            colorByDeck.Add((deck.Id,deck.Name,colors));
            result[deck.Id] = colors;
        }
        // Name-only historical assignments are linked only if exactly one
        // saved deck has that name. Never guess between duplicate deck names.
        var nameCounts = decks.GroupBy(x => x.Name,
            StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => x.Count(),
                StringComparer.OrdinalIgnoreCase);
        foreach (var item in colorByDeck)
            if (nameCounts.GetValueOrDefault(item.Name) == 1)
                result["name:" + item.Name] = item.Colors;
        return result;
    }

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
        header.Children.Add(new TextBlock { Text = "OBSERVED GAMEPLAY",
            Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, FontSize = 14,
            Margin = new Thickness(0,3,0,3) });
        header.Children.Add(gameplayStatus);
        header.Children.Add(new TextBlock { Text = "VERIFIED PLAY / DRAW",
            Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, FontSize = 14,
            Margin = new Thickness(0,3,0,3) });
        header.Children.Add(playDrawStatus);
        header.Children.Add(new TextBlock { Text = "SAVED DECK PLAY / DRAW",
            Foreground = Brushes.White, FontWeight = FontWeights.SemiBold,
            FontSize = 14, Margin = new Thickness(0,3,0,3) });
        header.Children.Add(deckScope);
        header.Children.Add(scopedPlayDrawStatus);
        deckScope.SelectionChanged += OnScopedDeckSelectionChanged;
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
                var matches = NativeMatchHistoryView.ReadPerformance();
                var savedDecks = new List<SavedDeckOption>();
                if (File.Exists(ArcaneVault.Next.Collection.NativeWorkspace.DefaultPath))
                {
                    try
                    {
                        savedDecks = ArcaneVault.Next.Collection.NativeWorkspaceSafety.ListDecks(
                            ArcaneVault.Next.Collection.NativeWorkspace.DefaultPath)
                            .Where(d => !string.IsNullOrWhiteSpace(d.Id))
                            .Select(d => new SavedDeckOption(d.Id, d.Name))
                            .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase).ToList();
                    }
                    catch (Exception ex) when (ex is IOException or SqliteException or
                        InvalidDataException or UnauthorizedAccessException or ArgumentException) { }
                }
                using var history = new MatchHistory();
                IReadOnlyDictionary<string,string> identities;
                try { identities = ReadSavedDeckColors(); }
                catch (Exception ex) when (ex is IOException or SqliteException or
                    InvalidDataException or UnauthorizedAccessException or ArgumentException)
                {
                    identities = new Dictionary<string,string>();
                }
                return (matches, identities, savedDecks,
                    scoped: selectedDeckId.Length == 0 ? null :
                        history.ReadVerifiedPlayDrawForDeck(selectedDeckId),
                    coverage: history.ReadTelemetryOverview(),
                    gameplay: history.ReadGameplayObservation(),
                    playDraw: history.ReadVerifiedPlayDraw());
            });
            dashboard.SetMatches(data.matches, data.identities);
            // Preserve the current deck by stable ID, not by row position or
            // name. A missing exact ID means no verified deck-scoped totals.
            deckScope.SelectionChanged -= OnScopedDeckSelectionChanged;
            deckScope.ItemsSource = data.savedDecks;
            deckScope.SelectedItem = data.savedDecks.FirstOrDefault(d => d.Id == selectedDeckId);
            deckScope.SelectionChanged += OnScopedDeckSelectionChanged;
            status.Text=$"{data.matches.Count} distinct match row(s) available for review. " +
                "All calculations exclude unassigned rows and unconfirmed outcomes.";
            var coverage = data.coverage;
            telemetryStatus.Text = $"{coverage.MatchesWithTelemetry} of {coverage.RecordedMatches} recorded matches " +
                $"have GRE observations ({coverage.ArchivedEvents} retained events). " +
                "Only events linked to an explicitly identified match are archived. " +
                "Game counts and highest turns are observed lower bounds, not complete game replays. " +
                "The table shows the 20 most recent matches with telemetry.";
            telemetryRows.ItemsSource = coverage.Recent;
            var playDraw = data.playDraw;
            string Rate(int wins,int losses) => wins+losses == 0 ? "—" :
                $"{100d*wins/(wins+losses):0.#}%";
            playDrawStatus.Text = $"On play: {playDraw.OnPlayWins} W / {playDraw.OnPlayLosses} L " +
                $"({Rate(playDraw.OnPlayWins,playDraw.OnPlayLosses)}). " +
                $"On draw: {playDraw.OnDrawWins} W / {playDraw.OnDrawLosses} L " +
                $"({Rate(playDraw.OnDrawWins,playDraw.OnDrawLosses)}). " +
                $"{playDraw.Unattributed} recorded results excluded (missing or ambiguous evidence). " +
                "Only single-game matches with an authenticated room seat and an explicit " +
                "turn-one active seat are attributed; multi-game matches are excluded.";
            var scoped = data.scoped;
            scopedPlayDrawStatus.Text = selectedDeckId.Length == 0
                ? "Choose a saved deck to view verified play/draw results. No deck is selected."
                : scoped is null
                    ? "No saved deck identity available."
                    : $"Selected deck: {data.savedDecks.FirstOrDefault(d => d.Id == selectedDeckId)?.Name ?? selectedDeckId}. " +
                      $"On play: {scoped.OnPlayWins} W / {scoped.OnPlayLosses} L " +
                      $"({Rate(scoped.OnPlayWins,scoped.OnPlayLosses)}). " +
                      $"On draw: {scoped.OnDrawWins} W / {scoped.OnDrawLosses} L " +
                      $"({Rate(scoped.OnDrawWins,scoped.OnDrawLosses)}). " +
                      $"{scoped.Unattributed} results lack sufficient seat/turn evidence. " +
                      "Only exact recorded native deck IDs count here; missing or ambiguous records are excluded.";
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
