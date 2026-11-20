using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// Privacy-minimized, versioned Arena GRE telemetry. Retains only a bounded
/// allowlist of gameplay mechanics (no account identifiers, authentication,
/// player names, chat, HTTP requests, or opaque raw log envelopes).
/// This is source evidence for future analysis, NOT a reconstructed replay.
/// </summary>
internal static class ArenaMatchTelemetry
{
    internal const int SchemaVersion = 1;
    internal const int MaxMessagesPerEnvelope = 256;
    internal const int MaxJsonBytesPerMessage = 96 * 1024;
    internal const int MaxTelemetryRowsPerMatch = 12000;

    private static readonly string[] TurnFields = {
        "turnNumber", "phase", "step", "activePlayer", "decisionPlayer", "priorityPlayer",
        "activePlayerSeatId", "decisionPlayerSeatId"
    };
    private static readonly string[] InfoFields = {
        "stage", "matchState", "gameNumber", "gameStateId", "gameId", "matchId"
    };
    private static readonly string[] PlayerFields = {
        "systemSeatNumber", "systemSeatId", "seatId", "lifeTotal", "startingLifeTotal",
        "poisonCount", "energyCount", "teamId", "status"
    };
    private static readonly string[] ObjectFields = {
        "instanceId", "grpId", "objectSourceGrpId", "zoneId", "ownerSeatId",
        "controllerSeatId", "visibility", "cardTypes", "subtypes", "power",
        "toughness", "isTapped", "counterCount", "counters"
    };
    private static readonly string[] ZoneFields = {
        "zoneId", "type", "ownerSeatId", "objectInstanceIds", "visibility"
    };
    private static readonly string[] AnnotationFields = {
        "id", "affectorId", "affectedIds", "type", "details"
    };
    private static readonly string[] DetailFields = {
        "key", "type", "valueInt32", "valueUint32", "valueString", "valueBool",
        "valueFloat", "valueInt64"
    };
    private static readonly string[] ResultFields = {
        "scope", "result", "winningTeamId", "reason", "winningSeatId"
    };

