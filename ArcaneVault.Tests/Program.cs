using ArcaneVault.Desktop;
using System.Text.Json;

var temp = Path.Combine(Path.GetTempPath(), "ArcaneVaultTest_" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temp);
var file = Path.Combine(temp,"Player.log");
var db = Path.Combine(temp,"test.sqlite3");
try
{
    // Explicit authenticated user and team mapping; Bo3 contains two game records plus one match record.
    File.WriteAllText(file, """
[UnityCrossThreadLogger]11/05/2026 10:00:00 AM
{"authenticateResponse":{"clientId":"local-user-1","screenName":"Tester"}}
[UnityCrossThreadLogger]11/05/2026 10:01:00 AM
{"matchGameRoomStateChangedEvent":{"gameRoomInfo":{"stateType":"MatchGameRoomStateType_Playing","gameRoomConfig":{"matchId":"match-001","eventId":"Constructed_BestOf3","reservedPlayers":[{"userId":"local-user-1","teamId":2},{"userId":"opponent","teamId":1}]}}}}
[UnityCrossThreadLogger]11/05/2026 10:20:00 AM
{"matchGameRoomStateChangedEvent":{"gameRoomInfo":{"stateType":"MatchGameRoomStateType_MatchCompleted","finalMatchResult":{"matchId":"match-001","resultList":[{"scope":"MatchScope_Game","result":"ResultType_WinLoss","winningTeamId":2},{"scope":"MatchScope_Game","result":"ResultType_WinLoss","winningTeamId":1},{"scope":"MatchScope_Game","result":"ResultType_WinLoss","winningTeamId":2},{"scope":"MatchScope_Match","result":"ResultType_WinLoss","winningTeamId":2}]}}}}
""" + "\n");
    using var tracker = new ArenaMatchTracker(db);
    tracker.Configure(true,file);
    tracker.SetCurrentDeck("Gwenom","v3");
    tracker.Tick();
    // Re-read the same events from scratch: same match id must not duplicate the record.
    tracker.Configure(false,file); tracker.Configure(true,file); tracker.Tick();
    var output = JsonSerializer.Serialize(tracker.Snapshot());
    if (!output.Contains("\"wins\":1") || !output.Contains("\"gameWins\":2") || !output.Contains("\"gameLosses\":1") || !output.Contains("\"total\":1"))
        throw new Exception("Match parser failed known-result or deduplication fixture: " + output);
    // Match result with no known authenticated seat is unknown, never a fabricated loss.
    File.AppendAllText(file,"""
[UnityCrossThreadLogger]11/05/2026 11:00:00 AM
{"matchGameRoomStateChangedEvent":{"gameRoomInfo":{"stateType":"MatchGameRoomStateType_MatchCompleted","finalMatchResult":{"matchId":"match-unknown","resultList":[{"scope":"MatchScope_Match","result":"ResultType_WinLoss","winningTeamId":1}]}}}}
""" + "\n");
    tracker.Tick();
    output=JsonSerializer.Serialize(tracker.Snapshot());
    if(!output.Contains("\"total\":2") || !output.Contains("\"awaitingReview\":1"))
        throw new Exception("Unknown identity fixture failed: " + output);
    // Arena deck selection event + matching deck ID should auto-label an otherwise unassigned match.
    File.AppendAllText(file,"""
[UnityCrossThreadLogger]11/05/2026 12:00:00 PM
{"EventSetDeckV2":{"DeckId":"arena-deck-7","Name":"Tinybones Discard"}}
[UnityCrossThreadLogger]11/05/2026 12:01:00 PM
{"matchGameRoomStateChangedEvent":{"gameRoomInfo":{"stateType":"MatchGameRoomStateType_Playing","gameRoomConfig":{"matchId":"match-auto","deckId":"arena-deck-7","reservedPlayers":[{"userId":"local-user-1","teamId":2}]}}}}
[UnityCrossThreadLogger]11/05/2026 12:10:00 PM
{"matchGameRoomStateChangedEvent":{"gameRoomInfo":{"stateType":"MatchGameRoomStateType_MatchCompleted","finalMatchResult":{"matchId":"match-auto","resultList":[{"scope":"MatchScope_Match","result":"ResultType_WinLoss","winningTeamId":2}]}}}}
""" + "\n");
    tracker.SetCurrentDeck("", "");
    tracker.Tick();
    output=JsonSerializer.Serialize(tracker.Snapshot());
    if(!output.Contains("Tinybones Discard") || !output.Contains("arena_deck_id") || !output.Contains("\"total\":3"))
        throw new Exception("Automatic deck assignment fixture failed: " + output);
    Console.WriteLine("PASS: deck ID matching auto-assigns match.");
    Console.WriteLine("PASS: win, game/match separation, duplicate IDs, unknown result.");
}
finally {try{Directory.Delete(temp,true);}catch{}}
