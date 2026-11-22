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
        string Result, string DeckName, string DeckId, string Source, string Note,
        int GameWins, int GameLosses, string Format, string OpponentColors,
        string NativeDeckId = "")
    {
        public string Date => DateTimeOffset.TryParse(Played, out var date)
            ? date.ToLocalTime().ToString("g") : Played;
        public string Status => string.IsNullOrWhiteSpace(DeckName)
            ? (Result == "unknown" ? "Review result & deck" : "Needs deck assignment")
            : (Result == "unknown" ? "Review result" : "Assigned");
        public string Deck => string.IsNullOrWhiteSpace(DeckName) ? "Unassigned" : DeckName;
        public string ArenaIdStatus => string.IsNullOrWhiteSpace(DeckId)
            ? "Not captured" : "Captured";
        public string GameRecord => GameWins + GameLosses > 0
            ? GameWins + "–" + GameLosses : "—";
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
    private readonly TextBox opponentColors = new() { Width = 125, MinHeight = 32,
        Margin = new Thickness(8, 0, 8, 0),
        ToolTip = "Opponent color identity: W, U, B, R, G or C. Blank means unknown." };
    private readonly Button saveOpponentColors = new() { Content = "Save opponent colors",
        Padding = new Thickness(12, 7, 12, 7), IsEnabled = false };
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
    private readonly TextBox search = new() { Width = 220, MinHeight = 30,
        Margin = new Thickness(10, 0, 10, 0),
        ToolTip = "Filter by deck name, date, result, Arena ID, status, or note" };
    private readonly Button resolveVerified = new() { Content = "Resolve verified IDs",
        Padding = new Thickness(14, 9, 14, 9),
        ToolTip = "Assign only exact Arena deck IDs already linked to one saved deck; never guess missing IDs" };
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
        toolbar.Children.Add(search);
        toolbar.Children.Add(resolveVerified);
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
        var colorsRow = new WrapPanel { Margin = new Thickness(0, 9, 0, 0) };
        colorsRow.Children.Add(new TextBlock { Text = "Opponent colors (WUBRG / C):",
            VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.LightGray });
        colorsRow.Children.Add(opponentColors);
        colorsRow.Children.Add(saveOpponentColors);
        footer.Children.Add(colorsRow);
        saveOpponentColors.Click += async (_, _) => await SaveOpponentColorsAsync();

        var grid = new GridView();
        grid.Columns.Add(new GridViewColumn { Header = "When", Width = 145,
            DisplayMemberBinding = new System.Windows.Data.Binding(nameof(MatchItem.Date)) });
        grid.Columns.Add(new GridViewColumn { Header = "Result", Width = 90,
            DisplayMemberBinding = new System.Windows.Data.Binding(nameof(MatchItem.Result)) });
        grid.Columns.Add(new GridViewColumn { Header = "Games W–L", Width = 95,
            DisplayMemberBinding = new System.Windows.Data.Binding(nameof(MatchItem.GameRecord)) });
        grid.Columns.Add(new GridViewColumn { Header = "Deck", Width = 225,
            DisplayMemberBinding = new System.Windows.Data.Binding(nameof(MatchItem.Deck)) });
        grid.Columns.Add(new GridViewColumn { Header = "Arena ID", Width = 115,
            DisplayMemberBinding = new System.Windows.Data.Binding(nameof(MatchItem.ArenaIdStatus)) });
        grid.Columns.Add(new GridViewColumn { Header = "Status", Width = 170,
            DisplayMemberBinding = new System.Windows.Data.Binding(nameof(MatchItem.Status)) });
        grid.Columns.Add(new GridViewColumn { Header = "Opp. colors", Width = 90,
            DisplayMemberBinding = new System.Windows.Data.Binding(nameof(MatchItem.OpponentColors)) });
        grid.Columns.Add(new GridViewColumn { Header = "Origin", Width = 125,
            DisplayMemberBinding = new System.Windows.Data.Binding(nameof(MatchItem.Origin)) });
        rows.View = grid;
        root.Children.Add(rows);

        rows.SelectionChanged += (_, _) => UpdateSelection();
        deckPicker.SelectionChanged += (_, _) => UpdateSelection();
        needsReview.Checked += (_, _) => FilterRows();
        needsReview.Unchecked += (_, _) => FilterRows();
        search.TextChanged += (_, _) => FilterRows();
        resolveVerified.Click += async (_, _) => await ResolveVerifiedAsync();
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
        var positions = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var file in HistoryFiles())
        {
            using var db = new SqliteConnection(new SqliteConnectionStringBuilder {
                DataSource = file, Mode = SqliteOpenMode.ReadOnly, Pooling = false
            }.ToString());
            db.Open();
            using var cmd = db.CreateCommand();
            using var schema = db.CreateCommand();
            schema.CommandText = "PRAGMA table_info(matches)";
            bool hasOpponentColors = false;
            bool hasNativeDeckId = false;
            using (var columns = schema.ExecuteReader())
                while (columns.Read())
                {
                    if (columns.GetString(1) == "opponent_colors") hasOpponentColors = true;
                    if (columns.GetString(1) == "native_deck_id") hasNativeDeckId = true;
                }
            cmd.CommandText = "SELECT match_id,played_utc,result,deck_name,deck_id,result_source,note,game_wins,game_losses,format,"+
                (hasOpponentColors ? "opponent_colors" : "''")+","+
                (hasNativeDeckId ? "native_deck_id" : "''")+
                " FROM matches ORDER BY played_utc DESC LIMIT 2000";
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                string id = rd.GetString(0);
                if (string.IsNullOrWhiteSpace(id)) continue;
                // The native history is authoritative for results and deck
                // identity. A legacy duplicate can still contain an older,
                // explicit opponent-color annotation omitted from the native
                // copy. Read it without overwriting any match outcomes.
                if (positions.TryGetValue(id, out int priorIndex))
                {
                    var primary = result[priorIndex];
                    string alternate = rd.IsDBNull(10) ? "" : rd.GetString(10);
                    string resolved = MatchPerformanceAnalytics.PreferExplicitOpponentColors(
                        primary.OpponentColors, alternate);
                    // A legacy duplicate may retain an Arena deck ID and a
                    // verified assignment note missing from the native copy.
                    // Use only missing metadata for the same named deck; never
                    // replace a native result, assignment or existing Arena ID.
                    bool sameDeck = primary.DeckName.Trim().Equals(
                        (rd.IsDBNull(3) ? "" : rd.GetString(3)).Trim(),
                        StringComparison.OrdinalIgnoreCase);
                    string secondaryId = rd.IsDBNull(4) ? "" : rd.GetString(4);
                    string secondaryNote = rd.IsDBNull(6) ? "" : rd.GetString(6);
                    result[priorIndex] = primary with {
                        OpponentColors = resolved,
                        DeckId = sameDeck && string.IsNullOrWhiteSpace(primary.DeckId)
                            ? secondaryId : primary.DeckId,
                        Note = sameDeck &&
                            !MatchDeckAttribution.HasVerifiedAssignmentNote(primary.Note) &&
                            MatchDeckAttribution.HasVerifiedAssignmentNote(secondaryNote)
                            ? secondaryNote : primary.Note
                    };
                    continue;
                }
                positions[id] = result.Count;
                result.Add(new MatchItem(file, id,
                    rd.IsDBNull(1) ? "" : rd.GetString(1),
                    rd.IsDBNull(2) ? "unknown" : rd.GetString(2),
                    rd.IsDBNull(3) ? "" : rd.GetString(3),
                    rd.IsDBNull(4) ? "" : rd.GetString(4),
                    rd.IsDBNull(5) ? "" : rd.GetString(5),
                    rd.IsDBNull(6) ? "" : rd.GetString(6),
                    rd.IsDBNull(7) ? 0 : rd.GetInt32(7),
                    rd.IsDBNull(8) ? 0 : rd.GetInt32(8),
                    rd.IsDBNull(9) ? "" : ArenaQueueInfo.Label(rd.GetString(9)),
                    rd.IsDBNull(10) ? "" : rd.GetString(10),
                    rd.IsDBNull(11) ? "" : rd.GetString(11)));
            }
        }
        return result.OrderByDescending(x => x.Played, StringComparer.Ordinal)
            .Take(2000).ToArray();
    }

    // Shared read-only view of the same native-first, deduplicated rows shown
    // in Match History. Analytics never opens the database for writing.
    internal static IReadOnlyList<MatchPerformanceAnalytics.Entry> ReadPerformance() =>
        ReadMatches()
            .Where(x => DateTimeOffset.TryParse(x.Played, out _))
            .Select(x => new MatchPerformanceAnalytics.Entry(
                x.MatchId, DateTimeOffset.Parse(x.Played),
                x.DeckName, x.NativeDeckId, x.Format, x.Result,
                x.OpponentColors, x.DeckId, x.Note))
            .ToArray();

    private void FilterRows()
    {
        IEnumerable<MatchItem> filtered = needsReview.IsChecked == true
            ? loaded.Where(x => string.IsNullOrWhiteSpace(x.DeckName) || x.Result == "unknown")
            : loaded;
        var term = search.Text.Trim();
        if (term.Length > 0)
            filtered = filtered.Where(x =>
                x.Date.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.Result.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.Deck.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.DeckId.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.Status.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.Note.Contains(term, StringComparison.OrdinalIgnoreCase));
        rows.ItemsSource = filtered.ToArray();
        UpdateSelection();
    }

    private void UpdateSelection()
    {
        assign.IsEnabled = !busy && rows.SelectedItem is MatchItem &&
            deckPicker.SelectedItem is SavedDeck;
        resolveVerified.IsEnabled = !busy && loaded.Any(x =>
            string.IsNullOrWhiteSpace(x.DeckName) &&
            !string.IsNullOrWhiteSpace(x.DeckId));
        saveOpponentColors.IsEnabled = !busy && rows.SelectedItem is MatchItem;
        rememberMapping.IsEnabled = !busy && rows.SelectedItem is MatchItem selected &&
            !string.IsNullOrWhiteSpace(selected.DeckId);
        rememberMapping.Visibility = rememberMapping.IsEnabled
            ? Visibility.Visible : Visibility.Collapsed;
        if (rows.SelectedItem is MatchItem item)
        {
            opponentColors.Text = item.OpponentColors;
            status.Text = item.Status + (string.IsNullOrWhiteSpace(item.DeckId) ?
                " · Arena deck ID absent" : " · Arena deck ID: " + item.DeckId) + " · " +
                (string.IsNullOrWhiteSpace(item.Note) ? "No diagnostic note" : item.Note);
        }
    }

    public async Task ReloadAsync()
    {
        if (busy) return;
        busy = true;
        assign.IsEnabled = false;
        resolveVerified.IsEnabled = false;
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
                " recorded locally. " +
                loaded.Count(x => !string.IsNullOrWhiteSpace(x.DeckId)) +
                " contain Arena deck IDs. Select a row to see its detection note.";
        }
        catch (Exception ex) when (ex is IOException or SqliteException or
            UnauthorizedAccessException or InvalidDataException or InvalidOperationException)
        { status.Text = "Match history unavailable: " + ex.Message; }
        finally { busy = false; UpdateSelection(); }
    }

    // Exact-ID-only recovery. Never infer a deck from matching names or
    // nearby match timestamps. The update methods already refuse assigned rows.
    private async Task ResolveVerifiedAsync()
    {
        if (busy) return;
        var answer = MessageBox.Show(
            "Resolve older unassigned matches only when their captured Arena deck ID " +
            "uniquely identifies a saved deck or an explicitly remembered mapping?\n\n" +
            "Matches without Arena IDs remain untouched. Results, game totals and " +
            "manual assignments are never changed.",
            "Resolve verified Arena deck IDs", MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;
        busy = true;
        assign.IsEnabled = false;
        resolveVerified.IsEnabled = false;
        status.Text = "Reconciling verified Arena IDs…";
        try
        {
            int count = await Task.Run(() => {
                var decks = File.Exists(NativeWorkspace.DefaultPath)
                    ? NativeWorkspaceSafety.ListDecks(NativeWorkspace.DefaultPath)
                        .Where(x => !string.IsNullOrWhiteSpace(x.Id))
                        .Select(x => new SavedDeck(x.Id, x.Name, x.ArenaDeckId))
                        .ToArray()
                    : Array.Empty<SavedDeck>();
                var uniqueNative = decks.GroupBy(x => x.Id, StringComparer.Ordinal)
                    .Where(g => g.Count() == 1)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
                var arenaGroups = decks.Where(x => !string.IsNullOrWhiteSpace(x.ArenaId))
                    .GroupBy(x => x.ArenaId.Trim(), StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                var ambiguous = arenaGroups.Where(g => g.Count() != 1)
                    .Select(g => g.Key)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var verified = arenaGroups.Where(g => g.Count() == 1)
                    .ToDictionary(g => g.Key, g => g.First(),
                        StringComparer.OrdinalIgnoreCase);
                using (var mappingStore = new MatchHistory())
                {
                    foreach (var mapping in mappingStore.Mappings())
                    {
                        if (ambiguous.Contains(mapping.ArenaId) ||
                            !uniqueNative.TryGetValue(mapping.NativeId, out var deck))
                            continue;
                        if (verified.TryGetValue(mapping.ArenaId, out var direct) &&
                            direct.Id != deck.Id)
                        {
                            // Conflicting evidence is not an automatic choice.
                            verified.Remove(mapping.ArenaId);
                            ambiguous.Add(mapping.ArenaId);
                            continue;
                        }
                        verified[mapping.ArenaId] = deck;
                    }
                }
                int changed = 0;
                foreach (var file in HistoryFiles())
                {
                    using var history = new MatchHistory(file);
                    foreach (var pair in verified)
                        changed += history.BackfillExactArenaId(
                            pair.Key, pair.Value.Id, pair.Value.Name);
                }
                return changed;
            });
            await ReloadAsyncAfterBulk(count);
        }
        catch (Exception ex) when (ex is SqliteException or IOException or
            UnauthorizedAccessException or InvalidOperationException)
        { status.Text = "Could not reconcile verified IDs: " + ex.Message; }
        finally { busy = false; UpdateSelection(); }
    }

    private async Task ReloadAsyncAfterBulk(int changed)
    {
        loaded = await Task.Run(ReadMatches);
        FilterRows();
        status.Text = changed == 0
            ? "No additional matches could be safely assigned by exact Arena ID. " +
              "Older matches with no ID still require manual review."
            : "Safely assigned " + changed + " existing match(es) using verified Arena IDs. " +
              "No match results or game totals were changed.";
    }

    private async Task SaveOpponentColorsAsync()
    {
        if (busy || rows.SelectedItem is not MatchItem item) return;
        var entered = opponentColors.Text;
        busy = true;
        try
        {
            bool changed = await Task.Run(() =>
            {
                using var history = new MatchHistory(item.Database);
                return history.UpdateOpponentColors(item.MatchId, entered);
            });
            if (changed)
            {
                await ReloadAsyncAfterColors();
                status.Text = "Opponent color identity saved to existing match only.";
            }
            else status.Text = "Match no longer exists.";
        }
        catch (Exception error)
        {
            status.Text = "Opponent colors not saved: " + error.Message;
        }
        finally { busy = false; UpdateSelection(); }
    }

    private async Task ReloadAsyncAfterColors()
    {
        loaded = await Task.Run(ReadMatches);
        FilterRows();
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
        bool saveMapping = rememberMapping.IsChecked == true &&
            !string.IsNullOrWhiteSpace(item.DeckId);
        bool replaceMapping = false;
        if (saveMapping)
        {
            var existing = await Task.Run(() => {
                using var history = new MatchHistory();
                return history.Mappings().FirstOrDefault(m =>
                    m.ArenaId.Equals(item.DeckId, StringComparison.OrdinalIgnoreCase));
            });
            if (existing is not null && existing.NativeId != deck.Id)
            {
                var decision = MessageBox.Show(
                    "Arena deck ID " + item.DeckId + " is already remembered for " +
                    existing.DeckName + ".\n\nYes: replace its future mapping with " +
                    deck.Name + ".\nNo: correct only this match.\nCancel: make no change.",
                    "Existing Arena deck mapping", MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Warning);
                if (decision == MessageBoxResult.Cancel) return;
                replaceMapping = decision == MessageBoxResult.Yes;
                saveMapping = replaceMapping;
            }
        }
        busy = true; assign.IsEnabled = false;
        try
        {
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
                    // The running Arena tracker reads the primary native
                    // match-history database. Persist there even when this
                    // particular match was found in legacy compatibility data.
                    using var mappings = new MatchHistory();
                    if (!mappings.RememberDeckMapping(item.DeckId, deck.Id,
                        deck.Name, replaceExisting: replaceMapping))
                        return (changed, conflict: true, backfilled: 0);
                    int count = history.BackfillExactArenaId(item.DeckId, deck.Id, deck.Name);
                    // Also reconcile newer native matches if correcting a
                    // historical record in the legacy database.
                    count += mappings.BackfillExactArenaId(item.DeckId, deck.Id, deck.Name);
                    return (changed, conflict: false, backfilled: count);
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
