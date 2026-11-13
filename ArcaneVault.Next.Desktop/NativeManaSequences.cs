namespace ArcaneVault.Next.Desktop;

/// <summary>
/// UI-agnostic exact symbol semantics shared by official WPF mana controls
/// and the headless native regression runner. Never flatten generic mana
/// into repeated colored pips or infer costs from color identity.
/// </summary>
public static class NativeManaSequences
{
    public enum Mode { Identity, Cost, Production }
    public sealed record Piece(string Text, bool IsSymbol);

    public static IReadOnlyList<Piece> Pieces(string? value, Mode mode)
    {
        if (mode == Mode.Identity)
        {
            var colors = NativeManaIdentity.ParseColorIdentity(value);
            if (colors.Count == 0)
                return new[] { new Piece("Unknown identity", false) };
            return colors.Select(x => new Piece(x.Symbol, true)).ToArray();
        }
        if (mode == Mode.Production)
        {
            // Production may contain {C} alongside colored symbols; unlike
            // color identity, an empty string means unclassified, NOT C.
            string raw = (value ?? "").Trim().ToUpperInvariant();
            if (raw.Length == 0) return Array.Empty<Piece>();
            const string sources = "WUBRGC";
            if (!raw.All(sources.Contains))
                return new[] { new Piece("Mana access unverified", false) };
            return sources.Where(raw.Contains)
                .Select(ch => new Piece(ch.ToString(), true)).ToArray();
        }
        if (mode != Mode.Cost)
            throw new ArgumentOutOfRangeException(nameof(mode));
        if (string.IsNullOrWhiteSpace(value))
            return Array.Empty<Piece>();
        return NativeOracleSymbolTokenizer.Split(value)
            .Select(segment => new Piece(segment.IsSymbol
                ? segment.Text[1..^1] : segment.Text, segment.IsSymbol))
            .ToArray();
    }
}
