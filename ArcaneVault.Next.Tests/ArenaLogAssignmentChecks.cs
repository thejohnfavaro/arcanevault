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
                            reservedPlayers = new[] { new { userId = playerId, playerName = "Tester", teamId = 1, seatId = 1 } }
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
        // A multi-deck CourseDeck response is inventory, not proof that
        // the first listed deck was submitted to matchmaking.
        var inventoryLog = Path.Combine(local, "inventory-only", "Player.log");
        Directory.CreateDirectory(Path.GetDirectoryName(inventoryLog)!);
        var inventoryDb = Path.Combine(local, "inventory-only.sqlite3");
        File.WriteAllLines(inventoryLog, new [] {
            Event(t, "Auth", auth),
            Event(t.AddMinutes(1), "CourseDeck", new {
                decks = new [] {
                    new { CourseDeckSummary = new { DeckId = "inventory-only-1" } },
                    new { CourseDeckSummary = new { DeckId = "inventory-only-2" } }
                }
            }),
            Event(t.AddMinutes(2), "match", Match("inventory-match",
                "MatchGameRoomStateType_Playing", "self-id")),
            Event(t.AddMinutes(3), "match", Match("inventory-match",
                "MatchGameRoomStateType_MatchCompleted", "self-id"))
        });
        using (var tracker = new ArenaMatchTracker(inventoryDb))
        {
            tracker.Configure(true, inventoryLog);
            tracker.Tick();
        }
        using (var db = new SqliteConnection("Data Source=" + inventoryDb))
        {
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT deck_id,result FROM matches WHERE match_id='inventory-match'";
            using var reader = cmd.ExecuteReader();
            Assert(reader.Read() && reader.GetString(0) == "" && reader.GetString(1) == "win",
                "A deck inventory must never guess the active deck");
        }
        // Arena also emits 24-hour log headings; they must be timestamped.
        var clockLog = Path.Combine(local, "24h", "Player.log");
        Directory.CreateDirectory(Path.GetDirectoryName(clockLog)!);
        var clockDb = Path.Combine(local, "24h.sqlite3");
        File.WriteAllLines(clockLog, new [] {
            Event(t, "Auth", auth),
            "[UnityCrossThreadLogger] 11/17/2026 09:01:00 ==> EventSetDeckV3 " +
                JsonSerializer.Serialize(new { request = JsonSerializer.Serialize(
                    new { Summary = new { DeckId = "24h-deck" } }) }),
            Event(t.AddMinutes(2), "match", Match("24h-match",
                "MatchGameRoomStateType_Playing", "self-id")),
            Event(t.AddMinutes(3), "match", Match("24h-match",
                "MatchGameRoomStateType_MatchCompleted", "self-id"))
        });
        using (var tracker = new ArenaMatchTracker(clockDb))
        {
            tracker.Configure(true, clockLog);
            tracker.Tick();
        }
        using (var db = new SqliteConnection("Data Source=" + clockDb))
        {
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT deck_id FROM matches WHERE match_id='24h-match'";
            Assert((string?)cmd.ExecuteScalar() == "24h-deck",
                "24-hour event headers must preserve deck submission timestamps");
        }
        // Detailed GRE batches carry high-value mechanics while the match
        // result schema and native collection remain unchanged. No usernames,
        // authentication material, chat, or unknown log data may be archived.
        var telemetryLog = Path.Combine(local, "telemetry", "Player.log");
        Directory.CreateDirectory(Path.GetDirectoryName(telemetryLog)!);
        var telemetryDb = Path.Combine(local, "telemetry.sqlite3");
        var stateOne = new {
            turnInfo = new { turnNumber = 1, phase = "Phase_Main1",
                activePlayer = 1, activePlayerSeatId = 1 },
            gameInfo = new { gameNumber = 1, stage = "GameStage_Play" },
            players = new[] { new { systemSeatId = 1, lifeTotal = 20,
                mulliganCount = 1, handCount = 6,
                screenName = "PRIVATE_NAME", token = "PRIVATE_TOKEN" } },
            zones = new[] { new { zoneId = 7, type = "ZoneType_Hand", ownerSeatId = 1,
                objectInstanceIds = new[] { 22 } } },
            gameObjects = new[] { new { instanceId = 22, grpId = 12345,
                zoneId = 7, ownerSeatId = 1, controllerSeatId = 1,
                privateNote = "PRIVATE_TOKEN" } },
            annotations = new[] { new { id = 8, type = new[] { "AnnotationType_ZoneTransfer" },
                affectedIds = new[] { 22 }, details = new[] {
                    new { key = "category", valueString = new[] { "PlayLand" } }
                } } }
        };
        var stateTwo = new {
            turnInfo = new { turnNumber = 2, phase = "Phase_Beginning", activePlayer = 2 },
            gameInfo = new { gameNumber = 1, stage = "GameStage_Play" },
            annotations = new[] { new { id = 9, type = new[] { "AnnotationType_DamageDealt" },
                affectedIds = new[] { 40 }, details = new object[0] } }
        };
        var greBatch = new { greToClientEvent = new { greToClientMessages = new object[] {
            new { type = "GREMessageType_GameStateMessage", gameStateMessage = (object)stateOne },
            new { type = "GREMessageType_GameStateMessage", gameStateMessage = (object)stateTwo }
        } } };
        File.WriteAllLines(telemetryLog, new[] {
            Event(t.AddSeconds(-2), "Auth", new {
                authenticateResponse = new { clientId = "self-id", screenName = "Tester" }
            }),
            Event(t, "greToClientEvent", greBatch), // outside match: ignore
            Event(t.AddSeconds(10), "matchGameRoomStateChangedEvent",
                Match("telemetry-match", "MatchGameRoomStateType_Playing", "self-id")),
            Event(t.AddSeconds(15), "greToClientEvent", greBatch),
            Event(t.AddSeconds(16), "greToClientEvent", greBatch), // replay: deduplicate
            Event(t.AddSeconds(20), "matchGameRoomStateChangedEvent",
                Match("telemetry-match", "MatchGameRoomStateType_MatchCompleted", "self-id")),
            Event(t.AddSeconds(21), "greToClientEvent", greBatch) // after match: ignore
        });
        using (var tracker = new ArenaMatchTracker(telemetryDb))
        {
            tracker.Configure(true, telemetryLog);
            tracker.Tick();
            tracker.Tick();
            Assert(JsonSerializer.Serialize(tracker.Snapshot()).Contains(
                "\"archivedTelemetryEvents\":2"),
                "Batched GRE telemetry should be counted exactly once.");
        }
        using (var db = new SqliteConnection("Data Source=" + telemetryDb))
        {
            db.Open();
            using var count = db.CreateCommand();
            count.CommandText = "SELECT COUNT(*), MIN(turn_number), MAX(turn_number) " +
                "FROM arena_match_telemetry WHERE match_id='telemetry-match'";
            using (var row = count.ExecuteReader())
            {
                Assert(row.Read() && row.GetInt32(0) == 2 &&
                    row.GetInt32(1) == 1 && row.GetInt32(2) == 2,
                    "Both GRE messages must be stored, with no duplicates outside match bounds.");
            }
            using var payload = db.CreateCommand();
            payload.CommandText = "SELECT payload_json FROM arena_match_telemetry " +
                "WHERE turn_number=1";
            var json = (string?)payload.ExecuteScalar() ?? "";
            Assert(json.Contains("ZoneType_Hand") && json.Contains("12345") &&
                json.Contains("AnnotationType_ZoneTransfer") &&
                json.Contains("mulliganCount") && json.Contains("handCount") &&
                !json.Contains("PRIVATE_NAME") &&
                !json.Contains("PRIVATE_TOKEN") &&
                !json.Contains("screenName") && !json.Contains("privateNote"),
                "Persist useful game mechanics without any personally identifying log data.");
        }

        // Pin the known synthetic match to one exact saved deck before
        // evaluating deck-scoped analysis. A different deck must never inherit
        // this win from name similarity or unrelated telemetry.
        using (var scopedDb = new SqliteConnection("Data Source=" + telemetryDb))
        {
            scopedDb.Open();
            using var setDeck = scopedDb.CreateCommand();
            setDeck.CommandText = "UPDATE matches SET native_deck_id='test-native-a' " +
                "WHERE match_id='telemetry-match'";
            Assert(setDeck.ExecuteNonQuery() == 1,
                "Synthetic telemetry match should exist before deck-scoped analysis.");
        }
        using (var telemetryHistory = new MatchHistory(telemetryDb))
        {
            var coverage = telemetryHistory.ReadTelemetryOverview();
            Assert(coverage.RecordedMatches == 1 && coverage.MatchesWithTelemetry == 1 &&
                coverage.ArchivedEvents == 2 && coverage.Recent.Count == 1 &&
                coverage.Recent[0].GamesWithNumbers == 1 &&
                coverage.Recent[0].HighestTurn == 2,
                "Analytics coverage must count deduplicated GRE events and observed turns.");
            var gameplay = telemetryHistory.ReadGameplayObservation();
            Assert(gameplay.GamesWithTurnData == 1 &&
                gameplay.AverageHighestObservedTurn == 2 &&
                gameplay.GamesWithMulliganData == 1 &&
                gameplay.SeatsWithMulligans == 1 &&
                gameplay.HighestObservedMulligans == 1,
                "Game observations must deduplicate repeated player snapshots and distinguish turns from match lengths.");
            var deckCoverage = telemetryHistory.ReadTelemetryOverviewForDeck("test-native-a");
            var otherCoverage = telemetryHistory.ReadTelemetryOverviewForDeck("test-native-b");
            var deckGameplay = telemetryHistory.ReadGameplayObservationForDeck("test-native-a");
            var otherGameplay = telemetryHistory.ReadGameplayObservationForDeck("test-native-b");
            Assert(deckCoverage.RecordedMatches == 1 &&
                deckCoverage.MatchesWithTelemetry == 1 && deckCoverage.ArchivedEvents == 2 &&
                otherCoverage.RecordedMatches == 0 &&
                otherCoverage.MatchesWithTelemetry == 0 && otherCoverage.ArchivedEvents == 0 &&
                deckGameplay.GamesWithTurnData == 1 &&
                deckGameplay.GamesWithMulliganData == 1 &&
                otherGameplay.GamesWithTurnData == 0 &&
                otherGameplay.GamesWithMulliganData == 0,
                "All gameplay telemetry totals and observed mulligans must respect exact saved deck scope.");
            var playDraw = telemetryHistory.ReadVerifiedPlayDraw();
            Assert(playDraw.OnPlayWins == 1 && playDraw.OnPlayLosses == 0 &&
                playDraw.OnDrawWins == 0 && playDraw.OnDrawLosses == 0 &&
                playDraw.Unattributed == 0,
                "Authenticated local seat and turn-one GRE active seat must identify on-play result.");
            var scoped = telemetryHistory.ReadVerifiedPlayDrawForDeck("test-native-a");
            var unrelated = telemetryHistory.ReadVerifiedPlayDrawForDeck("test-native-b");
            Assert(scoped.OnPlayWins == 1 && scoped.Unattributed == 0 &&
                unrelated.OnPlayWins == 0 && unrelated.OnDrawWins == 0 &&
                unrelated.Unattributed == 0,
                "Deck-scoped play/draw must require an exact stable saved deck ID.");
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
