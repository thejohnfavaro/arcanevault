using System.Text.RegularExpressions;

namespace ArcaneVault.Next.EDHLab;

/// <summary>
/// Approximates colored mana-symbol demand from cached Scryfall mana_cost
/// strings. This is not a casting simulator; generic/colorless/X are omitted,
/// two-color hybrid symbols split equally, and phyrexian/2-color choice
/// costs are discounted because they may be paid without that color.
/// </summary>
public static class EdhLabColoredPips
{
    private const string Colors = "WUBRG";
    private static readonly Regex Symbols = new(
        @"\{([^{}]{1,15})\}", RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(50));

    public static double[] Count(string? manaCost)
    {
        var result = new double[5];
        if (string.IsNullOrWhiteSpace(manaCost) || manaCost.Length > 512)
            return result;
        // Scryfall split cards may provide "cost // alternative cost".
        string firstCost = manaCost.Split(" // ", 2, StringSplitOptions.None)[0];
        foreach (Match match in Symbols.Matches(firstCost))
        {
            string symbol = match.Groups[1].Value.ToUpperInvariant();
            var parts = symbol.Split('/');
            var indices = parts.Where(x => x.Length == 1 &&
                    Colors.Contains(x[0]))
                .Select(x => Colors.IndexOf(x[0])).Distinct().ToArray();
            if (indices.Length == 0) continue;

            // "{W/U}" can be paid with either; "{2/W}" or "{W/P}"
            // can be paid without W. Plain "{W}" is a full commitment.
            bool optional = parts.Length > 1;
            double mass = optional && indices.Length == 1 ? .5 : 1.0;
            foreach (int index in indices)
                result[index] += mass / indices.Length;
        }
        return result;
    }
}