    private static JsonElement? Pick(JsonElement item, IReadOnlyList<string> names,
        bool hideStrings = false)
    {
        if (item.ValueKind != JsonValueKind.Object) return null;
        var value = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var name in names)
        {
            if (!item.TryGetProperty(name, out var field)) continue;
            // Never copy arbitrary objects. Only mechanics from fixed
            // allowlisted properties, and restrict untrusted string values.
            if (field.ValueKind == JsonValueKind.String &&
                (field.GetString()?.Length ?? 0) > 100) continue;
            if (hideStrings && field.ValueKind == JsonValueKind.String) continue;
            if (field.ValueKind is JsonValueKind.Object or JsonValueKind.Null or JsonValueKind.Undefined)
                continue;
            if (field.ValueKind == JsonValueKind.Array &&
                (field.GetArrayLength() > 250 ||
                 field.EnumerateArray().Any(e => e.ValueKind is JsonValueKind.Object
                    or JsonValueKind.Array or JsonValueKind.Null))) continue;
            value[name] = field.Clone();
        }
        return value.Count == 0 ? null : JsonSerializer.SerializeToElement(value);
    }

    private static object[] PickArray(JsonElement state, string key,
        IReadOnlyList<string> fields, int maxCount)
    {
        if (!state.TryGetProperty(key, out var list) ||
            list.ValueKind != JsonValueKind.Array) return [];
        return list.EnumerateArray().Take(maxCount)
            .Select(x => Pick(x, fields))
            .Where(x => x.HasValue).Select(x => (object)x!.Value).ToArray();
    }

    private static object[] PickAnnotations(JsonElement state)
    {
        if (!state.TryGetProperty("annotations", out var list) ||
            list.ValueKind != JsonValueKind.Array) return [];
        return list.EnumerateArray().Take(150).Select(annotation =>
        {
            var header = Pick(annotation, AnnotationFields);
            if (header is null) return null;
            // Annotation detail values have a fixed and well-understood
            // mechanics vocabulary. Retain keys and numeric values, but
            // discard any string value that could be a user-supplied name.
            var fields = new Dictionary<string, object?>();
            foreach (var property in header.Value.EnumerateObject())
                if (property.Name != "details") fields[property.Name] = property.Value.Clone();
            if (annotation.TryGetProperty("details", out var details) &&
                details.ValueKind == JsonValueKind.Array)
            {
                fields["details"] = details.EnumerateArray().Take(35)
                    .Select(x => Pick(x, DetailFields))
                    .Where(x => x.HasValue).Select(x => (object)x!.Value).ToArray();
            }
            return (object?)fields;
        }).Where(x => x is not null).Select(x => x!).ToArray();
    }

    internal sealed record Observed(string MatchId, string Kind, int? GameNumber,
        int? TurnNumber, string PayloadJson, string Fingerprint);

    internal static IReadOnlyList<Observed> Extract(JsonElement root, string matchId)
    {
        if (string.IsNullOrWhiteSpace(matchId) || matchId.Length > 180 ||
            root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("greToClientEvent", out var gre) ||
            gre.ValueKind != JsonValueKind.Object ||
            !gre.TryGetProperty("greToClientMessages", out var messages) ||
            messages.ValueKind != JsonValueKind.Array) return [];
        var output = new List<Observed>();
        foreach (var msg in messages.EnumerateArray().Take(MaxMessagesPerEnvelope))
        {
            if (msg.ValueKind != JsonValueKind.Object ||
                !msg.TryGetProperty("type", out var typ) ||
                typ.ValueKind != JsonValueKind.String) continue;
            var kind = typ.GetString() ?? "";
            if (kind is not ("GREMessageType_GameStateMessage"
                or "GREMessageType_QueuedGameStateMessage"
                or "GREMessageType_ConnectResp"
                or "GREMessageType_IntermissionReq")) continue;
            if (!msg.TryGetProperty("gameStateMessage", out var state) ||
                state.ValueKind != JsonValueKind.Object) continue;
            var payload = new Dictionary<string, object?>();
            if (state.TryGetProperty("turnInfo", out var turn) &&
                Pick(turn, TurnFields) is { } t) payload["turn"] = t;
            if (state.TryGetProperty("gameInfo", out var info) &&
                Pick(info, InfoFields) is { } g) payload["game"] = g;
            var players = PickArray(state, "players", PlayerFields, 8);
            if (players.Length > 0) payload["players"] = players;
            var zones = PickArray(state, "zones", ZoneFields, 40);
            if (zones.Length > 0) payload["zones"] = zones;
            var objects = PickArray(state, "gameObjects", ObjectFields, 500);
            if (objects.Length > 0) payload["objects"] = objects;
            var annotations = PickAnnotations(state);
            if (annotations.Length > 0) payload["annotations"] = annotations;
            var results = PickArray(state, "results", ResultFields, 10);
            if (results.Length > 0) payload["results"] = results;
            if (state.TryGetProperty("diffDeletedInstanceIds", out var deleted) &&
                deleted.ValueKind == JsonValueKind.Array &&
                deleted.GetArrayLength() <= 250 &&
                deleted.EnumerateArray().All(x => x.ValueKind == JsonValueKind.Number))
                payload["deletedIds"] = deleted.Clone();
            if (payload.Count == 0) continue;
            string json = JsonSerializer.Serialize(payload);
            if (Encoding.UTF8.GetByteCount(json) > MaxJsonBytesPerMessage) continue;
            int? gameNumber = Number(infoOrNull: state, container: "gameInfo", field: "gameNumber");
            int? turnNumber = Number(infoOrNull: state, container: "turnInfo", field: "turnNumber");
            string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                matchId + "\n" + kind + "\n" + json)));
            output.Add(new Observed(matchId, kind, gameNumber, turnNumber, json, hash));
        }
        return output;
    }

    private static int? Number(JsonElement infoOrNull, string container, string field)
    {
        if (infoOrNull.TryGetProperty(container, out var data) &&
            data.ValueKind == JsonValueKind.Object &&
            data.TryGetProperty(field, out var number) &&
            number.ValueKind == JsonValueKind.Number &&
            number.TryGetInt32(out var value)) return value;
        return null;
    }

    internal static void EnsureSchema(SqliteConnection db)
    {
        using var command = db.CreateCommand();
        command.CommandText = @"CREATE TABLE IF NOT EXISTS arena_match_telemetry (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            match_id TEXT NOT NULL,
            observed_utc TEXT NOT NULL,
            event_kind TEXT NOT NULL,
            game_number INTEGER NULL,
            turn_number INTEGER NULL,
            schema_version INTEGER NOT NULL,
            payload_json TEXT NOT NULL,
            fingerprint TEXT NOT NULL UNIQUE
        );
        CREATE INDEX IF NOT EXISTS idx_arena_match_telemetry_match
            ON arena_match_telemetry(match_id,game_number,id);";
        command.ExecuteNonQuery();
    }

    internal static int Insert(SqliteConnection db, IEnumerable<Observed> events,
        DateTimeOffset observedAt)
    {
        int inserted = 0;
        foreach (var item in events)
        {
            using var command = db.CreateCommand();
            command.CommandText = @"INSERT OR IGNORE INTO arena_match_telemetry
              (match_id,observed_utc,event_kind,game_number,turn_number,
               schema_version,payload_json,fingerprint)
              SELECT $match,$time,$kind,$game,$turn,$version,$payload,$hash
              WHERE (SELECT COUNT(*) FROM arena_match_telemetry WHERE match_id=$match)
                    < $max";
            command.Parameters.AddWithValue("$match", item.MatchId);
            command.Parameters.AddWithValue("$time", observedAt.ToUniversalTime().ToString("O"));
            command.Parameters.AddWithValue("$kind", item.Kind);
            command.Parameters.AddWithValue("$game", (object?)item.GameNumber ?? DBNull.Value);
            command.Parameters.AddWithValue("$turn", (object?)item.TurnNumber ?? DBNull.Value);
            command.Parameters.AddWithValue("$version", SchemaVersion);
            command.Parameters.AddWithValue("$payload", item.PayloadJson);
            command.Parameters.AddWithValue("$hash", item.Fingerprint);
            command.Parameters.AddWithValue("$max", MaxTelemetryRowsPerMatch);
            inserted += command.ExecuteNonQuery();
        }
        return inserted;
    }
}
