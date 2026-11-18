using System.Text.Json;
using ArcaneVault.Next.Desktop;
using Microsoft.Data.Sqlite;

internal static class ArenaQueueRankChecks
{
    public static void Run(string folder)
    {
        static void Assert(bool ok, string reason)
        {
            if(!ok) throw new InvalidOperationException(reason);
        }
        Assert(ArenaQueueInfo.Label("Brawl_Ladder") == "Competitive Brawl",
            "Ranked/Competitive Brawl must remain distinct from regular Brawl.");
        Assert(ArenaQueueInfo.Label("Brawl_Play") == "Brawl",
            "Unranked Brawl must remain regular Brawl.");
        Assert(ArenaQueueInfo.Label("Traditional_Standard_Ranked") == "Standard Ranked",
            "Ranked Constructed queue must remain distinguishable.");
        Assert(ArenaQueueInfo.Label("mystery-event-88") == "mystery-event-88",
            "Unrecognized event IDs must not be guessed.");
        using var document=JsonDocument.Parse("""
            {"constructedClass":"Gold","constructedLevel":2,
             "limitedClass":"Platinum","limitedLevel":3}
            """);
        var snap=ArenaRankInfo.TryRead(document.RootElement, "rank_response");
        Assert(snap is not null && snap.ConstructedClass=="Gold" &&
            snap.ConstructedLevel==2 && snap.LimitedClass=="Platinum" &&
            snap.LimitedLevel==3, "Rank payload must preserve both lanes.");
        var local=Path.Combine(folder,"arena-ranked-queue-check");
        Directory.CreateDirectory(local);
        var db=Path.Combine(local,"rank-history.sqlite3");
        var time=new DateTimeOffset(2026,11,18,12,0,0,TimeSpan.Zero);
        using(var store=new MatchHistory(db))
        {
            Assert(store.AddRankSnapshot(time,snap!),"Rank snapshot should save.");
            Assert(!store.AddRankSnapshot(time,snap!),"Timestamp duplicate must be idempotent.");
            var read=store.LatestRank();
            Assert(read is not null && read.ConstructedClass=="Gold" &&
                read.LimitedLevel==3, "Saved rank snapshots must survive readback.");
        }
    }
}
