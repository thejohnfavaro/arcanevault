namespace ArcaneVault.Next.Desktop;

/// <summary>
/// UI-agnostic exact symbol semantics shared by official WPF mana controls
/// and the headless native regression runner. Never flatten generic mana
/// into repeated colored pips or infer costs from color identity.
/// </summary>
public static class NativeManaSequences
{
    public enum Mode { Identity, Cost }
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
