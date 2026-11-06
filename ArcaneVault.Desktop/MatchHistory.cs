using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace ArcaneVault.Desktop;

// All data stored locally; no opponent identifying information is persisted.
internal sealed class MatchHistory : IDisposable
{
    private readonly SqliteConnection db;
    private readonly object gate = new();
    public MatchHistory(string? filePath = null)
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ArcaneVault");
        Directory.CreateDirectory(folder);
        db = new SqliteConnection($"Data Source={filePath ?? Path.Combine(folder, "match-history.sqlite3")}");
        db.Open();
        using var command = db.CreateCommand();
        command.CommandText = @"CREATE TABLE IF NOT EXISTS matches (
            match_id TEXT PRIMARY KEY,
            played_utc TEXT NOT NULL,
            result TEXT NOT NULL CHECK(result IN ('win','loss','draw','unknown')),
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
        CREATE INDEX IF NOT EXISTS idx_matches_deck ON matches(deck_name, deck_version);
        CREATE INDEX IF NOT EXISTS idx_matches_date ON matches(played_utc);";
        command.ExecuteNonQuery();
    }

    internal record MatchRow(string matchId, string playedUtc, string result, string resultSource,
        string format, string deckId, string deckName, string deckVersion, string sourceLog,
        string note, int gameWins, int gameLosses);

    public bool Add(MatchRow row)
    {
        lock (gate)
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText = @"INSERT OR IGNORE INTO matches
                (match_id,played_utc,result,result_source,format,deck_id,deck_name,deck_version,source_log,note,game_wins,game_losses)
                VALUES ($id,$time,$result,$source,$format,$deckId,$deck,$version,$log,$note,$gw,$gl)";
            cmd.Parameters.AddWithValue("$id", row.matchId);
            cmd.Parameters.AddWithValue("$time", row.playedUtc);
            cmd.Parameters.AddWithValue("$result", row.result);
            cmd.Parameters.AddWithValue("$source", row.resultSource);
            cmd.Parameters.AddWithValue("$format", row.format);
            cmd.Parameters.AddWithValue("$deckId", row.deckId);
            cmd.Parameters.AddWithValue("$deck", row.deckName);
            cmd.Parameters.AddWithValue("$version", row.deckVersion);
            cmd.Parameters.AddWithValue("$log", row.sourceLog);
            cmd.Parameters.AddWithValue("$note", row.note);
            cmd.Parameters.AddWithValue("$gw", row.gameWins);
            cmd.Parameters.AddWithValue("$gl", row.gameLosses);
            return cmd.ExecuteNonQuery() == 1;
        }
    }

    public object Snapshot()
    {
        lock (gate)
        {
            var rows = new List<MatchRow>();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT match_id,played_utc,result,result_source,format,deck_id,deck_name,deck_version,source_log,note,game_wins,game_losses FROM matches ORDER BY played_utc DESC";
            using var rd = cmd.ExecuteReader();
            while (rd.Read()) rows.Add(new MatchRow(rd.GetString(0),rd.GetString(1),rd.GetString(2),rd.GetString(3),rd.GetString(4),rd.GetString(5),rd.GetString(6),rd.GetString(7),rd.GetString(8),rd.GetString(9),rd.GetInt32(10),rd.GetInt32(11)));
            var confirmed = rows.Where(r => r.result is "win" or "loss").ToList();
            var groups = confirmed.GroupBy(r => (r.deckName, r.deckVersion, r.format))
                .Select(g => new { deck = g.Key.deckName, version = g.Key.deckVersion, format = g.Key.format,
                    wins = g.Count(r => r.result == "win"), losses = g.Count(r => r.result == "loss"),
                    gamesWon = g.Sum(r => r.gameWins), gamesLost = g.Sum(r => r.gameLosses) })
                .OrderByDescending(g => g.wins + g.losses).ToArray();
            return new { matches = rows, groups, total = rows.Count,
                wins = confirmed.Count(r => r.result == "win"), losses = confirmed.Count(r => r.result == "loss"),
                awaitingReview = rows.Count(r => r.result == "unknown" || string.IsNullOrWhiteSpace(r.deckName)),
                storedLocally = true };
        }
    }

    // Apply a subsequently observed exact Arena deck-ID mapping to older unassigned matches.
    // Never overwrite a deck already assigned by the user or another matching method.
    public int AssignUnassignedByDeckId(string deckId, string deckName)
    {
        if (string.IsNullOrWhiteSpace(deckId) || deckId.Length > 180 ||
            string.IsNullOrWhiteSpace(deckName) || deckName.Length > 150) return 0;
        lock (gate)
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText = @"UPDATE matches SET deck_name=$name,
                note='Deck assignment: arena_deck_id (backfilled)'
                WHERE deck_id=$id AND TRIM(deck_name)=''";
            cmd.Parameters.AddWithValue("$id", deckId);
            cmd.Parameters.AddWithValue("$name", deckName);
            return cmd.ExecuteNonQuery();
        }
    }

    // Only allow manual corrections; never infer a win from an unknown result.
    public bool Update(string id, string? result, string? deckName, string? deckVersion, string? note)
    {
        lock (gate)
        {
            if (result != null && result is not ("win" or "loss" or "draw" or "unknown"))
                throw new ArgumentException("Invalid match result.");
            if (deckName?.Length > 150 || deckVersion?.Length > 100 || note?.Length > 500)
                throw new ArgumentException("Text field too long.");
            using var cmd = db.CreateCommand();
            cmd.CommandText = @"UPDATE matches SET
              result = COALESCE($result,result),
              result_source = CASE WHEN $result IS NULL THEN result_source ELSE 'manual' END,
              deck_name = COALESCE($name,deck_name), deck_version = COALESCE($version,deck_version),
              note = COALESCE($note,note) WHERE match_id=$id";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.Parameters.AddWithValue("$result", (object?)result ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$name", (object?)deckName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$version", (object?)deckVersion ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$note", (object?)note ?? DBNull.Value);
            return cmd.ExecuteNonQuery() > 0;
        }
    }
    public void Dispose() => db.Dispose();
}
