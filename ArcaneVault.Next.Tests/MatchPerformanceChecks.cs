using ArcaneVault.Next.Desktop;

internal static class MatchPerformanceChecks
{
    internal static void Run()
    {
        var now = new DateTimeOffset(2026,11,17,12,0,0,TimeSpan.Zero);
        var rows = new[] {
            new MatchPerformanceAnalytics.Entry("one",now.AddDays(-1),"Deck A","native-a","Brawl","win"),
            new MatchPerformanceAnalytics.Entry("one",now.AddDays(-1),"Deck A","native-a","Brawl","win"),
            new MatchPerformanceAnalytics.Entry("two",now.AddDays(-2),"Deck A","native-a","Brawl","loss"),
            new MatchPerformanceAnalytics.Entry("three",now.AddDays(-3),"Deck A","native-a","Historic","win"),
            new MatchPerformanceAnalytics.Entry("unknown",now.AddDays(-4),"Deck A","native-a","Historic","unknown"),
            new MatchPerformanceAnalytics.Entry("unassigned",now.AddDays(-4),"","","Historic","loss"),
            new MatchPerformanceAnalytics.Entry("old",now.AddDays(-120),"Deck A","native-a","Brawl","loss"),
            new MatchPerformanceAnalytics.Entry("other",now.AddDays(-1),"Deck B","native-b","Brawl","loss")
        };
        var a = MatchPerformanceAnalytics.Build(rows,"Deck A",30,now);
        Assert(a.Wins == 2 && a.Losses == 1 && a.Unresolved == 1,
            "Exclude duplicate match IDs, unassigned and unknown outcomes.");
        Assert(a.Formats.Single(x => x.Format == "Brawl").Matches == 2,
            "Formats must reflect only confirmed results for the selected deck.");
        Assert(a.Formats.Single(x => x.Format == "Historic").Wins == 1,
            "Historic match not included correctly.");
        Assert(a.Trend.Count == 3 && a.Trend[^1].Rate > 60 && a.Trend[^1].Rate < 70,
            "Rolling win rate must use verified match outcomes only.");
        Assert(a.CurrentStreak == 1 && a.StreakResult == "win",
            "Current streak must use the latest chronological confirmed outcome.");
        var all = MatchPerformanceAnalytics.Build(rows,null,0,now);
        Assert(all.Wins == 2 && all.Losses == 3 && all.Unresolved == 1,
            "All time totals must remain deduplicated and exclude unassigned matches.");
        var shortRange = MatchPerformanceAnalytics.Build(rows,"Deck A",1,now);
        Assert(shortRange.Matches == 1, "Date range should not include older results.");
    }
    private static void Assert(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }
}
