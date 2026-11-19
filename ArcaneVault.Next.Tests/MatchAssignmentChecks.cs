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
        // Replaying a match with newly discovered deck evidence must enrich
        // exactly that row, without changing a recorded W/L or manual choice.
        Assert(history.Add(new MatchHistory.MatchRow("late-id-1", played, "loss",
            "arena_match_result", "", "", "", "", "Player.log",
            "No Arena deck ID in match log", 0, 1)), "Late-ID fixture insert failed");
        Assert(history.EnrichUnassignedMatch("late-id-1", "arena-late-1",
            "native-late-1", "Late Matched Deck", "", "Deck assignment: verified ID"),
            "A previously unassigned imported match must accept an exact later deck ID");
        Assert(!history.EnrichUnassignedMatch("late-id-1", "wrong-id",
            "", "Wrong Deck", "", "Incorrect overwrite"),
            "An enriched match must not be attributed again");
        Assert(!history.EnrichUnassignedMatch("manual-1", "arena-wrong",
            "", "Wrong Deck", "", "Incorrect overwrite"),
            "Manual choices must be protected against later automatic evidence");
        using (var db = new SqliteConnection("Data Source=" + path))
        {
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT deck_id,deck_name,result,game_losses FROM matches WHERE match_id='late-id-1'";
            using var reader = cmd.ExecuteReader();
            Assert(reader.Read() && reader.GetString(0) == "arena-late-1" &&
                reader.GetString(1) == "Late Matched Deck" && reader.GetString(2) == "loss" &&
                reader.GetInt32(3) == 1,
                "Late deck mapping must retain the original result and game counts");
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


        // Renaming a uniquely named saved deck pins previously name-only
        // matches to its immutable identity. No result or match count changes.
        Assert(history.Add(new MatchHistory.MatchRow("rename-legacy-1", played, "win",
            "arena_match_result", "Historic", "", "Old Borg", "",
            "Player.log", "Older name-only match", 1, 0)), "Rename fixture failed.");
        Assert(history.Add(new MatchHistory.MatchRow("rename-verified-2", played, "loss",
            "arena_match_result", "Historic", "arena-unmapped", "Old Borg", "",
            "Player.log", "Deck assignment: manually confirmed in Arcane Vault Match History",
            0, 1)), "Verified rename fixture failed.");
        Assert(history.Add(new MatchHistory.MatchRow("rename-passive-3", played, "win",
            "arena_match_result", "Historic", "arena-unknown", "Old Borg", "",
            "Player.log", "Passive inventory label", 1, 0)), "Passive fixture failed.");
        Assert(history.Add(new MatchHistory.MatchRow("rename-other-4", played, "win",
            "arena_match_result", "Historic", "arena-conflict", "Old Borg", "",
            "Player.log", "Deck assignment: manual", 1, 0)), "Conflict fixture failed.");
        Assert(history.RememberDeckMapping("arena-conflict", "another-native", "Different Deck"),
            "Conflicting Arena ID mapping fixture failed.");
        int pinned = history.PinLegacyMatchesForRename("Old Borg", "native-borg");
        Assert(pinned == 2, "Only uniquely eligible historical matches may be pinned.");
        Assert(history.PinLegacyMatchesForRename("Old Borg", "native-borg") == 0,
            "Repeated rename reconciliation must be idempotent.");
        using (var db = new SqliteConnection("Data Source=" + path))
        {
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = @"SELECT match_id,result,deck_name,native_deck_id
                FROM matches WHERE match_id LIKE 'rename-%' ORDER BY match_id";
            using var reader = cmd.ExecuteReader();
            var actual = new Dictionary<string, (string Result, string Name, string NativeId)>();
            while (reader.Read())
                actual[reader.GetString(0)] = (reader.GetString(1),
                    reader.GetString(2), reader.GetString(3));
            Assert(actual.Count == 4, "Renaming must never delete or duplicate matches.");
            Assert(actual["rename-legacy-1"] == ("win", "Old Borg", "native-borg") &&
                   actual["rename-verified-2"] == ("loss", "Old Borg", "native-borg"),
                "Historical results must retain their original names and stable attribution.");
            Assert(actual["rename-passive-3"].NativeId == "" &&
                   actual["rename-other-4"].NativeId == "",
                "Unverified or conflicting Arena evidence must not be reassigned.");
        }

    }

    private static void Assert(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }
}
