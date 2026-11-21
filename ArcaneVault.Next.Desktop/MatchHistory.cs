using System.IO;
using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace ArcaneVault.Next.Desktop;

// All data stored locally; no opponent identifying information is persisted.
internal sealed class MatchHistory : IDisposable
{
    private readonly SqliteConnection db;
    private readonly object gate = new();
    public MatchHistory(string? filePath = null)
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ArcaneVaultNext");
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
            native_deck_id TEXT NOT NULL DEFAULT '',
            deck_name TEXT NOT NULL DEFAULT '',
            deck_version TEXT NOT NULL DEFAULT '',
            source_log TEXT NOT NULL DEFAULT '',
            note TEXT NOT NULL DEFAULT '',
            game_wins INTEGER NOT NULL DEFAULT 0,
            game_losses INTEGER NOT NULL DEFAULT 0
        );
        CREATE INDEX IF NOT EXISTS idx_matches_deck ON matches(deck_name, deck_version);
        CREATE INDEX IF NOT EXISTS idx_matches_date ON matches(played_utc);
        CREATE TABLE IF NOT EXISTS rank_snapshots (
            captured_utc TEXT PRIMARY KEY,
            constructed_class TEXT NOT NULL DEFAULT '',
            constructed_level INTEGER NOT NULL DEFAULT 0,
            limited_class TEXT NOT NULL DEFAULT '',
            limited_level INTEGER NOT NULL DEFAULT 0,
            source TEXT NOT NULL DEFAULT 'Arena'
        );";
        command.ExecuteNonQuery();
        ArenaMatchTelemetry.EnsureSchema(db);
        using (var seatSchema = db.CreateCommand())
        {
            seatSchema.CommandText = @"CREATE TABLE IF NOT EXISTS arena_verified_local_seats (
                match_id TEXT PRIMARY KEY,
                seat_id INTEGER NOT NULL,
                evidence TEXT NOT NULL DEFAULT 'authenticated_room_user_id'
            );";
            seatSchema.ExecuteNonQuery();
        }
        using (var mappingSchema = db.CreateCommand())
        {
            mappingSchema.CommandText = @"CREATE TABLE IF NOT EXISTS deck_mappings (
                arena_deck_id TEXT PRIMARY KEY COLLATE NOCASE,
                native_deck_id TEXT NOT NULL,
                deck_name TEXT NOT NULL
            );";
            mappingSchema.ExecuteNonQuery();
        }
        // Existing match databases predate stable native-deck assignments.
        // Extend only the optional match-history database; never the native
        // collection or its immutable physical ownership records.
        using var columns = db.CreateCommand();
        columns.CommandText = "PRAGMA table_info(matches)";
        using var rows = columns.ExecuteReader();
        bool hasNativeDeckId = false;
        while (rows.Read())
            if (rows.GetString(1).Equals("native_deck_id", StringComparison.OrdinalIgnoreCase))
                hasNativeDeckId = true;
        rows.Close();
        if (!hasNativeDeckId)
        {
            using var migrate = db.CreateCommand();
            migrate.CommandText = "ALTER TABLE matches ADD COLUMN native_deck_id TEXT NOT NULL DEFAULT ''";
            migrate.ExecuteNonQuery();
        }
    }

    /// <summary>Record only explicitly match-associated, sanitized GRE telemetry.
    /// These observations never change match results or native inventory.</summary>
    internal int AddTelemetry(IEnumerable<ArenaMatchTelemetry.Observed> events,
        DateTimeOffset captured)
    {
        lock (gate) return ArenaMatchTelemetry.Insert(db, events, captured);
    }

    /// <summary>Persist only an exact authenticated user-id to room-seat match;
    /// contradictions invalidate attribution rather than overwriting evidence.</summary>
    internal void RecordVerifiedSeat(string matchId, int seat)
    {
        if (string.IsNullOrWhiteSpace(matchId) || matchId.Length > 180 || seat is < 0 or > 100)
            return;
        lock (gate)
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText = @"INSERT INTO arena_verified_local_seats(match_id,seat_id)
                VALUES ($id,$seat) ON CONFLICT(match_id) DO UPDATE SET
                seat_id=CASE WHEN seat_id=excluded.seat_id THEN seat_id ELSE -1 END";
            cmd.Parameters.AddWithValue("$id",matchId);
            cmd.Parameters.AddWithValue("$seat",seat);
            cmd.ExecuteNonQuery();
        }
    }

    internal sealed record PlayDrawSummary(int OnPlayWins,int OnPlayLosses,
        int OnDrawWins,int OnDrawLosses, int Unattributed);
    internal PlayDrawSummary ReadVerifiedPlayDraw()
    {
        lock (gate)
        {
            // Conservative: one numbered game only, one unambiguous starting
            // seat observed at turn one, and one authenticated room seat.
            var starts = new Dictionary<string,HashSet<int>>(StringComparer.Ordinal);
            var games = new Dictionary<string,HashSet<int>>(StringComparer.Ordinal);
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = @"SELECT t.match_id,t.game_number,t.turn_number,t.payload_json
                    FROM arena_match_telemetry t
                    INNER JOIN matches m ON m.match_id=t.match_id
                    INNER JOIN arena_verified_local_seats s ON s.match_id=t.match_id
                    WHERE s.seat_id>=0 ORDER BY t.id";
                using var reader = cmd.ExecuteReader();
                while(reader.Read())
                {
                    var id=reader.GetString(0);
                    if (!reader.IsDBNull(1))
                    {
                        if(!games.TryGetValue(id,out var g)) games[id]=g=new HashSet<int>();
                        g.Add(reader.GetInt32(1));
                    }
                    if(reader.IsDBNull(1) || reader.IsDBNull(2) || reader.GetInt32(2)!=1) continue;
                    try
                    {
                        using var json=JsonDocument.Parse(reader.GetString(3));
                        if (!json.RootElement.TryGetProperty("turn",out var turn) ||
                            !turn.TryGetProperty("activePlayerSeatId",out var seat) ||
                            !seat.TryGetInt32(out var n) || n is < 0 or > 100) continue;
                        if(!starts.TryGetValue(id,out var set)) starts[id]=set=new HashSet<int>();
                        set.Add(n);
                    }
                    catch(JsonException) { }
                }
            }
            int pw=0,pl=0,dw=0,dl=0,unknown=0;
            using var matches=db.CreateCommand();
            matches.CommandText=@"SELECT m.match_id,m.result,s.seat_id FROM matches m
                LEFT JOIN arena_verified_local_seats s ON s.match_id=m.match_id
                WHERE m.result IN ('win','loss')";
            using var rows=matches.ExecuteReader();
            while(rows.Read())
            {
                var id=rows.GetString(0);
                if(rows.IsDBNull(2) || rows.GetInt32(2)<0 ||
                    !games.TryGetValue(id,out var g) || g.Count!=1 ||
                    !starts.TryGetValue(id,out var found) || found.Count!=1)
                { unknown++; continue; }
                bool play=found.Single()==rows.GetInt32(2);
                bool win=rows.GetString(1)=="win";
                if(play) { if(win)pw++;else pl++; }
                else { if(win)dw++;else dl++; }
            }
            return new PlayDrawSummary(pw,pl,dw,dl,unknown);
        }
    }

    internal int TelemetryCount(string matchId)
    {
        lock (gate)
        {
            using var command = db.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM arena_match_telemetry WHERE match_id=$id";
            command.Parameters.AddWithValue("$id", matchId);
            return Convert.ToInt32(command.ExecuteScalar());
        }
    }

    /// <summary>Read-only coverage of safely retained Arena game-state evidence.
    /// An event is an observed GRE message, not necessarily a player action;
    /// a turn number is the highest observed turn, not guaranteed game length.</summary>
    internal sealed record TelemetryCoverage(string MatchId, string PlayedUtc,
        string DeckName, string Format, int Events, int GamesWithNumbers, int? HighestTurn);
    internal sealed record TelemetryOverview(int RecordedMatches, int MatchesWithTelemetry,
        int ArchivedEvents, IReadOnlyList<TelemetryCoverage> Recent);

    internal TelemetryOverview ReadTelemetryOverview()
    {
        lock (gate)
        {
            int recorded;
            using (var count = db.CreateCommand())
            {
                count.CommandText = "SELECT COUNT(*) FROM matches";
                recorded = Convert.ToInt32(count.ExecuteScalar());
            }
            int covered, events;
            using (var totals = db.CreateCommand())
            {
                totals.CommandText = @"SELECT COUNT(DISTINCT t.match_id), COUNT(*)
                    FROM arena_match_telemetry t
                    INNER JOIN matches m ON m.match_id=t.match_id";
                using var reader = totals.ExecuteReader();
                reader.Read();
                covered = reader.GetInt32(0);
                events = reader.GetInt32(1);
            }
            var recent = new List<TelemetryCoverage>();
            using (var query = db.CreateCommand())
            {
                query.CommandText = @"SELECT m.match_id, m.played_utc, m.deck_name,
                    m.format, COUNT(t.id), COUNT(DISTINCT t.game_number),
                    MAX(t.turn_number)
                    FROM matches m
                    INNER JOIN arena_match_telemetry t ON t.match_id=m.match_id
                    GROUP BY m.match_id
                    ORDER BY m.played_utc DESC, m.match_id DESC LIMIT 20";
                using var reader = query.ExecuteReader();
                while (reader.Read())
                    recent.Add(new TelemetryCoverage(reader.GetString(0),
                        reader.GetString(1),reader.GetString(2),reader.GetString(3),
                        reader.GetInt32(4),reader.GetInt32(5),
                        reader.IsDBNull(6) ? null : reader.GetInt32(6)));
            }
            return new TelemetryOverview(recorded,covered,events,recent);
        }
    }

    /// <summary>Partial, seat-anonymous gameplay observations from archived GRE data.
    /// Turn maxima are lower bounds: a missing final state must never be treated
    /// as a completed game. Mulligans may be reported only for observed seats.</summary>
    internal sealed record GameplayObservation(int GamesWithTurnData,
        double AverageHighestObservedTurn, int GamesWithMulliganData,
        int SeatsWithMulligans, int? HighestObservedMulligans);

    internal GameplayObservation ReadGameplayObservation()
    {
        lock (gate)
        {
            // One row per observed match/game; exclude unknown game identifiers
            // rather than silently blending multiple games in the same match.
            var gameTurns = new List<int>();
            using (var query = db.CreateCommand())
            {
                query.CommandText = @"SELECT MAX(t.turn_number)
                    FROM arena_match_telemetry t
                    INNER JOIN matches m ON m.match_id=t.match_id
                    WHERE t.game_number IS NOT NULL AND t.turn_number BETWEEN 1 AND 1000
                    GROUP BY t.match_id,t.game_number";
                using var reader = query.ExecuteReader();
                while (reader.Read())
                    if (!reader.IsDBNull(0)) gameTurns.Add(reader.GetInt32(0));
            }
            // Values are seat-anonymous. Across successive snapshots, use the
            // highest observed mulligan count for each match/game/seat, not
            // the number of GRE messages that repeat the same count.
            var mulligans = new Dictionary<(string Match,int Game,int Seat),int>();
            using (var query = db.CreateCommand())
            {
                query.CommandText = @"SELECT t.match_id,t.game_number,t.payload_json
                    FROM arena_match_telemetry t
                    INNER JOIN matches m ON m.match_id=t.match_id
                    WHERE t.game_number IS NOT NULL
                    ORDER BY t.id DESC LIMIT 10000";
                using var reader = query.ExecuteReader();
                while (reader.Read())
                {
                    var match = reader.GetString(0);
                    var game = reader.GetInt32(1);
                    try
                    {
                        using var doc = JsonDocument.Parse(reader.GetString(2));
                        if (!doc.RootElement.TryGetProperty("players",out var players) ||
                            players.ValueKind != JsonValueKind.Array) continue;
                        foreach (var player in players.EnumerateArray())
                        {
                            if (player.ValueKind != JsonValueKind.Object ||
                                !player.TryGetProperty("mulliganCount",out var count) ||
                                !count.TryGetInt32(out int value) || value is < 0 or > 20)
                                continue;
                            JsonElement seat;
                            if (!player.TryGetProperty("systemSeatId",out seat) &&
                                !player.TryGetProperty("seatId",out seat) &&
                                !player.TryGetProperty("systemSeatNumber",out seat))
                                continue;
                            if (!seat.TryGetInt32(out int seatNumber) ||
                                seatNumber is < 0 or > 100) continue;
                            var key = (match,game,seatNumber);
                            if (!mulligans.TryGetValue(key,out int prior) || value>prior)
                                mulligans[key] = value;
                        }
                    }
                    catch (JsonException) { /* Historical malformed telemetry is ignored. */ }
                }
            }
            return new GameplayObservation(gameTurns.Count,
                gameTurns.Count==0 ? 0 : gameTurns.Average(),
                mulligans.Keys.Select(x=>(x.Match,x.Game)).Distinct().Count(),
                mulligans.Values.Count(x=>x>0),
                mulligans.Count==0 ? null : mulligans.Values.Max());
        }
    }

    internal sealed record RankSnapshot(string WhenUtc, string ConstructedClass,
        int ConstructedLevel, string LimitedClass, int LimitedLevel);

    public bool AddRankSnapshot(DateTimeOffset time, ArenaRankInfo.Snapshot rank)
    {
        lock (gate)
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText = @"INSERT OR IGNORE INTO rank_snapshots
                (captured_utc,constructed_class,constructed_level,limited_class,limited_level,source)
                VALUES($at,$c,$cl,$l,$ll,$source)";
            cmd.Parameters.AddWithValue("$at", time.ToUniversalTime().ToString("O"));
            cmd.Parameters.AddWithValue("$c", rank.ConstructedClass);
            cmd.Parameters.AddWithValue("$cl", rank.ConstructedLevel);
            cmd.Parameters.AddWithValue("$l", rank.LimitedClass);
            cmd.Parameters.AddWithValue("$ll", rank.LimitedLevel);
            cmd.Parameters.AddWithValue("$source", rank.Source);
            return cmd.ExecuteNonQuery() == 1;
        }
    }

    public RankSnapshot? LatestRank()
    {
        lock (gate)
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText = @"SELECT captured_utc,constructed_class,constructed_level,
                limited_class,limited_level FROM rank_snapshots
                ORDER BY captured_utc DESC LIMIT 1";
            using var reader = cmd.ExecuteReader();
            return reader.Read()
                ? new RankSnapshot(reader.GetString(0),reader.GetString(1),reader.GetInt32(2),
                    reader.GetString(3),reader.GetInt32(4)) : null;
        }
    }

    // Explicitly approved, persistent Arena-ID to native-deck relationships.
    // Stored separately from the collection to avoid editing card/deck data.
    internal sealed record DeckMapping(string ArenaId, string NativeId, string DeckName);
    public IReadOnlyList<DeckMapping> Mappings()
    {
        lock (gate)
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT arena_deck_id,native_deck_id,deck_name FROM deck_mappings";
            using var reader = cmd.ExecuteReader();
            var mappings = new List<DeckMapping>();
            while (reader.Read())
                mappings.Add(new DeckMapping(reader.GetString(0),reader.GetString(1),reader.GetString(2)));
            return mappings;
        }
    }

    /// <summary>Save an explicitly confirmed exact Arena ID mapping.
    /// Replacing an existing conflicting mapping requires separate user consent.</summary>
    public bool RememberDeckMapping(string arenaId, string nativeId, string deckName,
        bool replaceExisting = false)
    {
        arenaId = arenaId?.Trim() ?? "";
        nativeId = nativeId?.Trim() ?? "";
        deckName = deckName?.Trim() ?? "";
        if (arenaId.Length is < 1 or > 180 || nativeId.Length is < 1 or > 180 ||
            deckName.Length is < 1 or > 150)
            throw new ArgumentException("Invalid Arena deck ID or native saved-deck identity.");
        lock (gate)
        {
            using var read = db.CreateCommand();
            read.CommandText = "SELECT native_deck_id FROM deck_mappings WHERE arena_deck_id=$arena";
            read.Parameters.AddWithValue("$arena",arenaId);
            string? existing = read.ExecuteScalar() as string;
            if (existing is not null && existing != nativeId && !replaceExisting)
                return false;
            using var cmd = db.CreateCommand();
            cmd.CommandText = @"INSERT INTO deck_mappings (arena_deck_id,native_deck_id,deck_name)
                VALUES ($arena,$native,$name)
                ON CONFLICT(arena_deck_id) DO UPDATE SET
                native_deck_id=excluded.native_deck_id,deck_name=excluded.deck_name";
            cmd.Parameters.AddWithValue("$arena",arenaId);
            cmd.Parameters.AddWithValue("$native",nativeId);
            cmd.Parameters.AddWithValue("$name",deckName);
            return cmd.ExecuteNonQuery() > 0;
        }
    }

    /// <summary>Backfill only unassigned matches with a verified stable deck.
    /// Existing manual and automatic assignments are never overwritten.</summary>
    public int BackfillExactArenaId(string arenaId, string nativeId, string deckName)
    {
        if (string.IsNullOrWhiteSpace(arenaId) || string.IsNullOrWhiteSpace(nativeId) ||
            string.IsNullOrWhiteSpace(deckName)) return 0;
        lock (gate)
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText = @"UPDATE matches SET native_deck_id=$native,
                deck_name=$name, note='Deck assignment: verified persistent Arena ID'
                WHERE deck_id=$arena AND TRIM(deck_name)='' AND TRIM(native_deck_id)=''";
            cmd.Parameters.AddWithValue("$arena",arenaId);
            cmd.Parameters.AddWithValue("$native",nativeId);
            cmd.Parameters.AddWithValue("$name",deckName);
            return cmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Pin historical name-attributed matches to their immutable native deck ID
    /// after a uniquely named saved deck is renamed. Never change match results,
    /// names, manual assignments, or Arena IDs. Entries with conflicting verified
    /// Arena mappings are excluded; uncertain records remain for manual review.
    /// The caller must establish that the old name was unique before the save.
    /// </summary>
    public int PinLegacyMatchesForRename(string oldName, string nativeId)
    {
        oldName = oldName?.Trim() ?? "";
        nativeId = nativeId?.Trim() ?? "";
        if (oldName.Length is < 1 or > 150 || nativeId.Length is < 1 or > 180)
            throw new ArgumentException("Invalid saved deck rename identity.");
        lock (gate)
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText = @"UPDATE matches SET native_deck_id=$native
                WHERE TRIM(native_deck_id)=''
                  AND deck_name=$old COLLATE NOCASE
                  AND (
                    TRIM(deck_id)=''
                    OR (
                      (note LIKE 'Deck assignment: manually confirmed%'
                       OR note='Deck assignment: manual'
                       OR note LIKE 'Deck assignment: recent_arena_%'
                       OR note LIKE 'Deck assignment: verified_arena_%'
                       OR note LIKE 'Deck assignment: match_deck_name%')
                      AND NOT EXISTS (
                        SELECT 1 FROM deck_mappings m
                        WHERE m.arena_deck_id=matches.deck_id
                          AND m.native_deck_id<>$native)
                    )
                  )";
            cmd.Parameters.AddWithValue("$native", nativeId);
            cmd.Parameters.AddWithValue("$old", oldName);
            return cmd.ExecuteNonQuery();
        }
    }

    internal record MatchRow(string matchId, string playedUtc, string result, string resultSource,
        string format, string deckId, string deckName, string deckVersion, string sourceLog,
        string note, int gameWins, int gameLosses, string nativeDeckId = "");

    // Repair an existing imported match by its exact Arena match ID, without
    // changing the result, counts, or a manually/previously assigned deck.
    // This also lets a second log pass supply a deck ID that arrived late.
    public bool EnrichUnassignedMatch(string matchId, string arenaDeckId,
        string nativeDeckId, string deckName, string deckVersion, string note)
    {
        if (string.IsNullOrWhiteSpace(matchId) || string.IsNullOrWhiteSpace(arenaDeckId) ||
            arenaDeckId.Length > 180 || nativeDeckId.Length > 180 ||
            deckName.Length > 150 || deckVersion.Length > 100 || note.Length > 500)
            return false;
        lock (gate)
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText = @"UPDATE matches
                SET deck_id=$arena, native_deck_id=$native, deck_name=$name,
                    deck_version=$version, note=$note
                WHERE match_id=$match
                  AND TRIM(deck_id)='' AND TRIM(deck_name)=''
                  AND TRIM(native_deck_id)=''";
            cmd.Parameters.AddWithValue("$match", matchId);
            cmd.Parameters.AddWithValue("$arena", arenaDeckId);
            cmd.Parameters.AddWithValue("$native", nativeDeckId);
            cmd.Parameters.AddWithValue("$name", deckName);
            cmd.Parameters.AddWithValue("$version", deckVersion);
            cmd.Parameters.AddWithValue("$note", note);
            return cmd.ExecuteNonQuery() == 1;
        }
    }

    public bool Add(MatchRow row)
    {
        lock (gate)
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText = @"INSERT OR IGNORE INTO matches
                (match_id,played_utc,result,result_source,format,deck_id,native_deck_id,deck_name,deck_version,source_log,note,game_wins,game_losses)
                VALUES ($id,$time,$result,$source,$format,$deckId,$native,$deck,$version,$log,$note,$gw,$gl)";
            cmd.Parameters.AddWithValue("$id", row.matchId);
            cmd.Parameters.AddWithValue("$time", row.playedUtc);
            cmd.Parameters.AddWithValue("$result", row.result);
            cmd.Parameters.AddWithValue("$source", row.resultSource);
            cmd.Parameters.AddWithValue("$format", row.format);
            cmd.Parameters.AddWithValue("$deckId", row.deckId);
            cmd.Parameters.AddWithValue("$native", row.nativeDeckId);
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
            // Treat case/outer-whitespace variants as the same displayed deck.
            // Version and queue remain distinct for accurate statistics.
            var groups = confirmed.GroupBy(r => (
                    name: r.deckName.Trim().ToUpperInvariant(),
                    version: r.deckVersion.Trim(),
                    format: r.format.Trim()))
                .Select(g => new { deck = g.First().deckName.Trim(), version = g.Key.version, format = g.Key.format,
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
                WHERE deck_id=$id AND TRIM(deck_name)='' AND TRIM(native_deck_id)=''";
            cmd.Parameters.AddWithValue("$id", deckId);
            cmd.Parameters.AddWithValue("$name", deckName);
            return cmd.ExecuteNonQuery();
        }
    }

    // Only allow manual corrections; never infer a win from an unknown result.
    public bool Update(string id, string? result, string? deckName, string? deckVersion, string? note,
        string? nativeDeckId = null)
    {
        lock (gate)
        {
            if (result != null && result is not ("win" or "loss" or "draw" or "unknown"))
                throw new ArgumentException("Invalid match result.");
            if (deckName?.Length > 150 || deckVersion?.Length > 100 || note?.Length > 500 ||
                nativeDeckId?.Length > 180)
                throw new ArgumentException("Text field too long.");
            using var cmd = db.CreateCommand();
            cmd.CommandText = @"UPDATE matches SET
              result = COALESCE($result,result),
              result_source = CASE WHEN $result IS NULL THEN result_source ELSE 'manual' END,
              deck_name = COALESCE($name,deck_name), deck_version = COALESCE($version,deck_version),
              native_deck_id = COALESCE($native,native_deck_id),
              note = CASE WHEN $note IS NOT NULL THEN $note
                          WHEN $name IS NOT NULL THEN 'Deck assignment: manual'
                          ELSE note END WHERE match_id=$id";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.Parameters.AddWithValue("$result", (object?)result ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$name", (object?)deckName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$native", (object?)nativeDeckId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$version", (object?)deckVersion ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$note", (object?)note ?? DBNull.Value);
            return cmd.ExecuteNonQuery() > 0;
        }
    }
    public void Dispose() => db.Dispose();
}
