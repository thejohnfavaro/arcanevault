namespace ArcaneVault.Next.Desktop;

/// <summary>Pure, read-only statistics derived from immutable match rows.
/// A match is counted once by its Arena match ID. Unknown results are never losses.</summary>
internal static class MatchPerformanceAnalytics
{
    internal sealed record Entry(string MatchId, DateTimeOffset Played,
        string DeckName, string NativeDeckId, string Format, string Result);
    internal sealed record FormatRow(string Format, int Wins, int Losses)
    {
        public int Matches => Wins + Losses;
        public double Rate => Matches == 0 ? 0 : 100d * Wins / Matches;
        public string Record => $"{Wins}–{Losses}";
        public string WinRate => $"{Rate:0.#}%";
    }
    internal sealed record Point(int Index, double Rate, string Result);
    internal sealed record Report(int Wins, int Losses, int Unresolved,
        int LastTenWins, int LastTenLosses, int CurrentStreak,
        string StreakResult, IReadOnlyList<FormatRow> Formats,
        IReadOnlyList<Point> Trend, IReadOnlyList<Entry> Recent)
    {
        public int Matches => Wins + Losses;
        public double Rate => Matches == 0 ? 0 : 100d * Wins / Matches;
    }

    internal static Report Build(IEnumerable<Entry> source, string? deckName,
        int days, DateTimeOffset now)
    {
        var cutoff = days > 0 ? now.AddDays(-days) : DateTimeOffset.MinValue;
        var filtered = source
            .Where(x => !string.IsNullOrWhiteSpace(x.MatchId) &&
                x.Played >= cutoff && x.Played <= now &&
                !string.IsNullOrWhiteSpace(x.DeckName) &&
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
        // Moving 10-match win rate; every point uses only confirmed games.
        var trend = new List<Point>();
        for (int i = 0; i < confirmed.Length; i++)
        {
            if (i < confirmed.Length - 20) continue;
            var window = confirmed.Skip(Math.Max(0, i - 9)).Take(Math.Min(i + 1, 10)).ToArray();
            trend.Add(new Point(i + 1,
                100d * window.Count(x => x.Result == "win") / window.Length,
                confirmed[i].Result));
        }
        return new Report(wins, losses, filtered.Length - confirmed.Length,
            lastTen.Count(x => x.Result == "win"),
            lastTen.Count(x => x.Result == "loss"),
            streak, lastResult, formats, trend, recent);
    }
}
