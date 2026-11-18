using System.Text.Json;

namespace ArcaneVault.Next.Desktop;

/// <summary>Conservative queue labels: raw Arena event IDs remain authoritative.
/// Unknown IDs are displayed verbatim instead of being guessed from deck type.</summary>
internal static class ArenaQueueInfo
{
    public static string Label(string? raw)
    {
        string value = (raw ?? "").Trim();
        if (value.Length == 0) return "";
        if (value.Length > 180) return "";
        string normalized = value.Replace('-', '_').Replace(' ', '_').ToLowerInvariant();
        // Competitive Brawl uses a separate ranked Constructed ladder. Check
        // its distinctive names BEFORE the generic Brawl substring.
        if (normalized.Contains("competitivebrawl") ||
            normalized.Contains("competitive_brawl") ||
            normalized.Contains("rankedbrawl") ||
            normalized.Contains("ranked_brawl") ||
            normalized.Contains("brawl_ladder") ||
            normalized.Contains("brawl_competitive") ||
            normalized.Contains("brawl_ranked"))
            return "Competitive Brawl";
        if (normalized is "brawl" or "brawl_play" or "unranked_brawl" or
            "brawl_unranked") return "Brawl";
        // An unfamiliar Brawl event could be Competitive, an event or
        // a custom queue. Preserve the raw identifier rather than merging it
        // into normal Brawl without evidence.
        if (normalized.Contains("brawl")) return value;
        // Preserve ranked and play distinctly; Arena has many changing IDs.
        bool ranked = normalized.Contains("ladder") || normalized.Contains("ranked");
        string[] known = {"standard","historic","timeless","explorer","pioneer","alchemy",
            "modern","pauper"};
        foreach (var format in known)
            if (normalized.Contains(format))
                return char.ToUpperInvariant(format[0]) + format[1..] + (ranked ? " Ranked" : "");
        if (normalized.Contains("quickdraft") || normalized.Contains("quick_draft")) return "Quick Draft";
        if (normalized.Contains("premierdraft") || normalized.Contains("premier_draft")) return "Premier Draft";
        if (normalized.Contains("traditionaldraft") || normalized.Contains("traditional_draft"))
            return "Traditional Draft";
        return value; // Keep raw event ID visible, never mislabel an event.
    }

    public static string EventId(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) return "";
        foreach (var name in new[] {"eventId", "eventID", "EventName", "eventName", "eventIdName"})
            if (root.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                var value = prop.GetString()?.Trim() ?? "";
                if (value.Length is > 0 and <= 180) return value;
            }
        return "";
    }

    public static string FromMatchConfig(JsonElement config)
    {
        string direct = EventId(config);
        if (direct.Length > 0) return direct;
        if (config.ValueKind == JsonValueKind.Object &&
            config.TryGetProperty("reservedPlayers", out var players) &&
            players.ValueKind == JsonValueKind.Array)
        {
            var choices = players.EnumerateArray().Select(EventId)
                .Where(x => x.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase).Take(2).ToArray();
            // No assumption about player seat when IDs disagree.
            if (choices.Length == 1) return choices[0];
        }
        return "";
    }
}
