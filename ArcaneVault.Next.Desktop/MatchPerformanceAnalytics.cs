namespace ArcaneVault.Next.Desktop;

/// <summary>Pure, read-only statistics derived from immutable match rows.
/// A match is counted once by its Arena match ID. Unknown results are never losses.</summary>
internal static class MatchPerformanceAnalytics
{
    // Shared with the headless regression project. A historical duplicate's
    // explicit opponent-color annotation can fill an empty primary field;
    // the native match result and deck identity remain authoritative.
    internal static string PreferExplicitOpponentColors(string primary, string secondary) =>
        string.IsNullOrWhiteSpace(primary) ? secondary : primary;

    internal sealed record Entry(string MatchId, DateTimeOffset Played,
        string DeckName, string NativeDeckId, string Format, string Result,
        string OpponentColors = "", string ArenaDeckId = "", string AttributionNote = "");
    internal sealed record FormatRow(string Format, int Wins, int Losses)
    {
        public int Matches => Wins + Losses;
        public double Rate => Matches == 0 ? 0 : 100d * Wins / Matches;
        public string Record => $"{Wins}–{Losses}";
        public string WinRate => $"{Rate:0.#}%";
    }
    internal sealed record Point(int Index, double Rate, string Result);
    internal sealed record WeekRow(string Week, int Wins, int Losses)
    {
        public int Matches => Wins + Losses;
        public string Record => $"{Wins}–{Losses}";
        public string WinRate => Matches == 0 ? "—" : $"{100d * Wins / Matches:0.#}%";
    }
    internal sealed record TimelineRow(string When, string Deck, string Format,
        string Result, string RollingRate);
    internal sealed record Report(int Wins, int Losses, int Unresolved,
        int LastTenWins, int LastTenLosses, int CurrentStreak,
        string StreakResult, IReadOnlyList<FormatRow> Formats,
        IReadOnlyList<Point> Trend, IReadOnlyList<Entry> Recent,
        IReadOnlyList<TimelineRow> Timeline, IReadOnlyList<WeekRow> Weeks,
        int BestWinStreak, int WorstLossStreak)
    {
        public int Matches => Wins + Losses;
        public double Rate => Matches == 0 ? 0 : 100d * Wins / Matches;
    }

    /// <summary>Group only confirmed, uniquely attributed matches by the saved
    /// commander's verified color identity. Unknown is never colorless.</summary>
    internal static IReadOnlyList<FormatRow> ByOwnColorIdentity(
        IEnumerable<Entry> source, IReadOnlyDictionary<string,string> deckColors)
    {
        string Identity(Entry match)
        {
            if (!string.IsNullOrWhiteSpace(match.NativeDeckId) &&
                deckColors.TryGetValue(match.NativeDeckId, out var byId))
                return byId;
            return deckColors.TryGetValue("name:" + match.DeckName.Trim(),
                out var byName) ? byName : "Unknown";
        }
        return source.Where(x => !string.IsNullOrWhiteSpace(x.MatchId) &&
                !string.IsNullOrWhiteSpace(x.DeckName) &&
                x.Result is "win" or "loss")
            .GroupBy(x => x.MatchId, StringComparer.Ordinal)
            .Select(g => g.OrderByDescending(x => x.Played).First())
            .GroupBy(Identity, StringComparer.OrdinalIgnoreCase)
            .Select(g => new FormatRow(g.Key, g.Count(x => x.Result == "win"),
                g.Count(x => x.Result == "loss")))
            .OrderBy(g => g.Format == "Unknown" ? 1 : 0)
            .ThenByDescending(g => g.Matches).ThenBy(g => g.Format).ToArray();
    }

