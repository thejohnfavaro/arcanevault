using System.Text.Json;

namespace ArcaneVault.Next.Desktop;

/// <summary>Local rank observations from Arena's RankGetCombinedRankInfo.
/// No interpolation, no MMR, no claim of a rank at an earlier match.</summary>
internal static class ArenaRankInfo
{
    internal sealed record Snapshot(string ConstructedClass, int ConstructedLevel,
        string LimitedClass, int LimitedLevel, string Source);

    internal static Snapshot? TryRead(JsonElement root, string source)
    {
        // Only accept dedicated rank endpoint responses or known combined rank
        // payloads. Rank from other players or arbitrary inventory is ignored.
        if (root.ValueKind != JsonValueKind.Object) return null;
        var candidate = root;
        if (!HasRankKeys(candidate))
        {
            if (root.TryGetProperty("payload", out var payload) &&
                payload.ValueKind == JsonValueKind.Object && HasRankKeys(payload))
                candidate = payload;
            else if (root.TryGetProperty("result", out var result) &&
                result.ValueKind == JsonValueKind.Object && HasRankKeys(result))
                candidate = result;
            else return null;
        }
        if (source != "rank_response" && !HasRankKeys(candidate)) return null;
        var constructedClass = ReadClass(candidate, "constructedClass");
        var limitedClass = ReadClass(candidate, "limitedClass");
        if (constructedClass.Length == 0 && limitedClass.Length == 0) return null;
        return new Snapshot(constructedClass, ReadLevel(candidate, "constructedLevel"),
            limitedClass, ReadLevel(candidate, "limitedLevel"), "Arena rank response");
    }

    private static bool HasRankKeys(JsonElement candidate) =>
        candidate.TryGetProperty("constructedLevel", out _) &&
        candidate.TryGetProperty("limitedLevel", out _);

    private static string ReadClass(JsonElement source, string key)
    {
        if (!source.TryGetProperty(key, out var v) || v.ValueKind != JsonValueKind.String)
            return "";
        string text = (v.GetString() ?? "").Trim();
        if (text.Length is < 1 or > 32) return "";
        return text.All(ch => char.IsLetter(ch) || ch == ' ') ? text : "";
    }
    private static int ReadLevel(JsonElement source, string key)
    {
        if (!source.TryGetProperty(key, out var v) || v.ValueKind != JsonValueKind.Number ||
            !v.TryGetInt32(out int n) || n is < 0 or > 10) return 0;
        return n;
    }
}
