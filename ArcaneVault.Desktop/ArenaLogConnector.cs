using System.Text;
using System.Text.Json;

namespace ArcaneVault.Desktop;

/// <summary>
/// Opt-in, read-only MTG Arena Player.log reader. No Arena process access, memory scanning,
/// account tokens, or outbound upload. Only explicitly recognized non-sensitive fields are retained.
/// Arena no longer exposes a dependable full owned-card snapshot in its regular logs.
/// </summary>
internal sealed class ArenaLogConnector
{
    private readonly object gate = new();
    private string? logPath;
    private bool enabled;
    private long offset;
    private string remainder = "";
    private readonly StringBuilder pendingDeckEvent = new();
    private int eventBalance;
    private bool eventQuoted, eventEscaped;
    private const int MaxDeckEventBytes = 2_000_000;
    private DateTimeOffset? lastRead;
    private string? error;
    private int eventsSeen;
    private readonly Dictionary<string, int> inventory = new();
    private DateTimeOffset? inventoryAt;
    private string? constructedRank;
    private DateTimeOffset? rankAt;
    private List<string> deckSummaries = new();
    private DateTimeOffset? decksAt;
    private readonly Dictionary<string, ArenaDeckPayload> deckLists = new(StringComparer.OrdinalIgnoreCase);
    internal record ArenaCardEntry(string card, int qty);
    internal record ArenaDeckPayload(string deckId, string name, ArenaCardEntry[] commander, ArenaCardEntry[] main, string observedAt);
    private const int MaxInitialBytes = 16 * 1024 * 1024;
    private const int MaxReadBytes = 4 * 1024 * 1024;
    private static readonly string[] Stats = ["gold", "gems", "wcCommon", "wcUncommon", "wcRare", "wcMythic", "draftTokens", "sealedTokens"];

    public ArenaLogConnector() => logPath = DefaultLogPath();