    internal static Report Build(IEnumerable<Entry> source, string? deckName,
        int days, DateTimeOffset now, string? format = null)
    {
        var cutoff = days > 0 ? now.AddDays(-days) : DateTimeOffset.MinValue;
        var filtered = source
            .Where(x => !string.IsNullOrWhiteSpace(x.MatchId) &&
                x.Played >= cutoff && x.Played <= now &&
                !string.IsNullOrWhiteSpace(x.DeckName) &&
                (string.IsNullOrWhiteSpace(format) ||
                    (string.IsNullOrWhiteSpace(x.Format) ? "Unknown format" : x.Format.Trim())
                        .Equals(format.Trim(), StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(deckName) ||
                    x.DeckName.Trim().Equals(deckName.Trim(),
                        StringComparison.OrdinalIgnoreCase)))
            .GroupBy(x => x.MatchId, StringComparer.Ordinal)
            .Select(x => x.OrderByDescending(y => y.Played).First())
            .OrderBy(x => x.Played).ThenBy(x => x.MatchId, StringComparer.Ordinal)
            .ToArray();
        var confirmed = filtered.Where(x => x.Result is "win" or "loss").ToArray();
        int wins = confirmed.Count(x => x.Result == "win");
        int losses = confirmed.Length - wins;
        var lastTen = confirmed.TakeLast(10).ToArray();
        int streak = 0;
        string lastResult = confirmed.LastOrDefault()?.Result ?? "";
        for (int i = confirmed.Length - 1; i >= 0 && confirmed[i].Result == lastResult; i--)
            streak++;
        var formats = confirmed.GroupBy(x =>
                string.IsNullOrWhiteSpace(x.Format) ? "Unknown format" : x.Format.Trim(),
                StringComparer.OrdinalIgnoreCase)
            .Select(g => new FormatRow(g.Key, g.Count(x => x.Result == "win"),
                g.Count(x => x.Result == "loss")))
            .OrderByDescending(x => x.Matches).ThenBy(x => x.Format).ToArray();
        var recent = confirmed.TakeLast(20).Reverse().ToArray();
        int currentWins = 0, currentLosses = 0, bestWins = 0, worstLosses = 0;
        foreach (var match in confirmed)
        {
            if (match.Result == "win")
            {
                currentWins++;
                currentLosses = 0;
                bestWins = Math.Max(bestWins, currentWins);
            }
            else
            {
                currentLosses++;
                currentWins = 0;
                worstLosses = Math.Max(worstLosses, currentLosses);
            }
        }
        // ISO week keys prevent regional calendar defaults from shifting results.
        var weekly = confirmed.GroupBy(x => {
                var day = x.Played.UtcDateTime.Date;
                int offset = ((int)day.DayOfWeek + 6) % 7;
                return day.AddDays(-offset).ToString("yyyy-MM-dd");
            })
            .Select(g => new WeekRow(g.Key, g.Count(x => x.Result == "win"),
                g.Count(x => x.Result == "loss")))
            .OrderByDescending(x => x.Week, StringComparer.Ordinal)
            .Take(12).ToArray();
        // Moving 10-match win rate; every point uses only confirmed games.
        var trend = new List<Point>();
        var timeline = new List<TimelineRow>();
        for (int i = 0; i < confirmed.Length; i++)
        {
            if (i < confirmed.Length - 20) continue;
            var window = confirmed.Skip(Math.Max(0, i - 9)).Take(Math.Min(i + 1, 10)).ToArray();
            double rolling = 100d * window.Count(x => x.Result == "win") / window.Length;
            trend.Add(new Point(i + 1, rolling, confirmed[i].Result));
            timeline.Add(new TimelineRow(confirmed[i].Played.ToLocalTime().ToString("g"),
                confirmed[i].DeckName,
                string.IsNullOrWhiteSpace(confirmed[i].Format) ? "Unknown format" : confirmed[i].Format,
                confirmed[i].Result.ToUpperInvariant(), $"{rolling:0.#}%"));
        }
        return new Report(wins, losses, filtered.Length - confirmed.Length,
            lastTen.Count(x => x.Result == "win"),
            lastTen.Count(x => x.Result == "loss"),
            streak, lastResult, formats, trend, recent,
            timeline.AsEnumerable().Reverse().ToArray(), weekly, bestWins, worstLosses);
    }
}
