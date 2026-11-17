using ArcaneVault.Next.Desktop;
using Microsoft.Data.Sqlite;
using System.Text.Json;

internal static class ArenaLogAssignmentChecks
{
    public static void Run(string folder)
    {
        var local = Path.Combine(folder, "arena-submission-check");
        Directory.CreateDirectory(local);
        var log = Path.Combine(local, "Player.log");
        var dbPath = Path.Combine(local, "match.sqlite3");
        var t = new DateTimeOffset(2026,11,17,9,0,0,TimeSpan.Zero);

        static string Event(DateTimeOffset t, string tag, object payload) =>
            "[UnityCrossThreadLogger] " +
            t.ToString("MM/dd/yyyy hh:mm:ss tt", System.Globalization.CultureInfo.GetCultureInfo("en-US")) +
            " ==> " + tag + " " + JsonSerializer.Serialize(payload);

        static object Match(string matchId, string state, string playerId) =>
            new {
                matchGameRoomStateChangedEvent = new {
                    gameRoomInfo = new {
                        stateType = state,
                        gameRoomConfig = new {
                            matchId,
                            reservedPlayers = new[] { new { userId = playerId, playerName = "Tester", teamId = 1 } }
                        },
                        finalMatchResult = state == "MatchGameRoomStateType_MatchCompleted"
                            ? new { matchId, resultList = new[] {
                                new { result = "ResultType_WinLoss",
                                    scope = "MatchScope_Match", winningTeamId = 1 }
                            }}
                            : null
                    }
                }
            };

        var id1 = "verified-arena-id-001";
        var submission = JsonSerializer.Serialize(new { jsonrpc = "2.0", method = "EventSetDeckV3",
            @params = new { DeckId = id1, EventName = "Brawl_Ladder" } });
        var auth = new { authenticateResponse = new { clientId = "self-id", screenName = "Tester" } };
        File.WriteAllLines(log, new [] {
            Event(t, "Auth", auth),
            Event(t.AddMinutes(1), "EventSetDeckV3", new { request = submission }),
            Event(t.AddMinutes(2), "match", Match("match-from-request", "MatchGameRoomStateType_Playing", "self-id")),
            Event(t.AddMinutes(3), "match", Match("match-from-request", "MatchGameRoomStateType_MatchCompleted", "self-id")),
            // CourseDeck carries the exact deck submitted to a queue,
            // even if the match-room configuration omits the deckId.
            Event(t.AddMinutes(4), "CourseDeck", new { CourseDeck = new { },
                CourseDeckSummary = new { DeckId = "course-deck-002" } }),
            Event(t.AddMinutes(5), "match", Match("match-from-course", "MatchGameRoomStateType_Playing", "self-id")),
            Event(t.AddMinutes(6), "match", Match("match-from-course", "MatchGameRoomStateType_MatchCompleted", "self-id")),
            // A passive CourseDeckSummary is not evidence of submitting
            // another deck for play and may not replace the active queue.
            Event(t.AddMinutes(7), "inventory", new {
                CourseDeckSummary = new { DeckId = "unrelated-inventory-999" } }),
            Event(t.AddMinutes(8), "match", Match("match-after-inventory", "MatchGameRoomStateType_Playing", "self-id")),
            Event(t.AddMinutes(9), "match", Match("match-after-inventory", "MatchGameRoomStateType_MatchCompleted", "self-id"))
        });
        using (var tracker = new ArenaMatchTracker(dbPath))
        {
            tracker.SetKnownSavedDecks(new [] {
                (ArenaId: id1, Name: "Known Brawl Deck", NativeId: "native-brawl-001"),
                (ArenaId: "course-deck-002", Name: "Course Submitted Deck", NativeId: "native-course-002")
            });
            tracker.Configure(true,log);
            tracker.Tick();
        }
        using (var db = new SqliteConnection("Data Source=" + dbPath))
        {
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT match_id,deck_id,native_deck_id,result FROM matches ORDER BY match_id";
            using var r = cmd.ExecuteReader();
            var found = new Dictionary<string,(string ArenaId,string NativeId,string Result)>();
            while (r.Read())
                found[r.GetString(0)] = (r.GetString(1), r.GetString(2), r.GetString(3));
            Assert(found.Count == 3, "Arena queue-to-match correlation must not lose or duplicate matches.");
            Assert(found["match-from-request"] == (id1,"native-brawl-001","win"),
                "Double-encoded EventSetDeckV3 request must link to subsequent match.");
            Assert(found["match-from-course"] == ("course-deck-002","native-course-002","win"),
                "CourseDeck queue submission must link without match room deckId.");
            Assert(found["match-after-inventory"] == ("course-deck-002","native-course-002","win"),
                "Passive CourseDeckSummary must not replace the actual submitted deck.");
        }
        // The user's live Arena event-shape report includes V3 messages
        // with the label on one line and a JSON envelope on the next.
        // This is distinct from the inline synthetic fixture above.
        var splitDirectory = Path.Combine(local, "split-envelope");
        Directory.CreateDirectory(splitDirectory);
        var splitLog = Path.Combine(splitDirectory, "Player.log");
        var splitDb = Path.Combine(splitDirectory, "match.sqlite3");
        var splitId = "live-shape-verified-id";
        File.WriteAllLines(splitLog, new [] {
            Event(t, "Auth", auth),
            "[UnityCrossThreadLogger] 11/17/2026 09:01:00 AM ==> EventSetDeckV3",
            JsonSerializer.Serialize(new { request = JsonSerializer.Serialize(new {
                Summary = new { DeckId = splitId, Name = "Verified Deck" },
                Deck = new { MainDeck = new int[] { 1,2,3 } }
            })}),
            "[UnityCrossThreadLogger] 11/17/2026 09:02:00 AM ==> matchGameRoomStateChangedEvent",
            JsonSerializer.Serialize(Match("match-from-split-1",
                "MatchGameRoomStateType_Playing","self-id")),
            "[UnityCrossThreadLogger] 11/17/2026 09:03:00 AM ==> matchGameRoomStateChangedEvent",
            JsonSerializer.Serialize(Match("match-from-split-1",
                "MatchGameRoomStateType_MatchCompleted","self-id"))
        });
        using (var tracker = new ArenaMatchTracker(splitDb))
        {
            tracker.SetKnownSavedDecks(new [] {
                (ArenaId: splitId, Name: "Verified Deck", NativeId: "native-split")
            });
            tracker.Configure(true, splitLog);
            tracker.Tick();
        }
        using (var db = new SqliteConnection("Data Source=" + splitDb))
        {
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT deck_id,native_deck_id,result FROM matches WHERE match_id='match-from-split-1'";
            using var r = cmd.ExecuteReader();
            Assert(r.Read() && r.GetString(0) == splitId &&
                r.GetString(1) == "native-split" && r.GetString(2) == "win",
                "Separated Arena V3 header and escaped JSON request must carry the deck ID into the match.");
        }
        // A real submission without a saved mapping must STILL preserve the
        // Arena ID, allowing a reusable mapping to be created by the user.
        var unknownLog = Path.Combine(local,"unmapped","Player.log");
        Directory.CreateDirectory(Path.GetDirectoryName(unknownLog)!);
        var missing = "unmapped-arena-id-777";
        var rawRequest = JsonSerializer.Serialize(new { Summary = new { DeckId = missing } });
        File.WriteAllLines(unknownLog,new [] {
            Event(t,"Auth",auth),
            Event(t.AddMinutes(1),"EventSetDeckV2",new { request = rawRequest }),
            Event(t.AddMinutes(2),"match", Match("unknown-deck-1","MatchGameRoomStateType_MatchCompleted","self-id"))
        });
        using (var tracker = new ArenaMatchTracker(Path.Combine(local,"unknown-match.sqlite3")))
        {
            tracker.Configure(true, unknownLog);
            tracker.Tick();
        }
        using (var db = new SqliteConnection("Data Source=" + Path.Combine(local,"unknown-match.sqlite3")))
        {
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT deck_id,deck_name,result FROM matches WHERE match_id='unknown-deck-1'";
            using var r = cmd.ExecuteReader();
            Assert(r.Read() && r.GetString(0) == missing &&
                r.GetString(1) == "" && r.GetString(2) == "win",
                "Unmapped submitted Arena IDs must remain visible, not disappear.");
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
