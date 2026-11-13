using System.Text.Json;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// Pure, independently testable trust boundary for Scryfall's symbology
/// mapping. Oracle text is NEVER used as a URL or a source of SVG markup.
/// </summary>
public static class NativeScryfallSymbology
{
    public static bool IsTrustedSvgUri(string? raw)
    {
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri)) return false;
        return uri.Scheme == Uri.UriSchemeHttps &&
            uri.Host.Equals("svgs.scryfall.io", StringComparison.OrdinalIgnoreCase) &&
            uri.AbsolutePath.StartsWith("/card-symbols/", StringComparison.Ordinal) &&
            uri.AbsolutePath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) &&
            uri.Query.Length == 0 && uri.Fragment.Length == 0 &&
            uri.UserInfo.Length == 0 && uri.Port == 443;
    }

    public static IReadOnlyDictionary<string, string> ReadUris(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!doc.RootElement.TryGetProperty("data", out var data) ||
            data.ValueKind != JsonValueKind.Array)
            return result;
        foreach (var item in data.EnumerateArray())
        {
            if (!item.TryGetProperty("symbol", out var s) ||
                !item.TryGetProperty("svg_uri", out var u) ||
                s.ValueKind != JsonValueKind.String ||
                u.ValueKind != JsonValueKind.String) continue;
            string token = s.GetString() ?? "";
            string svg = u.GetString() ?? "";
            if (token.Length is < 3 or > 28 ||
                !token.StartsWith("{", StringComparison.Ordinal) ||
                !token.EndsWith("}", StringComparison.Ordinal) ||
                !IsTrustedSvgUri(svg)) continue;
            result[token] = svg;
        }
        return result;
    }
}
