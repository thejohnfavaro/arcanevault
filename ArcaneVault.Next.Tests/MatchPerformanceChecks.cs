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
        // ARRANGE: duplicate match IDs, an unknown result, and an unassigned result.
        // ACT: calculate the last 30 days of Deck A's recorded matches.
        var a = MatchPerformanceAnalytics.Build(rows,"Deck A",30,now);
        // ASSERT: only confirmed, uniquely identified results affect win rate.
        RegressionExpect.Equal(a.Wins, 2, "Deck A confirmed wins after deduplication");
        RegressionExpect.Equal(a.Losses, 1, "Deck A confirmed losses after deduplication");
        RegressionExpect.Equal(a.Unresolved, 1,
            "Unknown result remains unresolved rather than becoming a loss");
        RegressionExpect.That(a.Formats.Single(x => x.Format == "Brawl").Matches == 2,
            "Formats must reflect only confirmed results for the selected deck.");
        RegressionExpect.That(a.Formats.Single(x => x.Format == "Historic").Wins == 1,
            "Historic match not included correctly.");
        RegressionExpect.That(a.Trend.Count == 3 && a.Trend[^1].Rate > 60 && a.Trend[^1].Rate < 70,
            "Rolling win rate must use verified match outcomes only.");
        RegressionExpect.That(a.CurrentStreak == 1 && a.StreakResult == "win",
            "Current streak must use the latest chronological confirmed outcome.");
        // ARRANGE/ACT: switch to all decks and all-time without changing fixtures.
        var all = MatchPerformanceAnalytics.Build(rows,null,0,now);
        RegressionExpect.Equal(all.Wins, 2, "All-deck, all-time confirmed wins");
        RegressionExpect.Equal(all.Losses, 3, "All-deck, all-time confirmed losses");
        RegressionExpect.Equal(all.Unresolved, 1, "Unresolved matches stay outside win/loss totals");
        // ACT: filter to Brawl; the timeline and unresolved count must also filter.
        var brawl = MatchPerformanceAnalytics.Build(rows,"Deck A",30,now,"Brawl");
        RegressionExpect.That(brawl.Wins == 1 && brawl.Losses == 1 && brawl.Unresolved == 0,
            "Format filtering must apply to primary statistics and unresolved count.");
        RegressionExpect.That(brawl.Formats.Count == 1 && brawl.Formats[0].Format == "Brawl" &&
            brawl.Timeline.Count == 2, "Format-specific timeline should exclude other queues.");
        RegressionExpect.That(brawl.Timeline[0].Result == "WIN" && brawl.Timeline[1].Result == "LOSS",
            "Timeline must show newest confirmed matches first.");
        RegressionExpect.That(brawl.Timeline[0].RollingRate == "50%",
            "Recent timeline must use its own filtered rolling win rate.");
        var historic = MatchPerformanceAnalytics.Build(rows,"Deck A",0,now,"Historic");
        RegressionExpect.That(historic.Wins == 1 && historic.Losses == 0 && historic.Unresolved == 1,
            "Unknown outcomes must not count as losses within an individual format.");
        var compare = MatchPerformanceAnalytics.Build(rows,"Deck B",30,now,"Brawl");
        RegressionExpect.That(compare.Wins == 0 && compare.Losses == 1 && compare.Rate == 0,
            "The comparison deck must have independent, filtered metrics.");
        var unclassified = MatchPerformanceAnalytics.Build(new [] {
            new MatchPerformanceAnalytics.Entry("blank-format",now,"Deck A","","","win")
        },"Deck A",30,now,"Unknown format");
        RegressionExpect.That(unclassified.Wins == 1 && unclassified.Formats.Single().Format == "Unknown format",
            "Unlabeled queues must be selectable as Unknown format.");
        RegressionExpect.That(a.BestWinStreak == 1 && a.WorstLossStreak == 1,
            "Longest streak must use confirmed chronological outcomes only.");
        RegressionExpect.That(a.Weeks.Count > 0 && a.Weeks.Sum(x => x.Matches) == a.Matches,
            "Weekly buckets must sum exactly to confirmed matches.");
        // ARRANGE: a new deck with two losses followed by three wins.
        var streakRows = new[] {
            new MatchPerformanceAnalytics.Entry("s1",now.AddMinutes(-4),"Deck C","","Brawl","loss"),
            new MatchPerformanceAnalytics.Entry("s2",now.AddMinutes(-3),"Deck C","","Brawl","loss"),
            new MatchPerformanceAnalytics.Entry("s3",now.AddMinutes(-2),"Deck C","","Brawl","win"),
            new MatchPerformanceAnalytics.Entry("s4",now.AddMinutes(-1),"Deck C","","Brawl","win"),
            new MatchPerformanceAnalytics.Entry("s5",now,"Deck C","","Brawl","win")
        };
        var streakReport = MatchPerformanceAnalytics.Build(streakRows,"Deck C",0,now);
        RegressionExpect.That(streakReport.BestWinStreak == 3 && streakReport.WorstLossStreak == 2,
            "Streak extremes must not be confused with the active streak.");
        RegressionExpect.That(streakReport.CurrentStreak == 3,
            "Latest win streak must reflect the chronological last match.");
        // Commander colors belong to the user's saved deck, not the opponent.
        // Stable native IDs take precedence over ambiguous deck-name matches.
        var ownIdentity = MatchPerformanceAnalytics.ByOwnColorIdentity(new[] {
            new MatchPerformanceAnalytics.Entry("color-win",now,"Renamed deck",
                "native-b","Arena Brawl","win"),
            new MatchPerformanceAnalytics.Entry("color-win",now,"Renamed deck",
                "native-b","Arena Brawl","win"),
            new MatchPerformanceAnalytics.Entry("color-loss",now,"Deck A",
                "native-c","Arena Brawl","loss"),
            new MatchPerformanceAnalytics.Entry("colorless",now,"Deck C",
                "native-colorless","Arena Brawl","win"),
            new MatchPerformanceAnalytics.Entry("unidentified",now,"Missing",
                "unknown-id","Arena Brawl","loss"),
            new MatchPerformanceAnalytics.Entry("unconfirmed",now,"Deck A",
                "native-b","Arena Brawl","unknown")
        }, new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase) {
            ["native-b"] = "BR", ["native-c"] = "GU",
            ["native-colorless"] = "C", ["name:Deck A"] = "W"
        });
        RegressionExpect.That(ownIdentity.Sum(x => x.Matches) == 4 &&
            ownIdentity.Single(x => x.Format == "BR").Wins == 1 &&
            ownIdentity.Single(x => x.Format == "GU").Losses == 1 &&
            ownIdentity.Single(x => x.Format == "C").Wins == 1 &&
            ownIdentity.Single(x => x.Format == "Unknown").Losses == 1 &&
            !ownIdentity.Any(x => x.Format == "W"),
            "Native deck IDs must determine verified color identity; duplicate, unknown " +
            "and missing results must not invent wins or silently become colorless.");
        // Native wins/losses remain authoritative even when a matching legacy
        // record carries an explicit opponent-color annotation missing from
        // the native copy. Unknown is never guessed from your own colors.
        RegressionExpect.That(
            MatchPerformanceAnalytics.PreferExplicitOpponentColors("", "WU") == "WU" &&
            MatchPerformanceAnalytics.PreferExplicitOpponentColors("BR", "WU") == "BR" &&
            MatchPerformanceAnalytics.PreferExplicitOpponentColors("", "") == "",
            "Native-first match reconciliation must retain explicitly recorded " +
            "opponent colors from historical duplicate rows without inventing colors.");
        var shortRange = MatchPerformanceAnalytics.Build(rows,"Deck A",1,now);
        RegressionExpect.That(shortRange.Matches == 1, "Date range should not include older results.");
    }
}
