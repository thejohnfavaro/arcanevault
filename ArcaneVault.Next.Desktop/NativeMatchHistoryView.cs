using ArcaneVault.Next.Collection;
using Microsoft.Data.Sqlite;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// In-app match reconciliation. Reads the native/legacy Arena history without
/// modifying a card collection. An assignment updates ONLY an existing match
/// by its immutable match_id; it never inserts or guesses a match result.
/// </summary>
internal sealed class NativeMatchHistoryView : UserControl
{
    private sealed record MatchItem(string Database, string MatchId, string Played,
        string Result, string DeckName, string DeckId, string Source, string Note)
    {
        public string Date => DateTimeOffset.TryParse(Played, out var date)
            ? date.ToLocalTime().ToString("g") : Played;
        public string Status => string.IsNullOrWhiteSpace(DeckName)
            ? (Result == "unknown" ? "Review result & deck" : "Needs deck assignment")
            : (Result == "unknown" ? "Review result" : "Assigned");
        public string Deck => string.IsNullOrWhiteSpace(DeckName) ? "Unassigned" : DeckName;
        public string Origin => System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(Database));
    }

    private sealed record SavedDeck(string Id, string Name, string ArenaId)
    {
        public override string ToString() =>
            Name + (string.IsNullOrWhiteSpace(ArenaId) ? "" : " (Arena ID linked)");
    }

    private readonly TextBlock status = new() { Margin = new Thickness(0, 8, 0, 10),
        TextWrapping = TextWrapping.Wrap, Foreground = Brushes.LightGray };
    private readonly ListView rows = new() { MinHeight = 260, Margin = new Thickness(0, 6, 0, 14) };
    private readonly ComboBox deckPicker = new() { MinWidth = 260, Margin = new Thickness(0, 0, 10, 0),
        MaxWidth = 420 };
    private readonly ComboBox resultPicker = new() { MinWidth = 150,
        Margin = new Thickness(0, 0, 10, 0) };
    private readonly Button assign = new() { Content = "Assign selected match",
        MinWidth = 170, Padding = new Thickness(14, 9, 14, 9), IsEnabled = false };
    private readonly Button refresh = new() { Content = "Refresh",
        Padding = new Thickness(14, 9, 14, 9), Margin = new Thickness(0, 0, 10, 0) };
    private readonly CheckBox rememberMapping = new() {
        Content = "Remember this Arena deck ID for future matches",
        IsChecked = true, Margin = new Thickness(0, 8, 0, 0),
        Foreground = Brushes.LightGray
    };
    private readonly CheckBox needsReview = new() { Content = "Needs assignment / review only",
        IsChecked = true, VerticalAlignment = VerticalAlignment.Center };
    private IReadOnlyList<MatchItem> loaded = Array.Empty<MatchItem>();
    private bool busy;

    public NativeMatchHistoryView()
    {
        var root = new DockPanel { Margin = new Thickness(24, 18, 24, 18) };
        Content = root;
        var header = new StackPanel { Orientation = Orientation.Vertical };
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);
        header.Children.Add(new TextBlock { Text = "MATCH HISTORY",
            FontSize = 27, FontWeight = FontWeights.Bold, Foreground = Brushes.White });
        header.Children.Add(new TextBlock {
            Text = "Arena match results and deck assignments. Every correction updates an existing match; no wins are invented.",
            Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 0, 8) });
        var toolbar = new WrapPanel { Margin = new Thickness(0, 6, 0, 5) };
        toolbar.Children.Add(refresh);
        toolbar.Children.Add(needsReview);
        header.Children.Add(toolbar);
        header.Children.Add(status);

        var footer = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);
        footer.Children.Add(new TextBlock {
            Text = "Selected match: choose the saved deck you actually used. If the result is unknown, select the actual outcome explicitly.",
            Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8) });
        var actions = new WrapPanel();
        actions.Children.Add(deckPicker);
        resultPicker.ItemsSource = new [] { "Keep detected result", "Win", "Loss", "Draw", "Unknown" };
        resultPicker.SelectedIndex = 0;
        actions.Children.Add(resultPicker);
        actions.Children.Add(assign);
        footer.Children.Add(actions);
        footer.Children.Add(rememberMapping);

        var grid = new GridView();
        grid.Columns.Add(new GridViewColumn { Header = "When", Width = 145,
            DisplayMemberBinding = new System.Windows.Data.Binding(nameof(MatchItem.Date)) });
        grid.Columns.Add(new GridViewColumn { Header = "Result", Width = 90,
            DisplayMemberBinding = new System.Windows.Data.Binding(nameof(MatchItem.Result)) });
        grid.Columns.Add(new GridViewColumn { Header = "Deck", Width = 225,
            DisplayMemberBinding = new System.Windows.Data.Binding(nameof(MatchItem.Deck)) });
        grid.Columns.Add(new GridViewColumn { Header = "Status", Width = 170,
            DisplayMemberBinding = new System.Windows.Data.Binding(nameof(MatchItem.Status)) });
        grid.Columns.Add(new GridViewColumn { Header = "Origin", Width = 125,
            DisplayMemberBinding = new System.Windows.Data.Binding(nameof(MatchItem.Origin)) });
        rows.View = grid;
        root.Children.Add(rows);

        rows.SelectionChanged += (_, _) => UpdateSelection();
        deckPicker.SelectionChanged += (_, _) => UpdateSelection();
        needsReview.Checked += (_, _) => FilterRows();
        needsReview.Unchecked += (_, _) => FilterRows();
        refresh.Click += async (_, _) => await ReloadAsync();
        assign.Click += async (_, _) => await AssignAsync();
        Loaded += async (_, _) => { if (loaded.Count == 0) await ReloadAsync(); };
    }

    private static string[] HistoryFiles()
    {
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return new [] {
            System.IO.Path.Combine(local, "ArcaneVaultNext", "match-history.sqlite3"),
            System.IO.Path.Combine(local, "ArcaneVault", "match-history.sqlite3")
        }.Where(File.Exists).ToArray();
    }

    private static IReadOnlyList<MatchItem> ReadMatches()
    {
        var result = new List<MatchItem>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in HistoryFiles())
        {
            using var db = new SqliteConnection(new SqliteConnectionStringBuilder {
                DataSource = file, Mode = SqliteOpenMode.ReadOnly, Pooling = false
            }.ToString());
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT match_id,played_utc,result,deck_name,deck_id,result_source,note "+
                "FROM matches ORDER BY played_utc DESC LIMIT 2000";
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                string id = rd.GetString(0);
                if (string.IsNullOrWhiteSpace(id) || !seen.Add(id)) continue;
                result.Add(new MatchItem(file, id,
                    rd.IsDBNull(1) ? "" : rd.GetString(1),
                    rd.IsDBNull(2) ? "unknown" : rd.GetString(2),
                    rd.IsDBNull(3) ? "" : rd.GetString(3),
                    rd.IsDBNull(4) ? "" : rd.GetString(4),
                    rd.IsDBNull(5) ? "" : rd.GetString(5),
                    rd.IsDBNull(6) ? "" : rd.GetString(6)));
            }
        }
        return result.OrderByDescending(x => x.Played, StringComparer.Ordinal)
            .Take(2000).ToArray();
    }

    private void FilterRows()
    {
        rows.ItemsSource = needsReview.IsChecked == true
            ? loaded.Where(x => string.IsNullOrWhiteSpace(x.DeckName) || x.Result == "unknown").ToArray()
            : loaded;
        UpdateSelection();
    }

    private void UpdateSelection()
    {
        assign.IsEnabled = !busy && rows.SelectedItem is MatchItem &&
            deckPicker.SelectedItem is SavedDeck;
        rememberMapping.IsEnabled = !busy && rows.SelectedItem is MatchItem selected &&
            !string.IsNullOrWhiteSpace(selected.DeckId);
        rememberMapping.Visibility = rememberMapping.IsEnabled
            ? Visibility.Visible : Visibility.Collapsed;
        if (rows.SelectedItem is MatchItem item)
        {
            status.Text = item.Status + " · " +
                (string.IsNullOrWhiteSpace(item.Note) ? "No diagnostic note" : item.Note);
        }
    }

    public async Task ReloadAsync()
    {
        if (busy) return;
        busy = true;
        assign.IsEnabled = false;
        status.Text = "Reading local Arena match history and saved decks…";
        try
        {
            var data = await Task.Run(() => {
                var matches = ReadMatches();
                var decks = File.Exists(NativeWorkspace.DefaultPath)
                    ? NativeWorkspaceSafety.ListDecks(NativeWorkspace.DefaultPath)
                        .Where(x => !string.IsNullOrWhiteSpace(x.Id))
                        .Select(x => new SavedDeck(x.Id, x.Name, x.ArenaDeckId))
                        .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                        .ToArray()
                    : Array.Empty<SavedDeck>();
                return (matches, decks);
            });
            loaded = data.matches;
            deckPicker.ItemsSource = data.decks;
            if (data.decks.Length > 0) deckPicker.SelectedIndex = 0;
            FilterRows();
            status.Text = loaded.Count == 0 ? "No Arena matches recorded yet. Enable Detailed Logs in MTG Arena." :
                loaded.Count(x => string.IsNullOrWhiteSpace(x.DeckName) || x.Result == "unknown") +
                " matches need assignment or result review; " + loaded.Count +
                " recorded locally. Select a row to see its detection note.";
        }
        catch (Exception ex) when (ex is IOException or SqliteException or
            UnauthorizedAccessException or InvalidDataException or InvalidOperationException)
        { status.Text = "Match history unavailable: " + ex.Message; }
        finally { busy = false; UpdateSelection(); }
    }

    private async Task AssignAsync()
    {
        if (busy || rows.SelectedItem is not MatchItem item ||
            deckPicker.SelectedItem is not SavedDeck deck) return;
        string? corrected = resultPicker.SelectedIndex switch {
            1 => "win", 2 => "loss", 3 => "draw", 4 => "unknown", _ => null
        };
        if (string.Equals(item.Result, "unknown", StringComparison.OrdinalIgnoreCase) &&
            corrected is null)
        {
            status.Text = "This result was not verified by Arena. Choose Win, Loss, Draw or Unknown explicitly.";
            return;
        }
        var answer = MessageBox.Show(
            "Assign the existing " + (corrected ?? item.Result) +
            " match from " + item.Date + " to \"" + deck.Name +
            "\"? No new match will be created.",
            "Confirm Arena match assignment", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;
        busy = true; assign.IsEnabled = false;
        try
        {
            bool saveMapping = rememberMapping.IsChecked == true;
            var assignment = await Task.Run(() => {
                using var history = new MatchHistory(item.Database);
                bool changed = history.Update(item.MatchId, corrected, deck.Name, null,
                    "Deck assignment: manually confirmed in Arcane Vault Match History",
                    deck.Id);
                if (changed && saveMapping &&
                    !string.IsNullOrWhiteSpace(item.DeckId))
                {
                    // Only the user's confirmed Arena ID is remembered. Any
                    // previously conflicting association remains unchanged.
                    if (!history.RememberDeckMapping(item.DeckId, deck.Id, deck.Name))
                        return (changed, conflict: true, backfilled: 0);
                    return (changed, conflict: false,
                        backfilled: history.BackfillExactArenaId(item.DeckId, deck.Id, deck.Name));
                }
                return (changed, conflict: false, backfilled: 0);
            });
            if (!assignment.changed)
            {
                status.Text = "Match not updated. It may have been removed by another process.";
                return;
            }
            await ReloadAfterAssignmentAsync(deck.Name);
            if (assignment.conflict)
                status.Text += " Existing Arena ID mapping points to another deck and was NOT overwritten. Review that mapping before applying it to future matches.";
            else if (assignment.backfilled > 0)
                status.Text += " Linked " + assignment.backfilled + " older unassigned match(es) using that confirmed Arena ID.";
        }
        catch (Exception ex) when (ex is SqliteException or IOException or
            UnauthorizedAccessException or InvalidOperationException)
        { status.Text = "Could not update match assignment: " + ex.Message; }
        finally { busy = false; UpdateSelection(); }
    }

    private async Task ReloadAfterAssignmentAsync(string deckName)
    {
        loaded = await Task.Run(ReadMatches);
        FilterRows();
        status.Text = "Updated the existing match for " + deckName +
            ". Arcane Vault, Stream Deck and EDGE totals refresh on the next native snapshot.";
    }
}
