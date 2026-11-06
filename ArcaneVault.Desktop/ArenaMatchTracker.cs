using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Globalization;

namespace ArcaneVault.Desktop;

/// <summary>Read-only opt-in match tracking from Arena logs. Unknown self identity => unknown result.</summary>
internal sealed class ArenaMatchTracker : IDisposable
{
    private readonly MatchHistory history;
    private readonly object gate = new();
    private readonly Dictionary<string, long> offsets = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SessionMatch> sessions = new(StringComparer.Ordinal);
    private string? selectedLog;
    private bool enabled;
    private string? authenticatedId;
    private string? screenName;
    private string? currentDeckId;
    private string? currentDeckName;
    private string? currentDeckVersion;
    private DateTimeOffset? latestLogDeckSelection;
    private readonly Dictionary<string, (string Name, string Version)> decksByArenaId = new(StringComparer.OrdinalIgnoreCase);
    private string? error;
    private int processedEvents;
    private int skippedEvents;
    private bool userChoseActiveDeck;
    private DateTimeOffset? lastScan;
    private const int MaxFileBytesOnFirstScan = 24 * 1024 * 1024;
    private const int MaxEventBytes = 3 * 1024 * 1024;

    private sealed class SessionMatch
    {
        public string Id = "";
        public string Format = "";
        public int? PlayerTeam;
        public string DeckId = "";
        public string DeckName = "";
        public string DeckVersion = "";
        public string Assignment = "unassigned";
        public DateTimeOffset? Started;
    }
    public ArenaMatchTracker(string? databaseFile = null)
    {
        history = new MatchHistory(databaseFile);
        selectedLog = ArenaLogConnector.DefaultLogPath();
    }
    public void Configure(bool enable, string? logFile)
    {
        lock (gate)
        {
            if (!string.IsNullOrWhiteSpace(logFile))
            {
                var full = Path.GetFullPath(Environment.ExpandEnvironmentVariables(logFile));
                if (!new[] { "Player.log", "Player-prev.log" }.Contains(Path.GetFileName(full), StringComparer.OrdinalIgnoreCase))
                    throw new ArgumentException("Choose a Player.log or Player-prev.log file.");
                selectedLog = full;
            }
            if (enable && !enabled) offsets.Clear();
            enabled = enable;
            error = null;
        }
    }
    public void SetCurrentDeck(string? deckName, string? version)
    {
        if (deckName?.Length > 150 || version?.Length > 100) throw new ArgumentException("Deck name or version too long.");
        lock (gate) { currentDeckName = deckName?.Trim(); currentDeckVersion = version?.Trim(); userChoseActiveDeck = !string.IsNullOrWhiteSpace(currentDeckName); }
    }
    public void Tick()
    {
        lock (gate)
        {
            if (!enabled) return;
            try
            {
                var directory = Path.GetDirectoryName(selectedLog);
                if (directory == null || !Directory.Exists(directory))
                    throw new IOException("Arena log folder not found. Enable Detailed Logs and launch MTG Arena.");
                foreach (var filename in new[] { "Player-prev.log", "Player.log" })
                {
                    var path = Path.Combine(directory, filename);
                    if (File.Exists(path)) ScanFile(path);
                }
                lastScan = DateTimeOffset.UtcNow;
                error = null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            { error = ex.Message; }
        }
    }
    public object Snapshot()
    {
        lock (gate)
            return new { enabled, path = selectedLog, error, lastScan, processedEvents, skippedEvents,
                currentDeckName, currentDeckVersion, userChoseActiveDeck, history = history.Snapshot(),
                note = "When Arena logs include deck IDs or a recent explicit deck selection, decks are assigned automatically. Otherwise choose a deck manually." };
    }
    public bool Correct(string matchId, string? result, string? deck, string? version, string? note) =>
        history.Update(matchId, result, deck, version, note);

    private void ScanFile(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var offset = offsets.GetValueOrDefault(path);
        if (offset > fs.Length) offset = 0;
        if (offset == 0 && fs.Length > MaxFileBytesOnFirstScan) offset = fs.Length - MaxFileBytesOnFirstScan;
        if (fs.Length == offset) return;
        fs.Position = offset;
        using var reader = new StreamReader(fs, Encoding.UTF8, detectEncodingFromByteOrderMarks:true, bufferSize:16384, leaveOpen:true);
        string? line;
        var builder = new StringBuilder();
        DateTimeOffset? timestamp = null;
        DateTimeOffset? lastHeaderTime = null;
        bool relevant = false;
        int balance = 0;
        bool quoted = false, escaped = false;
        while ((line = reader.ReadLine()) != null)
        {
            // Stop on a partial trailing line; it will be read on the next scan.
            if (fs.Position == fs.Length && !EndsWithNewline(fs)) break;
            if (line.StartsWith("[UnityCrossThreadLogger]", StringComparison.OrdinalIgnoreCase))
                lastHeaderTime = HeaderTime(line) ?? lastHeaderTime;
            if (builder.Length == 0)
            {
                relevant = line.Contains("matchGameRoomStateChangedEvent", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("EventSetDeckV2", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("CourseDeckSummary", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("authenticateResponse", StringComparison.OrdinalIgnoreCase);
                if (!relevant) continue;
                timestamp = HeaderTime(line) ?? lastHeaderTime;
                var brace = line.IndexOf('{');
                if (brace < 0) continue;
                line = line[brace..];
            }
            if (builder.Length + line.Length > MaxEventBytes)
            { skippedEvents++; builder.Clear(); balance = 0; quoted = false; escaped = false; continue; }
            builder.AppendLine(line);
            foreach (var c in line)
            {
                if (escaped) { escaped = false; continue; }
                if (quoted && c == '\\') { escaped = true; continue; }
                if (c == '"') quoted = !quoted;
                if (!quoted) { if (c == '{') balance++; else if (c == '}') balance--; }
            }
            if (balance == 0 && builder.Length > 0)
            {
                try { using var doc = JsonDocument.Parse(builder.ToString()); ParseEvent(doc.RootElement, timestamp, path); processedEvents++; }
                catch (JsonException) { skippedEvents++; }
                builder.Clear(); quoted = false; escaped = false;
            }
        }
        // Re-read an unfinished JSON object on the next pass rather than losing it.
        var readUpTo = fs.Position;
        var incompleteBytes = Encoding.UTF8.GetByteCount(builder.ToString());
        offsets[path] = Math.Max(offset, readUpTo - incompleteBytes);
    }
    private static bool EndsWithNewline(FileStream stream)
    {
        var p = stream.Position;
        if (p == 0) return true;
        stream.Position = p - 1;
        var b = stream.ReadByte();
        stream.Position = p;
        return b == '\n';
    }
    private static DateTimeOffset? HeaderTime(string line)
    {
        var match = Regex.Match(line, @"\b\d{1,2}/\d{1,2}/\d{4}\s+\d{1,2}:\d{2}:\d{2}\s*[AP]M\b", RegexOptions.IgnoreCase);
        return match.Success && DateTimeOffset.TryParse(match.Value, CultureInfo.GetCultureInfo("en-US"), DateTimeStyles.AssumeLocal, out var dt) ? dt : null;
    }
    private static bool Property(JsonElement e, string key, out JsonElement result)
    {
        result = default;
        return e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out result);
    }
    private static string Str(JsonElement e, string key) => Property(e,key,out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
    private static int? Number(JsonElement e, string key) => Property(e,key,out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) ? n : null;
    private static JsonElement Locate(JsonElement e, string property, int depth=0)
    {
        if (depth > 5) return default;
        if (Property(e,property,out var item)) return item;
        if (e.ValueKind == JsonValueKind.Object)
            foreach (var child in e.EnumerateObject())
            {
                var found = Locate(child.Value,property,depth+1);
                if (found.ValueKind != JsonValueKind.Undefined) return found;
            }
        return default;
    }
    private void ParseEvent(JsonElement root, DateTimeOffset? timestamp, string source)
    {
        var auth = Locate(root,"authenticateResponse");
        if (auth.ValueKind == JsonValueKind.Object)
        {
            var id = Str(auth,"clientId");
            if (!string.IsNullOrWhiteSpace(id)) authenticatedId = id;
            var name = Str(auth,"screenName");
            if (!string.IsNullOrWhiteSpace(name)) screenName = name;
        }
        // Capture deck selection as a candidate; no speculative correlation to matches.
        var deck = Locate(root,"EventSetDeckV2");
        if (deck.ValueKind == JsonValueKind.Object) ReadDeck(deck, timestamp, isSelection:true);
        var summary = Locate(root,"CourseDeckSummary");
        if (summary.ValueKind == JsonValueKind.Object) ReadDeck(summary, timestamp, isSelection:false);
        var change = Locate(root,"matchGameRoomStateChangedEvent");
        if (change.ValueKind != JsonValueKind.Object || !Property(change,"gameRoomInfo",out var room)) return;
        var state = Str(room,"stateType");
        if (state is not ("MatchGameRoomStateType_Playing" or "MatchGameRoomStateType_MatchCompleted")) return;
        var config = Property(room,"gameRoomConfig",out var c) ? c : default;
        var final = Property(room,"finalMatchResult",out var f) ? f : default;
        var idMatch = Str(config,"matchId");
        if (idMatch.Length == 0) idMatch = Str(final,"matchId");
        if (idMatch.Length == 0 || idMatch.Length > 180) return;
        if (!sessions.TryGetValue(idMatch,out var info)) sessions[idMatch] = info = new SessionMatch {Id=idMatch};
        if (config.ValueKind == JsonValueKind.Object)
        {
            if (info.Started == null) info.Started = timestamp;
            var queue = Str(config,"eventId"); if (queue.Length > 0) info.Format = queue;
            var explicitDeck = Str(config,"deckId");
            if (explicitDeck.Length == 0) explicitDeck = Str(config,"DeckId");
            if (explicitDeck.Length > 0)
            {
                info.DeckId = explicitDeck;
                if (decksByArenaId.TryGetValue(explicitDeck, out var known))
                { info.DeckName = known.Name; info.DeckVersion = known.Version; info.Assignment = "arena_deck_id"; }
                else if (explicitDeck.Equals(currentDeckId, StringComparison.OrdinalIgnoreCase) &&
                         latestLogDeckSelection.HasValue && timestamp.HasValue &&
                         (timestamp.Value - latestLogDeckSelection.Value).Duration() <= TimeSpan.FromMinutes(30))
                { info.DeckName = currentDeckName ?? ""; info.DeckVersion = currentDeckVersion ?? ""; info.Assignment = "recent_arena_selection"; }
            }
            // Some game modes log a deck name directly in the match configuration.
            var directName = Str(config,"deckName");
            if (directName.Length is > 0 and <= 150 && info.DeckName.Length == 0)
            { info.DeckName = directName; info.Assignment = "match_deck_name"; }
            // Player identity must be verified from an authenticated session, not just seat order.
            if (Property(config,"reservedPlayers",out var players) && players.ValueKind == JsonValueKind.Array)
            foreach (var p in players.EnumerateArray())
            {
                if (p.ValueKind != JsonValueKind.Object) continue;
                var uid = Str(p,"userId"); var pn = Str(p,"playerName");
                if ((!string.IsNullOrWhiteSpace(authenticatedId) && uid==authenticatedId) ||
                    (!string.IsNullOrWhiteSpace(screenName) && pn==screenName))
                    info.PlayerTeam = Number(p,"teamId");
            }
        }
        if (state == "MatchGameRoomStateType_Playing")
        {
            info.Started ??= timestamp;
            if (info.DeckName.Length == 0 && !userChoseActiveDeck &&
                latestLogDeckSelection.HasValue && timestamp.HasValue &&
                (timestamp.Value - latestLogDeckSelection.Value).TotalMinutes is >= 0 and <= 30 &&
                !string.IsNullOrWhiteSpace(currentDeckName))
            { info.DeckName = currentDeckName; info.DeckVersion = currentDeckVersion ?? ""; info.Assignment = "recent_arena_selection"; }
            if (userChoseActiveDeck && info.DeckName.Length == 0)
            { info.DeckName = currentDeckName ?? ""; info.DeckVersion = currentDeckVersion ?? ""; info.Assignment = "manual_active_deck"; }
            return;
        }
        if (final.ValueKind != JsonValueKind.Object || !Property(final,"resultList",out var results) || results.ValueKind != JsonValueKind.Array) return;
        int? winningTeam = null; int gw=0, gl=0;
        foreach (var result in results.EnumerateArray())
        {
            if (Str(result,"result") != "ResultType_WinLoss") continue;
            var scope = Str(result,"scope"); var team = Number(result,"winningTeamId");
            if (scope == "MatchScope_Match") winningTeam = team;
            if (scope == "MatchScope_Game" && info.PlayerTeam.HasValue && team.HasValue)
            { if(team==info.PlayerTeam) gw++; else gl++; }
        }
        string outcome = winningTeam.HasValue && info.PlayerTeam.HasValue ? (winningTeam==info.PlayerTeam ? "win" : "loss") : "unknown";
        if (info.DeckName.Length == 0 && info.DeckId.Length > 0 && decksByArenaId.TryGetValue(info.DeckId, out var mapped))
        { info.DeckName = mapped.Name; info.DeckVersion = mapped.Version; info.Assignment = "arena_deck_id"; }
        if (info.DeckName.Length == 0 && userChoseActiveDeck)
        { info.DeckName = currentDeckName ?? ""; info.DeckVersion = currentDeckVersion ?? ""; info.Assignment = "manual_active_deck"; }
        var occurred = timestamp ?? info.Started ?? DateTimeOffset.UtcNow;
        var note = outcome=="unknown" ? "Player/team identity not verified; review result manually." :
            info.DeckName.Length==0 ? "Deck not identified from match log; assign manually." :
            "Deck assignment: " + info.Assignment;
        history.Add(new MatchHistory.MatchRow(idMatch, occurred.ToUniversalTime().ToString("O"),outcome,
            outcome=="unknown"?"unverified":"arena_match_result",info.Format,info.DeckId,info.DeckName,info.DeckVersion,
            Path.GetFileName(source),note,gw,gl));
        sessions.Remove(idMatch);
        if (sessions.Count > 300) sessions.Clear();
    }
    private void ReadDeck(JsonElement deck, DateTimeOffset? timestamp, bool isSelection)
    {
        // Only a real Arena deck-selection event contributes to automatic assignment.
        // CourseDeckSummary may describe unrelated decks: store its ID mapping, but don't make it active.
        var id = Str(deck,"DeckId"); if (id.Length == 0) id = Str(deck,"deckId");
        var name = Str(deck,"Name"); if (name.Length == 0) name = Str(deck,"name");
        if (name.Length == 0) name = Str(deck,"deckName");
        if (id.Length is < 1 or > 180 || name.Length is < 1 or > 150) return;
        decksByArenaId[id] = (name, "");
        history.AssignUnassignedByDeckId(id, name);
        if (decksByArenaId.Count > 1000) decksByArenaId.Clear();
        // Do not replace an intentionally selected deck label until we observe the exact deck ID in match data.
        if (isSelection && !userChoseActiveDeck && timestamp.HasValue)
        { currentDeckId = id; currentDeckName = name; latestLogDeckSelection = timestamp; }
    }
    public void Dispose() => history.Dispose();
}
