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
    private DateTimeOffset? lastRead;
    private string? error;
    private int eventsSeen;
    private readonly Dictionary<string, int> inventory = new();
    private DateTimeOffset? inventoryAt;
    private string? constructedRank;
    private DateTimeOffset? rankAt;
    private List<string> deckSummaries = new();
    private DateTimeOffset? decksAt;
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
            remainder = lines[^1].Length > 8192 ? "" : lines[^1];
            for (int i = 0; i < lines.Length - 1; i++)
            {
                var line = lines[i].Trim();
                // Very large events may include multiline JSON, which is not currently parsed.
                if (line.Length is > 5 and < 2_000_000) ExamineLine(line);
            }
            lastRead = DateTimeOffset.UtcNow;
            error = null;
        }
        catch (IOException ex) { error = "Unable to read Arena log: " + ex.Message; }
        catch (UnauthorizedAccessException) { error = "Log access denied. Check the path and file permissions."; }
    }

    private void ExamineLine(string line)
    {
        if (!(line.Contains("StartHook", StringComparison.OrdinalIgnoreCase) ||
              line.Contains("InventoryInfo", StringComparison.OrdinalIgnoreCase) ||
              line.Contains("wcCommon", StringComparison.OrdinalIgnoreCase) ||
              line.Contains("constructedClass", StringComparison.OrdinalIgnoreCase) ||
              line.Contains("DeckSummariesV2", StringComparison.OrdinalIgnoreCase))) return;
        var first = line.IndexOf('{');
        if (first < 0) return;
        try
        {
            using var doc = JsonDocument.Parse(line[first..]);
            eventsSeen++;
            Visit(doc.RootElement, 0);
        }
        catch (JsonException) { /* Some logs split the header and payload; ignore uncertain entries. */ }
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
