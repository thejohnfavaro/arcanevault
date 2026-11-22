using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// Partial, read-only opponent color evidence from public GRE zones. Never
/// promotes observed colors to a claim about an opponent's complete deck.
/// Opponent ownership requires an authenticated local seat in the same match.
/// </summary>
internal static class OpponentColorObservation
{
    internal sealed record MatchEvidence(string Colors, int DistinctArenaCards);
    internal sealed record Report(IReadOnlyDictionary<string, MatchEvidence> Matches,
        int ObservedCardIds, int UnresolvedCardIds, bool CatalogHasArenaIds);

    private static readonly HashSet<string> PublicZones = new(
        new[] { "ZoneType_Battlefield", "ZoneType_Graveyard",
            "ZoneType_Exile", "ZoneType_Stack" }, StringComparer.OrdinalIgnoreCase);

    internal static Report Read(SqliteConnection history, string? catalogPath,
        IReadOnlySet<string>? selectedMatchIds = null)
    {
        ArgumentNullException.ThrowIfNull(history);
        var evidence = new Dictionary<string, HashSet<long>>(StringComparer.Ordinal);
        using (var query = history.CreateCommand())
        {
            query.CommandText = @"SELECT t.match_id,s.seat_id,t.payload_json
                FROM arena_match_telemetry t
                INNER JOIN arena_verified_local_seats s ON s.match_id=t.match_id
                WHERE s.seat_id>=0" +
                (selectedMatchIds is null ? "" :
                    " AND t.match_id IN (SELECT value FROM json_each($ids))") +
                " ORDER BY t.id DESC LIMIT 10000";
            if (selectedMatchIds is not null)
                query.Parameters.AddWithValue("$ids", JsonSerializer.Serialize(
                    selectedMatchIds.OrderBy(x => x, StringComparer.Ordinal).ToArray()));
            using var rows = query.ExecuteReader();
            while (rows.Read())
            {
                string matchId = rows.GetString(0);
                int localSeat = rows.GetInt32(1);
                if (localSeat is < 0 or > 100) continue;
                try
                {
                    using var doc = JsonDocument.Parse(rows.GetString(2));
                    var ids = PublicOpponentCardIds(doc.RootElement, localSeat);
                    if (ids.Count == 0) continue;
                    if (!evidence.TryGetValue(matchId, out var cards))
                        evidence[matchId] = cards = new HashSet<long>();
                    cards.UnionWith(ids);
                }
                catch (JsonException)
                {
                    // Damaged historical telemetry is not color evidence.
                }
            }
        }

        var wanted = evidence.Values.SelectMany(x => x).Distinct().Take(5000).ToArray();
        if (wanted.Length == 0)
            return new(new Dictionary<string, MatchEvidence>(), 0, 0, CatalogHasIds(catalogPath));

        var colorsById = new Dictionary<long, string>();
        bool catalogReady = false;
        if (CatalogHasIds(catalogPath))
        {
            using var catalog = new SqliteConnection(
                new SqliteConnectionStringBuilder {
                    DataSource = catalogPath, Mode = SqliteOpenMode.ReadOnly, Pooling = false
                }.ToString());
            catalog.Open();
            using var lookup = catalog.CreateCommand();
            lookup.CommandText = @"SELECT v.arena_id,c.colors
                FROM variants v INNER JOIN cards c ON c.name=v.name COLLATE NOCASE
                WHERE v.arena_id IN (SELECT CAST(value AS INTEGER) FROM json_each($ids))
                  AND v.arena_id>0";
            lookup.Parameters.AddWithValue("$ids", JsonSerializer.Serialize(wanted));
            var conflicts = new HashSet<long>();
            using var reader = lookup.ExecuteReader();
            while (reader.Read())
            {
                long id = reader.GetInt64(0);
                string identity = Canonical(reader.GetString(1));
                if (identity.Length == 0 && reader.GetString(1).Length > 0) continue;
                if (colorsById.TryGetValue(id, out var old) && old != identity)
                    conflicts.Add(id);
                else colorsById[id] = identity;
            }
            foreach (long id in conflicts) colorsById.Remove(id);
            catalogReady = true;
        }
        var matches = new Dictionary<string, MatchEvidence>(StringComparer.Ordinal);
        foreach (var pair in evidence)
        {
            var resolved = pair.Value.Where(colorsById.ContainsKey).ToArray();
            var colors = Canonical(string.Concat(resolved.Select(id => colorsById[id])));
            if (colors.Length > 0)
                matches[pair.Key] = new MatchEvidence(colors, resolved.Length);
        }
        return new(matches, wanted.Length,
            wanted.Count(id => !colorsById.ContainsKey(id)), catalogReady);
    }