    public static string DefaultLogPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "AppData", "LocalLow", "Wizards Of The Coast", "MTGA", "Player.log");

    public void Configure(bool monitor, string? customPath = null)
    {
        lock (gate)
        {
            if (!string.IsNullOrWhiteSpace(customPath))
            {
                var full = Path.GetFullPath(Environment.ExpandEnvironmentVariables(customPath.Trim()));
                if (!string.Equals(Path.GetFileName(full), "Player.log", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(Path.GetFileName(full), "Player-prev.log", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("Select an Arena Player.log or Player-prev.log file.");
                logPath = full;
            }
            enabled = monitor;
            offset = 0;
            remainder = "";
            pendingDeckEvent.Clear(); eventBalance=0; eventQuoted=false; eventEscaped=false;
            error = null;
        }
    }

    public object Snapshot()
    {
        lock (gate)
        {
            if (enabled) Scan();
            var exists = logPath != null && File.Exists(logPath);
            return new
            {
                enabled,
                detected = exists,
                path = logPath,
                lastRead,
                error,
                eventsSeen,
                inventory = new Dictionary<string, int>(inventory),
                inventoryAt,
                constructedRank,
                rankAt,
                deckSummaries = deckSummaries.ToArray(),
                deckLists = deckLists.Values.OrderBy(d => d.name, StringComparer.OrdinalIgnoreCase).Take(250).ToArray(),
                decksAt,
                collectionOwnership = "CSV snapshot only — full owned-card collection is not available reliably from Player.log",
                readOnly = true
            };
        }
    }

    private void Scan()
    {
        if (string.IsNullOrWhiteSpace(logPath) || !File.Exists(logPath))
        {
            error = "Player.log not found. Enable Detailed Logs in Arena and restart it, or choose a log file.";
            return;
        }
        try
        {
            using var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length < offset) { offset = 0; remainder = ""; }
            if (offset == 0 && stream.Length > MaxInitialBytes)
                offset = stream.Length - MaxInitialBytes;
            if (stream.Length == offset) { error = null; return; }
            stream.Position = offset;
            var toRead = (int)Math.Min(MaxReadBytes, stream.Length - offset);
            var bytes = new byte[toRead];
            var read = stream.Read(bytes, 0, toRead);
            offset += read;
            var chunk = remainder + Encoding.UTF8.GetString(bytes, 0, read);
            var lines = chunk.Split('\n');
            remainder = lines[^1].Length > MaxDeckEventBytes ? "" : lines[^1];
            for (int i = 0; i < lines.Length - 1; i++)
            {
                var line = lines[i].Trim();
                // Very large events may include multiline JSON, which is not currently parsed.
                if (line.Length is > 5 and < MaxDeckEventBytes) ExamineLine(line);
            }
            lastRead = DateTimeOffset.UtcNow;
            error = null;
        }
        catch (IOException ex) { error = "Unable to read Arena log: " + ex.Message; }
        catch (UnauthorizedAccessException) { error = "Log access denied. Check the path and file permissions."; }
    }

    private void ExamineLine(string line)
    {
        if(pendingDeckEvent.Length==0)
        {
            if (!(line.Contains("StartHook", StringComparison.OrdinalIgnoreCase) ||
                  line.Contains("InventoryInfo", StringComparison.OrdinalIgnoreCase) ||
                  line.Contains("wcCommon", StringComparison.OrdinalIgnoreCase) ||
                  line.Contains("constructedClass", StringComparison.OrdinalIgnoreCase) ||
                  line.Contains("DeckSummariesV2", StringComparison.OrdinalIgnoreCase) ||
                  line.Contains("EventSetDeckV2", StringComparison.OrdinalIgnoreCase) ||
                  line.Contains("DecksV3", StringComparison.OrdinalIgnoreCase))) return;
            var first=line.IndexOf('{');
            if(first<0)return;
            line=line[first..];
        }
        if (pendingDeckEvent.Length + line.Length > MaxDeckEventBytes)
        { pendingDeckEvent.Clear(); eventBalance=0; eventQuoted=false; eventEscaped=false; return; }
        pendingDeckEvent.AppendLine(line);
        foreach (var ch in line)
        {
            if(eventEscaped){eventEscaped=false;continue;}
            if(eventQuoted && ch=='\\'){eventEscaped=true;continue;}
            if(ch=='"')eventQuoted=!eventQuoted;
            if(!eventQuoted){if(ch=='{')eventBalance++;else if(ch=='}')eventBalance--;}
        }
        if(eventBalance>0)return;
        try
        {
            using var doc=JsonDocument.Parse(pendingDeckEvent.ToString());
            eventsSeen++;
            Visit(doc.RootElement,0);
        }
        catch (JsonException) { /* Missing or malformed log JSON is never imported. */ }
        finally{pendingDeckEvent.Clear();eventBalance=0;eventQuoted=false;eventEscaped=false;}
    }

    // Reads only explicit deck objects; never interprets in-game battlefield or
    // other player card arrays as a saved deck. Numeric IDs stay unresolved.
    private static bool Find(JsonElement obj, out JsonElement value, params string[] names)
    {
        if (obj.ValueKind == JsonValueKind.Object)
            foreach (var name in names)
                if (obj.TryGetProperty(name, out value)) return true;
        value = default;
        return false;
    }
    private static string Text(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? "",
            JsonValueKind.Number => value.GetRawText(),
            _ => ""
        };
    }
    internal static ArenaCardEntry[] ReadCards(JsonElement deck, params string[] keys)
    {
        if (!Find(deck,out var array,keys) || array.ValueKind != JsonValueKind.Array) return [];
        var cards = new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
        foreach(var item in array.EnumerateArray().Take(300))
        {
            string card = "";
            int qty = 1;
            if (item.ValueKind == JsonValueKind.Array)
            {
                var tuple = item.EnumerateArray().Take(2).ToArray();
                if (tuple.Length > 0) card=Text(tuple[0]);
                if (tuple.Length > 1 && tuple[1].ValueKind==JsonValueKind.Number && tuple[1].TryGetInt32(out var count)) qty=count;
            }
            else if (item.ValueKind == JsonValueKind.Object)
            {
                if (Find(item,out var id,"cardId","CardId","id","Id","name","Name")) card=Text(id);
                if (Find(item,out var quantity,"quantity","Quantity","qty","Qty","count","Count") && quantity.ValueKind==JsonValueKind.Number && quantity.TryGetInt32(out var count)) qty=count;
            }
            if(card.Length is < 1 or > 200 || qty is < 1 or > 250) continue;
            if(cards.Count<250 || cards.ContainsKey(card)) cards[card]=Math.Min(250,cards.GetValueOrDefault(card)+qty);
        }
        return cards.Select(pair=>new ArenaCardEntry(pair.Key,pair.Value)).ToArray();
    }
    private void TryCaptureDeck(JsonElement obj)
    {
        if (!Find(obj,out var deckId,"DeckId","deckId") || !Find(obj,out var nameValue,"Name","name","DeckName","deckName")) return;
        var deckKey=Text(deckId);
        var name=Text(nameValue);
        if(deckKey.Length is < 1 or > 180 || name.Length is < 1 or > 150) return;
        var main=ReadCards(obj,"MainDeck","mainDeck","mainboard","Mainboard","Cards","cards");
        if(main.Length==0)return; // A summary without card entries is not importable.
        var command=ReadCards(obj,"CommandZone","commandZone","Commander","commander");
        // Non-Brawl lists can be observed, but require a commander for our Brawl library.
        deckLists[deckKey]=new ArenaDeckPayload(deckKey,name,command,main,DateTimeOffset.UtcNow.ToString("O"));
        if(deckLists.Count>250)deckLists.Remove(deckLists.Keys.First());
        decksAt=DateTimeOffset.UtcNow;
    }

    private void Visit(JsonElement element, int depth)
    {
        if (depth > 12) return;
        if (element.ValueKind == JsonValueKind.Object)
        {
            int found = 0;
            var stats = new Dictionary<string, int>();
            foreach (var name in Stats)
            {
                if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) && number >= 0)
                {
                    stats[name] = number;
                    if (name.StartsWith("wc", StringComparison.Ordinal)) found++;
                }
            }
            // Only capture an inventory SNAPSHOT with multiple counters; don't confuse deltas with balances.
            if (found >= 2)
            {
                foreach (var (name, value) in stats) inventory[name] = value;
                inventoryAt = DateTimeOffset.UtcNow;
            }
            if (element.TryGetProperty("constructedClass", out var cls) && cls.ValueKind == JsonValueKind.String)
            {
                var label = cls.GetString();
                if (label?.Length is > 0 and < 40)
                {
                    var lvl = element.TryGetProperty("constructedLevel", out var level) && level.ValueKind == JsonValueKind.Number ? level.ToString() : "";
                    constructedRank = label + (lvl == "" ? "" : " " + lvl);
                    rankAt = DateTimeOffset.UtcNow;
                }
            }
            TryCaptureDeck(element);
            if (element.TryGetProperty("DeckSummariesV2", out var decks) && decks.ValueKind == JsonValueKind.Array)
            {
                var names = new List<string>();
                foreach (var d in decks.EnumerateArray())
                {
                    if (d.ValueKind != JsonValueKind.Object) continue;
                    foreach (var nameField in new[] { "Name", "name", "DeckName", "deckName" })
                    {
                        if (d.TryGetProperty(nameField, out var nm) && nm.ValueKind == JsonValueKind.String)
                        {
                            var name = nm.GetString();
                            if (name is { Length: > 0 and < 150 }) names.Add(name);
                            break;
                        }
                    }
                }
                if (names.Count > 0)
                {
                    deckSummaries = names.Distinct(StringComparer.Ordinal).Take(250).ToList();
                    decksAt = DateTimeOffset.UtcNow;
                }
            }
            foreach (var property in element.EnumerateObject()) Visit(property.Value, depth + 1);
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            // Avoid traversing enormous game-state arrays, which aren't needed for this connector.
            if (element.GetArrayLength() > 1000) return;
            foreach (var child in element.EnumerateArray()) Visit(child, depth + 1);
        }
        else if (element.ValueKind == JsonValueKind.String && depth < 8)
        {
            var value = element.GetString();
            if (value is { Length: > 6 and < 1_000_000 } && value[0] == '{')
                try { using var nested = JsonDocument.Parse(value); Visit(nested.RootElement, depth + 1); }
                catch (JsonException) { }
        }
    }
}
