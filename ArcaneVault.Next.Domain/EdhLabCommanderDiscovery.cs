using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace ArcaneVault.Next.Domain;

public enum EdhLabFormat { PaperCommander, ArenaBrawl }
public enum EdhLabColorMatch { Exact, ContainsAll, ContainsAny }

/// <summary>
/// A catalog card identity; not a specific printing or ownership claim.
/// The existing catalog does not preserve power/toughness; specialized
/// Vehicles/Spacecraft must remain excluded until that data is supplied.
/// </summary>
public sealed record EdhLabCommanderCard(
    string Name,
    string TypeLine,
    string OracleText,
    string ColorIdentity,
    bool AvailableOnArena,
    IReadOnlyDictionary<string, string> Legalities,
    string ImageUrl,
    bool HasVerifiedPowerToughness = false);

public sealed record EdhLabQuery(
    EdhLabFormat Format = EdhLabFormat.PaperCommander,
    string Colors = "",
    EdhLabColorMatch Match = EdhLabColorMatch.Exact,
    string Search = "",
    int Page = 0,
    int PageSize = 50);

public sealed record EdhLabPage(
    int Total,
    int Page,
    int PageSize,
    IReadOnlyList<EdhLabCommanderCard> Cards)
{
    public int TotalPages => (Total + PageSize - 1) / PageSize;
}