    // The local Scryfall catalog must be refreshed once after the new
    // arena_id printing column ships. Older catalogs remain read-only.
    private static bool CatalogHasIds(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;
        using var db = new SqliteConnection(new SqliteConnectionStringBuilder {
            DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false
        }.ToString());
        db.Open();
        using var schema = db.CreateCommand();
        schema.CommandText = "PRAGMA table_info(variants)";
        using var reader = schema.ExecuteReader();
        while (reader.Read())
            if (reader.GetString(1).Equals("arena_id",
                StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    internal static IReadOnlySet<long> PublicOpponentCardIds(JsonElement state, int localSeat)
    {
        var result = new HashSet<long>();
        if (state.ValueKind != JsonValueKind.Object ||
            !state.TryGetProperty("zones", out var zones) ||
            zones.ValueKind != JsonValueKind.Array ||
            !state.TryGetProperty("objects", out var objects) ||
            objects.ValueKind != JsonValueKind.Array) return result;
        var publicZoneIds = new HashSet<int>();
        foreach (var zone in zones.EnumerateArray())
        {
            if (zone.ValueKind != JsonValueKind.Object ||
                !Number(zone, "zoneId", out int zoneId) ||
                !zone.TryGetProperty("type", out var type) ||
                type.ValueKind != JsonValueKind.String ||
                !PublicZones.Contains(type.GetString() ?? "") ||
                Hidden(zone)) continue;
            publicZoneIds.Add(zoneId);
        }
        foreach (var card in objects.EnumerateArray())
        {
            if (card.ValueKind != JsonValueKind.Object ||
                !Number(card, "zoneId", out int zoneId) ||
                !publicZoneIds.Contains(zoneId) ||
                !Number(card, "ownerSeatId", out int owner) ||
                owner is < 0 or > 100 || owner == localSeat ||
                Hidden(card)) continue;
            if (CardId(card, "grpId", out long id) ||
                CardId(card, "objectSourceGrpId", out id))
                result.Add(id);
        }
        return result;
    }

    private static bool Hidden(JsonElement value)
    {
        if (!value.TryGetProperty("visibility", out var field)) return false;
        if (field.ValueKind != JsonValueKind.String) return true;
        var raw = field.GetString() ?? "";
        // Unknown visibility is not proof of public information.
        return !raw.Equals("Visibility_Public", StringComparison.OrdinalIgnoreCase) &&
               !raw.Equals("Visibility_VisibleToAll", StringComparison.OrdinalIgnoreCase) &&
               !raw.Equals("Visibility_Any", StringComparison.OrdinalIgnoreCase);
    }

    private static bool Number(JsonElement value, string property, out int result)
    {
        result = 0;
        return value.TryGetProperty(property, out var field) &&
            field.ValueKind == JsonValueKind.Number &&
            field.TryGetInt32(out result);
    }

    private static bool CardId(JsonElement value, string property, out long id)
    {
        id = 0;
        return value.TryGetProperty(property, out var field) &&
            field.ValueKind == JsonValueKind.Number &&
            field.TryGetInt64(out id) && id > 0;
    }

    internal static string Canonical(string? raw)
    {
        if (raw is null || raw.Any(x => !"WUBRG".Contains(x))) return "";
        return string.Concat("WUBRG".Where(raw.Contains));
    }
}
