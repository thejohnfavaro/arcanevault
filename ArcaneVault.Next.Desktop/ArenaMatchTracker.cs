using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Globalization;

namespace ArcaneVault.Next.Desktop;

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
    private readonly Dictionary<string, (string Name, string Version, string NativeId)> decksByArenaId =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> blockedArenaIds = new(StringComparer.OrdinalIgnoreCase);
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
        public string NativeDeckId = "";
        public string DeckName = "";
        public string DeckVersion = "";
        public string Assignment = "unassigned";
        public DateTimeOffset? Started;
    }
    public ArenaMatchTracker(string? databaseFile = null)
    {
        history = new MatchHistory(databaseFile);
        selectedLog = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "Wizards Of The Coast", "MTGA", "Player.log");
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
    /// <summary>
    /// Register stable Arena deck IDs from the verified native saved-deck
    /// library. This is authoritative ID matching, not a best-guess name
    /// lookup. Ambiguous IDs are refused rather than silently attributed.
    /// Also repairs previously unassigned records that already include
    /// the matching Arena deck ID.
    /// </summary>
    public void SetKnownSavedDecks(
        IEnumerable<(string ArenaId, string Name)> savedDecks) =>
        SetKnownSavedDecks(savedDecks.Select(d =>
            (d.ArenaId, d.Name, NativeId: "")));

    public void SetKnownSavedDecks(
        IEnumerable<(string ArenaId, string Name, string NativeId)> savedDecks)
    {
        ArgumentNullException.ThrowIfNull(savedDecks);
        // Native saved decks without an embedded Arena ID still need to
        // participate: a user-confirmed persistent mapping can target them.
        var rows = savedDecks.Where(x => !string.IsNullOrWhiteSpace(x.Name) &&
            x.Name.Length <= 150 && x.NativeId.Length <= 180).ToArray();
        var direct = rows.Where(x => !string.IsNullOrWhiteSpace(x.ArenaId) &&
            x.ArenaId.Length <= 180).ToArray();
        var collisions = direct.GroupBy(x => x.ArenaId.Trim(),
            StringComparer.OrdinalIgnoreCase).Where(g => g.Count() != 1)
            .Select(g => g.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var verified = direct.Where(x => !collisions.Contains(x.ArenaId.Trim()))
            .ToDictionary(x => x.ArenaId.Trim(), x => x,
                StringComparer.OrdinalIgnoreCase);
        lock (gate)
        {
            blockedArenaIds.Clear();
            blockedArenaIds.UnionWith(collisions);
            decksByArenaId.Clear();
            foreach (var d in verified.Values)
                decksByArenaId[d.ArenaId.Trim()] = (d.Name.Trim(), "", d.NativeId);
            // The persistent mapping is only usable if its native target
            // still exists in the latest verified saved-deck library.
            var byNative = rows.Where(x => x.NativeId.Length > 0)
                .GroupBy(x => x.NativeId, StringComparer.Ordinal)
                .Where(g => g.Count() == 1)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
            foreach (var saved in history.Mappings())
            {
                if (blockedArenaIds.Contains(saved.ArenaId)) continue;
                if (!byNative.TryGetValue(saved.NativeId, out var deck)) continue;
                // Conflicting association to a native library Arena ID is
                // ambiguous; never override the authoritative deck property.
                if (verified.TryGetValue(saved.ArenaId, out var existing) &&
                    existing.NativeId != deck.NativeId)
                {
                    blockedArenaIds.Add(saved.ArenaId);
                    decksByArenaId.Remove(saved.ArenaId);
                    continue;
                }
                decksByArenaId[saved.ArenaId] = (deck.Name, "", deck.NativeId);
            }
            foreach (var pair in decksByArenaId)
            {
                if (!string.IsNullOrWhiteSpace(pair.Value.NativeId))
                    history.BackfillExactArenaId(pair.Key, pair.Value.NativeId,
                        pair.Value.Name);
                else
                    history.AssignUnassignedByDeckId(pair.Key, pair.Value.Name);
            }
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
    public string? CurrentDeckName
    {
        get { lock (gate) return currentDeckName; }
    }
    public string? CurrentDeckId
    {
        get { lock (gate) return currentDeckId; }
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
        int balance = 0;
        bool quoted = false, escaped = false;
        string eventLabel = "";
        string pendingEventLabel = "";
        DateTimeOffset? pendingEventTime = null;
        int pendingLines = 0;
        while ((line = reader.ReadLine()) != null)
        {
            // Preserve the last complete line; never consume partially
            // written Arena JSON on a live shared file.
            if (fs.Position == fs.Length && !EndsWithNewline(fs)) break;
            var lineTime = HeaderTime(line);
            if (line.StartsWith("[UnityCrossThreadLogger]", StringComparison.OrdinalIgnoreCase))
                lastHeaderTime = lineTime ?? lastHeaderTime;
            if (builder.Length == 0)
            {
                // Arena writes some event labels on their own line, with
                // the associated JSON envelope on the following line.
                // Older versions discarded that JSON because it no longer
                // contained the event name. Keep a short, bounded pending
                // header and reset it on the next unrelated event.
                bool hasRoom = line.Contains("matchGameRoomStateChangedEvent", StringComparison.OrdinalIgnoreCase);
                bool hasSet = line.Contains("EventSetDeckV", StringComparison.OrdinalIgnoreCase);
                bool hasCourse = line.Contains("CourseDeck", StringComparison.OrdinalIgnoreCase);
                bool hasAuth = line.Contains("authenticateResponse", StringComparison.OrdinalIgnoreCase);
                bool relevant = hasRoom || hasSet || hasCourse || hasAuth;
                string detected = hasSet ? "deck_submission" :
                    line.Contains("==> CourseDeck", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("\"CourseDeck\"", StringComparison.OrdinalIgnoreCase)
                        ? "course_deck" : hasRoom ? "match_room" :
                        hasAuth ? "auth" : "deck_inventory";

                // New headers replace prior unfinished headers. An
                // inventory summary must not be mistaken for a submission.
                if (relevant)
                {
                    pendingEventLabel = detected;
                    pendingEventTime = lineTime ?? lastHeaderTime;
                    pendingLines = 0;
                }
                else if (pendingEventLabel.Length > 0)
                {
                    if (++pendingLines > 8)
                    {
                        pendingEventLabel = "";
                        pendingEventTime = null;
                    }
                }
                else continue;
                var brace = line.IndexOf('{');
                if (brace < 0) continue;
                eventLabel = pendingEventLabel;
                timestamp = pendingEventTime ?? lineTime ?? lastHeaderTime;
                pendingEventLabel = "";
                pendingEventTime = null;
                pendingLines = 0;
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
                try { using var doc = JsonDocument.Parse(builder.ToString()); ParseEvent(doc.RootElement, timestamp, path, eventLabel); processedEvents++; }
                catch (JsonException) { skippedEvents++; }
                builder.Clear(); quoted = false; escaped = false; eventLabel = "";
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
        if (e.ValueKind == JsonValueKind.Array)
            foreach (var child in e.EnumerateArray())
            {
                var found = Locate(child,property,depth+1);
                if (found.ValueKind != JsonValueKind.Undefined) return found;
            }
        return default;
    }
    // Extract the submitted deck ID from either direct request parameters
    // or the escaped request envelope's Summary object.
    private void ReadDeckSubmission(JsonElement root, DateTimeOffset? timestamp)
    {
        var nested = Locate(root, "Summary");
        if (nested.ValueKind == JsonValueKind.Object)
            ReadDeck(nested, timestamp, isSelection:true);
        else
        {
            var parameters = Locate(root,"params");
            if (parameters.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                ReadDeck(parameters,timestamp,isSelection:true);
            else
                ReadDeck(root,timestamp,isSelection:true);
        }
    }

    private void ParseEvent(JsonElement root, DateTimeOffset? timestamp, string source,
        string eventLabel = "")
    {
        var auth = Locate(root,"authenticateResponse");
        if (auth.ValueKind == JsonValueKind.Object)
        {
            var id = Str(auth,"clientId");
            if (!string.IsNullOrWhiteSpace(id)) authenticatedId = id;
            var name = Str(auth,"screenName");
            if (!string.IsNullOrWhiteSpace(name)) screenName = name;
        }
        // Queue submissions (EventSetDeckV2/V3 RPC, CourseDeck) are
        // independent of matchGameRoomStateChangedEvent. Some payloads wrap
        // their request as an escaped JSON string; parse only that known
        // field rather than searching unrelated logged deck inventories.
        if (eventLabel == "deck_submission" || eventLabel == "course_deck")
        {
            if (Property(root, "request", out var request) &&
                request.ValueKind == JsonValueKind.String &&
                request.GetString() is string encoded &&
                encoded.Length is > 1 and <= 100_000)
            {
                try
                {
                    using var decoded = JsonDocument.Parse(encoded);
                    ReadDeckSubmission(decoded.RootElement, timestamp);
                }
                catch (JsonException) { skippedEvents++; }
            }
            if (eventLabel == "course_deck")
            {
                var submitted = Locate(root,"CourseDeckSummary");
                if (submitted.ValueKind == JsonValueKind.Object)
                    ReadDeck(submitted,timestamp,isSelection:true);
            }
            else
            {
                // Some Arena versions emit the submission directly as JSON.
                ReadDeckSubmission(root, timestamp);
            }
        }
        // Deck summaries alone may describe inventory, not active play.
        var summary = Locate(root,"CourseDeckSummary");
        if (summary.ValueKind == JsonValueKind.Object && eventLabel != "course_deck")
            ReadDeck(summary, timestamp, isSelection:false);
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
        // Arena match-room events usually omit a deckId. A recent,
        // explicit submission to Arena's queue is the trustworthy link.
        // Do not use mere deck inventory or an indefinite "last deck".
        if (info.DeckId.Length == 0 && currentDeckId is { Length: > 0 } &&
            latestLogDeckSelection.HasValue && timestamp.HasValue &&
            (timestamp.Value - latestLogDeckSelection.Value).TotalMinutes is >= 0 and <= 20)
        {
            info.DeckId = currentDeckId;
            if (decksByArenaId.TryGetValue(currentDeckId, out var queued))
            {
                info.DeckName = queued.Name;
                info.NativeDeckId = queued.NativeId;
                info.DeckVersion = queued.Version;
                info.Assignment = "recent_arena_deck_submission";
            }
            else info.Assignment = "unmapped_arena_deck_submission";
        }
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
                {
                    info.DeckName = known.Name;
                    info.NativeDeckId = known.NativeId;
                    info.DeckVersion = known.Version;
                    info.Assignment = "verified_arena_deck_id";
                }
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
        {
            info.DeckName = mapped.Name;
            info.NativeDeckId = mapped.NativeId;
            info.DeckVersion = mapped.Version;
            info.Assignment = "verified_arena_deck_id";
        }
        if (info.DeckName.Length == 0 && userChoseActiveDeck)
        { info.DeckName = currentDeckName ?? ""; info.DeckVersion = currentDeckVersion ?? ""; info.Assignment = "manual_active_deck"; }
        var occurred = timestamp ?? info.Started ?? DateTimeOffset.UtcNow;
        var note = outcome=="unknown" ? "Player/team identity not verified; review result manually." :
            info.DeckName.Length==0 ?
                (info.DeckId.Length == 0 ?
                    "No Arena deck ID in match log; choose a deck manually." :
                    blockedArenaIds.Contains(info.DeckId) ?
                        "Arena deck ID links to multiple saved decks; assign manually." :
                        "Arena deck ID has no verified saved-deck mapping; assign manually.") :
            "Deck assignment: " + info.Assignment;
        history.Add(new MatchHistory.MatchRow(idMatch, occurred.ToUniversalTime().ToString("O"),outcome,
            outcome=="unknown"?"unverified":"arena_match_result",info.Format,info.DeckId,info.DeckName,info.DeckVersion,
            Path.GetFileName(source),note,gw,gl,info.NativeDeckId));
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
        if (id.Length is < 1 or > 180 || name.Length > 150) return;
        // Queue submissions can include only the Arena ID and format.
        // A name is optional when an exact persisted mapping exists.
        if (name.Length == 0 && decksByArenaId.TryGetValue(id, out var linked))
            name = linked.Name;
        // Arena's summary may reference unrelated decks; it must not
        // override verified native or user-confirmed persistent mappings.
        if (name.Length > 0 && !blockedArenaIds.Contains(id) &&
            !decksByArenaId.ContainsKey(id))
            decksByArenaId[id] = (name, "", "");
        if (!blockedArenaIds.Contains(id) &&
            decksByArenaId.TryGetValue(id, out var knownDeck))
        {
            if (knownDeck.NativeId.Length > 0)
                history.BackfillExactArenaId(id, knownDeck.NativeId, knownDeck.Name);
            else if (knownDeck.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                history.AssignUnassignedByDeckId(id, name);
        }
        // Do not replace an intentionally selected deck label until we observe the exact deck ID in match data.
        if (isSelection && timestamp.HasValue)
        {
            currentDeckId = id;
            latestLogDeckSelection = timestamp;
            // Do not claim a saved deck unless a verified ID resolves.
            if (!userChoseActiveDeck)
                currentDeckName = decksByArenaId.TryGetValue(id, out var selected)
                    ? selected.Name : "";
        }
    }
    public void Dispose() => history.Dispose();
}
