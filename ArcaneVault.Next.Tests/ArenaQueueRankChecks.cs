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
        // Exercise the real text scanner and the event-correlation path,
        // not only isolated rank/format parsing utilities.
        var log=Path.Combine(local, "Player.log");
        static string Event(DateTimeOffset time, string name, object data) =>
            "[UnityCrossThreadLogger] " +
            time.ToString("MM/dd/yyyy hh:mm:ss tt",
                System.Globalization.CultureInfo.GetCultureInfo("en-US")) +
            " ==> " + name + " " + JsonSerializer.Serialize(data);
        var submit=JsonSerializer.Serialize(new {
            @params=new {DeckId="competitive-brawl-deck",EventName="Brawl_Ladder"}
        });
        var match=new {
            matchGameRoomStateChangedEvent=new {
                gameRoomInfo=new {
                    stateType="MatchGameRoomStateType_MatchCompleted",
                    gameRoomConfig=new {
                        matchId="queue-rank-real-match",
                        reservedPlayers=new[] {new {userId="me",playerName="Tester",teamId=1}}
                    },
                    finalMatchResult=new {
                        matchId="queue-rank-real-match",
                        resultList=new[] {new {
                            result="ResultType_WinLoss",
                            scope="MatchScope_Match",winningTeamId=1
                        }}
                    }
                }
            }
        };
        File.WriteAllLines(log, new []{
            Event(time,"Auth",new {authenticateResponse=new {
                clientId="me",screenName="Tester"}}),
            Event(time.AddMinutes(1),"RankGetCombinedRankInfo",new {
                constructedClass="Gold",constructedLevel=2,
                limitedClass="Platinum",limitedLevel=3
            }),
            Event(time.AddMinutes(2),"EventSetDeckV3",new {request=submit}),
            Event(time.AddMinutes(3),"matchGameRoomStateChangedEvent",match)
        });
        using(var tracker=new ArenaMatchTracker(db))
        {
            tracker.SetKnownSavedDecks(new [] {
                (ArenaId:"competitive-brawl-deck", Name:"Borg", NativeId:"native-borg")
            });
            tracker.Configure(true,log);
            tracker.Tick();
        }
        using(var checkDb=new SqliteConnection("Data Source="+db))
        {
            checkDb.Open();
            using var cmd=checkDb.CreateCommand();
            cmd.CommandText="SELECT format,result FROM matches WHERE match_id='queue-rank-real-match'";
            using var reader=cmd.ExecuteReader();
            Assert(reader.Read() && reader.GetString(0)=="Brawl_Ladder" &&
                reader.GetString(1)=="win",
                "Escaped queue submission must preserve competitive Brawl mode and correct win.");
        }
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
