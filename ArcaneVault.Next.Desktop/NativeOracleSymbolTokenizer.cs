using System.Text.RegularExpressions;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// Lossless tokenization of authoritative Scryfall Oracle text. Tokens always
/// retain their literal braces for copy/export and offline fallbacks.
/// </summary>
public static partial class NativeOracleSymbolTokenizer
{
    public readonly record struct Segment(string Text, bool IsSymbol);

    [GeneratedRegex(@"\{[^{}\r\n]{1,24}\}", RegexOptions.CultureInvariant)]
    private static partial Regex ManaToken();

    public static IReadOnlyList<Segment> Split(string? oracle)
    {
        string source = oracle ?? "";
        var result = new List<Segment>();
        int cursor = 0;
        foreach (Match match in ManaToken().Matches(source))
        {
            if (match.Index > cursor)
                result.Add(new Segment(source[cursor..match.Index], false));
            // Scryfall's structured symbology endpoint decides whether a
            // token has an official glyph. Unknown braces remain intact.
            result.Add(new Segment(match.Value, true));
            cursor = match.Index + match.Length;
        }
        if (cursor < source.Length)
            result.Add(new Segment(source[cursor..], false));
        if (result.Count == 0)
            result.Add(new Segment(source, false));
        return result;
    }

    public static string CopyText(IEnumerable<Segment> segments) =>
        string.Concat(segments.Select(s => s.Text));
}
