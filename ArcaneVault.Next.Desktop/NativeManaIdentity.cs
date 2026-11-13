namespace ArcaneVault.Next.Desktop;

/// <summary>
/// Canonical, WPF-independent color-identity presentation model. This is
/// for readable mana symbols only; rules legality and collection validation
/// continue to use authoritative Scryfall color identity metadata.
/// </summary>
public static class NativeManaIdentity
{
    public sealed record Color(string Symbol, string Name);
    private const string Order = "WUBRG";

    public static IReadOnlyList<Color> ParseColorIdentity(string? raw)
    {
        string value = (raw ?? "").Trim().ToUpperInvariant();
        if (value is "" or "C" or "COLORLESS" or "[]")
            return new[] { new Color("C", "Colorless") };

        // Support WUBRG, W,U and Scryfall-style JSON arrays. Do not infer
        // colors from arbitrary prose or invalid card metadata.
        if (!value.All(ch => Order.Contains(ch) || char.IsWhiteSpace(ch) ||
                ch is ',' or '[' or ']' or '"' or '\'' or '/'))
            return Array.Empty<Color>();
        string compact = new(value.Where(Order.Contains).ToArray());
        if (compact.Length == 0) return Array.Empty<Color>();
        return Order.Where(compact.Contains)
            .Select(ch => new Color(ch.ToString(), NameFor(ch.ToString())))
            .ToArray();
    }

    public static string NameFor(string symbol) => symbol switch
    {
        "W" => "White", "U" => "Blue", "B" => "Black",
        "R" => "Red", "G" => "Green", "C" => "Colorless",
        _ => "Unknown"
    };
}
