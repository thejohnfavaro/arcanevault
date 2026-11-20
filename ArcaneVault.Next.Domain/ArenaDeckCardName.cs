using System.Text.RegularExpressions;

namespace ArcaneVault.Next.Domain;

/// <summary>
/// Read-only identity normalization for legacy MTG Arena decklist annotations.
/// Exact saved text remains authoritative for editing and printing choices.
/// </summary>
public static class ArenaDeckCardName
{
    private static readonly Regex Printing = new(
        @"^(?<name>.+?)\s+\([a-zA-Z0-9]{2,8}\)\s+[0-9]{1,6}[a-zA-Z]?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    public static string Identity(string? savedName)
    {
        var name = (savedName ?? "").Trim();
        if (name.Length == 0 || name.Length > 255) return name;
        var match = Printing.Match(name);
        return match.Success ? match.Groups["name"].Value.Trim() : name;
    }
}