public static class EdhLabCommanderRules
{
    private const string Mana = "WUBRG";
    private static readonly Regex LegendaryWord = new(@"\bLegendary\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex CreatureWord = new(@"\bCreature\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PlaneswalkerWord = new(@"\bPlaneswalker\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex VehicleWord = new(@"\bVehicle\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex SpacecraftWord = new(@"\bSpacecraft\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static int ColorBits(string? colors)
    {
        int bits = 0;
        foreach (var c in (colors ?? "").Trim().ToUpperInvariant())
        {
            if (char.IsWhiteSpace(c) || c == ',' || c == '/' || c == '-') continue;
            int index = Mana.IndexOf(c);
            if (index < 0) throw new ArgumentException("Invalid mana color identity.", nameof(colors));
            bits |= 1 << index;
        }
        return bits;
    }

    public static bool CanBeCommander(EdhLabCommanderCard card, EdhLabFormat format)
    {
        if (card is null || string.IsNullOrWhiteSpace(card.Name)) return false;
        var type = card.TypeLine ?? "";
        bool legendary = LegendaryWord.IsMatch(type);
        bool exception = (card.OracleText ?? "").Contains("can be your commander",
                StringComparison.OrdinalIgnoreCase) ||
            (card.OracleText ?? "").Contains("may be your commander",
                StringComparison.OrdinalIgnoreCase);
        bool standardCreature = legendary && CreatureWord.IsMatch(type);
        bool qualifiedArtifact = legendary && card.HasVerifiedPowerToughness &&
            (VehicleWord.IsMatch(type) || SpacecraftWord.IsMatch(type));
        bool paperEligible = exception || standardCreature || qualifiedArtifact;
        bool brawlEligible = paperEligible || (legendary && PlaneswalkerWord.IsMatch(type));

        if (format == EdhLabFormat.ArenaBrawl && !card.AvailableOnArena) return false;
        if (format == EdhLabFormat.PaperCommander && !paperEligible) return false;
        if (format == EdhLabFormat.ArenaBrawl && !brawlEligible) return false;

        // These legality values are sourced from the local catalog, not
        // guessed from creature type or Arena availability alone.
        var key = format == EdhLabFormat.PaperCommander ? "commander" : "brawl";
        if (card.Legalities.TryGetValue(key, out var state))
            return state.Equals("legal", StringComparison.OrdinalIgnoreCase);

        // Older Scryfall catalog versions used historicbrawl for the 100-card
        // Arena format. Never override an explicit newer 'brawl' restriction.
        return format == EdhLabFormat.ArenaBrawl &&
            card.Legalities.TryGetValue("historicbrawl", out state) &&
            state.Equals("legal", StringComparison.OrdinalIgnoreCase);
    }

    public static bool ColorMatches(string candidateColors, string filterColors, EdhLabColorMatch match)
    {
        if (string.IsNullOrWhiteSpace(filterColors)) return true;
        if (filterColors.Trim().Equals("C", StringComparison.OrdinalIgnoreCase))
            return ColorBits(candidateColors) == 0;
        if (filterColors.Contains('C', StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Colorless must be selected by itself.", nameof(filterColors));
        var mask = ColorBits(candidateColors);
        var selected = ColorBits(filterColors);
        return match switch
        {
            EdhLabColorMatch.Exact => mask == selected,
            EdhLabColorMatch.ContainsAll => (mask & selected) == selected,
            EdhLabColorMatch.ContainsAny => (mask & selected) != 0,
            _ => throw new ArgumentOutOfRangeException(nameof(match))
        };
    }

    public static bool Matches(EdhLabCommanderCard card, EdhLabQuery query)
    {
        if (!CanBeCommander(card, query.Format) ||
            !ColorMatches(card.ColorIdentity, query.Colors, query.Match))
            return false;
        var search = (query.Search ?? "").Trim();
        return search.Length == 0 ||
            card.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
            (card.OracleText ?? "").Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    public static string CanonicalColors(string colors)
    {
        var mask = ColorBits(colors);
        return string.Concat(Mana.Where((_, i) => (mask & (1 << i)) != 0));
    }
}

/// <summary>Immutable catalog snapshot of commander candidates, grouped by card name.</summary>
public sealed class EdhLabCommanderIndex
{
    private readonly IReadOnlyList<EdhLabCommanderCard> candidates;

    public EdhLabCommanderIndex(IEnumerable<EdhLabCommanderCard> cards)
    {
        ArgumentNullException.ThrowIfNull(cards);
        candidates = cards.Where(x => x is not null && !string.IsNullOrWhiteSpace(x.Name))
            .GroupBy(x => x.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public int LoadedCardIdentities => candidates.Count;

    public IReadOnlyList<EdhLabCommanderCard> Filter(EdhLabQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.PageSize is < 1 or > 200 || query.Page < 0)
            throw new ArgumentOutOfRangeException(nameof(query), "Invalid EDH Lab page bounds.");
        return candidates.Where(c => EdhLabCommanderRules.Matches(c, query)).ToArray();
    }

    public EdhLabPage Browse(EdhLabQuery query)
    {
        var filtered = Filter(query);
        var offset = (long)query.Page * query.PageSize;
        var page = offset >= filtered.Count
            ? Array.Empty<EdhLabCommanderCard>()
            : filtered.Skip((int)offset).Take(query.PageSize).ToArray();
        return new EdhLabPage(filtered.Count, query.Page, query.PageSize, page);
    }
}

/// <summary>Uniform random roll over only the current legal, filtered pool.</summary>
public sealed class EdhLabRandomPicker
{
    private readonly HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
    private string signature = "";

    public EdhLabCommanderCard? Roll(EdhLabCommanderIndex index, EdhLabQuery query,
        bool avoidRepeats = false, Func<int, int>? chooseIndex = null)
    {
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(query);
        var pool = index.Filter(query);
        var normalizedSignature = string.Join("|", query.Format, query.Match,
            query.Colors.Trim().ToUpperInvariant(), query.Search.Trim().ToUpperInvariant());
        if (!string.Equals(signature, normalizedSignature, StringComparison.Ordinal))
        {
            signature = normalizedSignature;
            seen.Clear();
        }
        if (pool.Count == 0) return null;
        var options = avoidRepeats ? pool.Where(c => !seen.Contains(c.Name)).ToArray() : pool.ToArray();
        if (options.Length == 0)
        {
            seen.Clear();
            options = pool.ToArray();
        }
        var indexValue = chooseIndex is null
            ? RandomNumberGenerator.GetInt32(options.Length) : chooseIndex(options.Length);
        if (indexValue < 0 || indexValue >= options.Length)
            throw new ArgumentOutOfRangeException(nameof(chooseIndex));
        var selected = options[indexValue];
        if (avoidRepeats) seen.Add(selected.Name);
        return selected;
    }

    public void Reset()
    {
        seen.Clear();
        signature = "";
    }
}
