using ArcaneVault.Next.Desktop;
using Microsoft.Data.Sqlite;

internal static class MatchAssignmentChecks
{
    public static void Run(string folder)
    {
        string path = Path.Combine(folder, "match-assignments.sqlite3");
        // Deliberately reproduce a pre-v0.9.134 local match database.
        using (var db = new SqliteConnection("Data Source=" + path))
        {
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE matches (
                    match_id TEXT PRIMARY KEY,
                    played_utc TEXT NOT NULL,
                    result TEXT NOT NULL,
                    result_source TEXT NOT NULL,
                    format TEXT NOT NULL DEFAULT '',
                    deck_id TEXT NOT NULL DEFAULT '',
                    deck_name TEXT NOT NULL DEFAULT '',
                    deck_version TEXT NOT NULL DEFAULT '',
                    source_log TEXT NOT NULL DEFAULT '',
                    note TEXT NOT NULL DEFAULT '',
                    game_wins INTEGER NOT NULL DEFAULT 0,
                    game_losses INTEGER NOT NULL DEFAULT 0
                );
                """;
            cmd.ExecuteNonQuery();
        }

        using var history = new MatchHistory(path);
        using (var db = new SqliteConnection("Data Source=" + path))
        {
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "PRAGMA table_info(matches)";
            using var reader = cmd.ExecuteReader();
            bool migrated = false;
            while (reader.Read())
                if (reader.GetString(1) == "native_deck_id") migrated = true;
            Assert(migrated, "Stable native deck identity migration must preserve legacy history.");
        }

        var played = DateTimeOffset.UtcNow.ToString("O");
        Assert(history.Add(new MatchHistory.MatchRow("manual-1", played, "win",
            "arena_match_result", "Historic", "", "", "", "Player.log",
            "Deck not identified from match log; assign manually.", 1, 0)),
            "Fixture record insert failed.");
        Assert(history.Update("manual-1", null, "Exact Saved Deck", null,
            "Manual deck assignment", "native-saved-123"),
            "Assignment must update an existing match.");
        Assert(!history.Update("missing-id", null, "Exact Saved Deck", null,
            "Should not create", "native-saved-123"),
            "An unknown match must never create a new win.");

        using (var db = new SqliteConnection("Data Source=" + path))
        {
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*),MAX(result),MAX(deck_name),MAX(native_deck_id) FROM matches";
            using var reader = cmd.ExecuteReader();
            Assert(reader.Read() && reader.GetInt64(0) == 1 &&
                reader.GetString(1) == "win" &&
                reader.GetString(2) == "Exact Saved Deck" &&
                reader.GetString(3) == "native-saved-123",
                "Manual match attribution must not duplicate wins or change results.");
        }

        Assert(history.Add(new MatchHistory.MatchRow("auto-1", played, "loss",
            "arena_match_result", "Historic", "arena-id-123", "", "",
            "Player.log", "Deck not identified", 0, 1)), "Auto fixture insert failed.");
        Assert(history.Add(new MatchHistory.MatchRow("ambiguous-1", played, "win",
            "arena_match_result", "Historic", "arena-id-ambiguous", "", "",
            "Player.log", "Deck not identified", 1, 0)), "Ambiguous fixture insert failed.");
        using (var tracker = new ArenaMatchTracker(path))
            tracker.SetKnownSavedDecks(new [] {
                (ArenaId: "arena-id-123", Name: "Mapped Existing Deck"),
                (ArenaId: "arena-id-ambiguous", Name: "First Saved Deck"),
                (ArenaId: "arena-id-ambiguous", Name: "Second Saved Deck")
            });
        using (var db = new SqliteConnection("Data Source=" + path))
        {
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT deck_name FROM matches WHERE match_id='auto-1'";
            Assert((string?)cmd.ExecuteScalar() == "Mapped Existing Deck",
                "An exact saved Arena deck ID should safely backfill a prior unknown deck.");
            cmd.CommandText = "SELECT deck_name FROM matches WHERE match_id='ambiguous-1'";
            Assert((string?)cmd.ExecuteScalar() == "",
                "Duplicated Arena deck IDs must never be assigned speculatively.");
            cmd.CommandText = "SELECT COUNT(*) FROM matches";
            Assert(Convert.ToInt64(cmd.ExecuteScalar()) == 3,
                "ID reconciliation must update in place without adding matches.");
        }
        // Persist an explicit mapping, then verify a new tracker instance
        // restores it and backfills an existing result without duplication.
        Assert(history.Add(new MatchHistory.MatchRow("remember-1", played, "win",
            "arena_match_result", "Historic", "arena-persistent-77", "", "",
            "Player.log", "No mapping yet", 1, 0)),
            "Persistent mapping fixture insert failed.");
        Assert(history.RememberDeckMapping("arena-persistent-77", "saved-persisted",
            "Persistent Commander"), "Mapping should be saved");
        Assert(!history.RememberDeckMapping("arena-persistent-77", "other-saved",
            "Wrong Commander"), "Conflicting mapping must not overwrite without consent");
        using (var fresh = new ArenaMatchTracker(path))
            fresh.SetKnownSavedDecks(new [] {
                (ArenaId:"", Name:"Persistent Commander", NativeId:"saved-persisted")
            });
        using (var db = new SqliteConnection("Data Source=" + path))
        {
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT native_deck_id FROM matches WHERE match_id='remember-1'";
            Assert((string?)cmd.ExecuteScalar() == "saved-persisted",
                "Persisted mapping must survive a new tracker and backfill exact ID");
            cmd.CommandText = "SELECT COUNT(*) FROM matches";
            Assert(Convert.ToInt64(cmd.ExecuteScalar()) == 4,
                "Persistent backfill must not duplicate existing matches");
        }
        // A saved-deck collision must block automatic attribution even when
        // the Arena ID is otherwise present and both names are similar.
        Assert(history.Add(new MatchHistory.MatchRow("collision-1", played, "loss",
            "arena_match_result", "Historic", "arena-collision-1", "", "",
            "Player.log", "No mapping", 0, 1)), "Collision fixture insert failed.");
        using (var fresh = new ArenaMatchTracker(path))
            fresh.SetKnownSavedDecks(new [] {
                (ArenaId:"arena-collision-1", Name:"Commander One", NativeId:"saved-one"),
                (ArenaId:"arena-collision-1", Name:"Commander Two", NativeId:"saved-two")
            });
        using (var db = new SqliteConnection("Data Source=" + path))
        {
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT native_deck_id FROM matches WHERE match_id='collision-1'";
            Assert((string?)cmd.ExecuteScalar() == "",
                "Ambiguous Arena IDs must remain unassigned");
        }

    }

    private static void Assert(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }
}
