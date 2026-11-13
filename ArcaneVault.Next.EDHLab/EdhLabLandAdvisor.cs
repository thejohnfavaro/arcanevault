using ArcaneVault.Next.Domain;
using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace ArcaneVault.Next.EDHLab;

/// <summary>
/// Read-only candidate lands from the already format/commander-identity
/// verified EDH catalog. "Produced" means detected mana access in Oracle
/// text or basic land types—not the card's color identity. Fetchlands are
/// specifically NOT treated as direct colored-mana producers.
/// </summary>
public static class EdhLabLandAdvisor
{
    private const string ColorOrder = "WUBRG";
    private static readonly string[] BasicTypes =
        ["Plains", "Island", "Swamp", "Mountain", "Forest"];

    public enum Availability { OwnedOrProxy, RealOwned, Proxy, All }

    private sealed record BaseLand(EdhLabDraftCatalog.Card Card,
        string ProductionSymbols, string AccessNote, bool FetchesLand,
        bool AnyColor, bool Conditional, int CoveredCommanderColors,
        string Tempo, bool Restricted);
    private sealed class CachedLandSet
    {
        public ConcurrentDictionary<int, IReadOnlyList<BaseLand>> ByColors { get; } = new();
    }
    // Weak catalog key: recompute Oracle production only after a different
    // verified local catalog snapshot or commander identity; cheap search,
    // filter, sorting and draft edits never reparse all land Oracle texts.
    private static readonly ConditionalWeakTable<
        IReadOnlyList<EdhLabDraftCatalog.Card>, CachedLandSet> Parsed = new();

    public sealed record Land(
        string Name, string Type, string Oracle,
        long RealOwned, long ProxyCopies, int InDeckCopies,
        string ProductionSymbols, string AccessNote,
        bool FetchesLand, bool AnyColor, bool Conditional,
        int CoveredCommanderColors, bool IsBasic, int CopyLimit,
        string Tempo = "Unknown entry", bool Restricted = false,
        int GapPriority = 0)
    {
        public bool CanQuickAdd => InDeckCopies == 0 && CopyLimit > 0;
        public string AvailabilityLabel =>
            $"{RealOwned:N0} real owned" +
            (ProxyCopies > 0 ? $" · {ProxyCopies:N0} Proxy" : "") +
            (InDeckCopies > 0 ? $" · In deck ×{InDeckCopies}" : " · Not in deck");
        public string Explanation => AccessNote + " · " + Tempo +
            (Restricted ? " · Restricted mana spending." : "") +
            (Conditional ? " · Read printed conditions before adding." : "") +
            (FetchesLand ? " · Color production depends on fetched targets." : "") +
            " · Land legality/printing is a separate check.";
    }

    public sealed record Result(IReadOnlyList<Land> Rows,
        int Matching, int TotalEligibleLands, bool Truncated)
    {
        public string Summary => Rows.Count == 0
            ? $"No matching lands from {TotalEligibleLands:N0} locally eligible names. " +
              "Try All eligible or Show basics; no deck or ownership changes."
            : $"{Rows.Count:N0} shown of {Matching:N0} matching lands " +
              $"({TotalEligibleLands:N0} eligible names)" +
              (Truncated ? " · first results only" : "") +
              " · single-click to preview, double-click to add once.";
    }

    private static int BitsForText(string text)
    {
        int mask = 0;
        for (int i = 0; i < ColorOrder.Length; i++)
            if (text.Contains("{" + ColorOrder[i] + "}",
                    StringComparison.OrdinalIgnoreCase))
                mask |= 1 << i;
        return mask;
    }

    private static bool IsLand(string type) => Regex.IsMatch(type,
        @"(?:^|[\s—])Land(?:$|[\s—])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(50));

    // Conservative hints, not a substitute for full Oracle legality or
    // activated-ability resolution. In particular, an arbitrary mana symbol
    // in a cost or reminder text is never counted as production.
    private static (string Symbols, string Note, bool Fetch, bool Any, bool Conditional,
        int Colors, string Tempo, bool Restricted) Production(
        EdhLabDraftCatalog.Card card, int allowed)
    {
        int mask = 0;
        bool colorless = false;
        string type = card.Type ?? "";
        string oracle = card.Oracle ?? "";
        int dash = type.IndexOf('—');
        if (dash >= 0)
        {
            string subtypes = type[(dash + 1)..];
            for (int i = 0; i < BasicTypes.Length; i++)
                if (Regex.IsMatch(subtypes, @"\b" + BasicTypes[i] + @"\b",
                        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                        TimeSpan.FromMilliseconds(50)))
                    mask |= 1 << i;
        }

        bool any = false;
        bool conditional = false;
        bool restricted = false;
        foreach (Match m in Regex.Matches(oracle,
                     @"\bAdd\b(?<clause>[^.\r\n]{0,220})",
                     RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                     TimeSpan.FromMilliseconds(50)))
        {
            // An opponent's instruction such as "Whenever a player adds
            // {U}, ..." is not this land producing mana. Require an actual
            // 'Add' instruction, followed by a mana token or a mana phrase.
            string clause = m.Groups["clause"].Value;
            bool anyPhrase = Regex.IsMatch(clause,
                @"\b(?:one|two|three|X|a) mana\b[^.\r\n]{0,100}\b(?:any color|any type)\b|\bany (?:one )?color\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(50));
            bool symbols = Regex.IsMatch(clause, @"\{[WUBRGC]\}",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(50));
            if (!anyPhrase && !symbols) continue;
            if (anyPhrase)
            {
                mask |= allowed;
                any = true;
            }
            mask |= BitsForText(clause);
            colorless |= clause.Contains("{C}", StringComparison.OrdinalIgnoreCase);
            if (clause.Contains("only", StringComparison.OrdinalIgnoreCase) ||
                clause.Contains("chosen", StringComparison.OrdinalIgnoreCase) ||
                clause.Contains("among", StringComparison.OrdinalIgnoreCase) ||
                clause.Contains("could produce", StringComparison.OrdinalIgnoreCase) ||
                clause.Contains("for each", StringComparison.OrdinalIgnoreCase))
                conditional = true;
        }

        bool fetch = Regex.IsMatch(oracle,
            @"search your library for[^.\r\n]{0,180}\b(?:land|plains|island|swamp|mountain|forest)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(50));
        bool tapped = Regex.IsMatch(oracle,
            @"\b(?:enters (?:the battlefield )?tapped|enters tapped)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(50));
        bool exceptUntapped = tapped && Regex.IsMatch(oracle,
            @"\bunless\b|\bif\b[^.\r\n]{0,100}\buntapped\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(50));
        restricted = oracle.Contains("Spend this mana only", StringComparison.OrdinalIgnoreCase) ||
            oracle.Contains("Spend this mana to", StringComparison.OrdinalIgnoreCase) ||
            oracle.Contains("only to cast", StringComparison.OrdinalIgnoreCase) ||
            oracle.Contains("Activate only", StringComparison.OrdinalIgnoreCase);
        conditional |= fetch || restricted || exceptUntapped ||
            oracle.Contains("if you control", StringComparison.OrdinalIgnoreCase) ||
            oracle.Contains("as long as", StringComparison.OrdinalIgnoreCase) ||
            oracle.Contains("any type that", StringComparison.OrdinalIgnoreCase) ||
            oracle.Contains("chosen color", StringComparison.OrdinalIgnoreCase) ||
            oracle.Contains("could produce", StringComparison.OrdinalIgnoreCase) ||
            oracle.Contains("only if", StringComparison.OrdinalIgnoreCase);
        string tempo = tapped ? (exceptUntapped ? "May enter tapped" : "Enters tapped") :
            oracle.Contains("enters the battlefield", StringComparison.OrdinalIgnoreCase) &&
                oracle.Contains("if", StringComparison.OrdinalIgnoreCase)
                ? "Check entry conditions" : "No tapped-entry clause found";
        mask &= allowed;
        string symbolsOut = string.Concat(Enumerable.Range(0, ColorOrder.Length)
            .Where(i => (mask & (1 << i)) != 0)
            .Select(i => ColorOrder[i]));
        if (colorless) symbolsOut += "C";
        int colors = BitOperations.PopCount((uint)mask);

        string note = any
            ? "Can add any commander color (read its conditions)"
            : colors > 1
                ? "Can produce multiple commander colors"
                : colors == 1
                    ? "Produces one commander color"
                    : colorless
                        ? "Colorless mana access"
                        : fetch ? "Fetchland: target determines mana access"
                        : oracle.Contains("could produce", StringComparison.OrdinalIgnoreCase)
                            ? "Conditional: depends on other lands"
                            : "Utility/conditional mana; no guaranteed color inferred";
        return (symbolsOut, note, fetch, any, conditional, colors, tempo, restricted);
    }

    /// <summary>
    /// Candidates must come from EdhLabDraftCatalog.AllEligibleCards: caller
    /// already constrains commander identity, format and Arena availability.
    /// Proxies are paper-only and remain distinct from real owned copies.
    /// </summary>
    public static Result Recommend(
        IReadOnlyList<EdhLabDraftCatalog.Card> eligibleCards,
        string commanderColors,
        IReadOnlyDictionary<string, int>? inDeck = null,
        IReadOnlyDictionary<string, long>? paperProxies = null,
        Availability filter = Availability.OwnedOrProxy,
        bool includeBasics = false, string? search = null, int limit = 100)
    {
        ArgumentNullException.ThrowIfNull(eligibleCards);
        if (!Enum.IsDefined(filter)) throw new ArgumentOutOfRangeException(nameof(filter));
        if (limit is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(limit));
        search = (search ?? "").Trim();
        if (search.Length > 255) throw new ArgumentException(
            "Land search is too long.", nameof(search));
        int allowed = EdhLabCommanderRules.ColorBits(commanderColors);

        var cache = Parsed.GetValue(eligibleCards, _ => new CachedLandSet());
        var landBases = cache.ByColors.GetOrAdd(allowed, identity =>
            eligibleCards.Where(card => IsLand(card.Type))
                .Select(card =>
                {
                    var access = Production(card, identity);
                    return new BaseLand(card, access.Symbols, access.Note,
                        access.Fetch, access.Any, access.Conditional,
                        access.Colors, access.Tempo, access.Restricted);
                }).ToArray());
        var lands = landBases.Select(item =>
        {
            var card = item.Card;
            long proxy = paperProxies?.GetValueOrDefault(card.Name) ?? 0;
            int drafted = inDeck?.GetValueOrDefault(card.Name) ?? 0;
            if (proxy < 0 || drafted < 0)
                throw new InvalidDataException("Invalid land availability totals.");
            return new Land(card.Name, card.Type, card.Oracle,
                card.RecordedCopies, proxy, drafted,
                item.ProductionSymbols, item.AccessNote, item.FetchesLand,
                item.AnyColor, item.Conditional, item.CoveredCommanderColors,
                card.IsBasicLand, card.SuggestedCopyLimit,
                item.Tempo, item.Restricted);
        }).ToArray();

        // Evaluate which colors the current mainboard is actually short on.
        // Only direct, non-restricted colored production contributes; fetch
        // targets and conditional any-color cards are deliberately discounted.
        int[] coverage = new int[ColorOrder.Length];
        foreach (var item in lands)
        {
            if (item.InDeckCopies <= 0 || item.FetchesLand || item.Restricted)
                continue;
            int weight = Math.Min(item.InDeckCopies, 60);
            if (item.Conditional || item.Tempo == "Enters tapped")
                weight = Math.Max(1, weight / 2);
            for (int i = 0; i < ColorOrder.Length; i++)
                if (item.ProductionSymbols.Contains(ColorOrder[i]))
                    coverage[i] = checked(coverage[i] + weight);
        }
        var usedColors = Enumerable.Range(0, ColorOrder.Length)
            .Where(i => (allowed & (1 << i)) != 0).ToArray();
        int minimum = usedColors.Length == 0 ? 0 :
            usedColors.Min(i => coverage[i]);
        int priority(Land item)
        {
            int gain = 0;
            foreach (int i in usedColors)
                if (item.ProductionSymbols.Contains(ColorOrder[i]))
                    gain += 4 + Math.Min(12, Math.Max(0, 12 - coverage[i])) * 2 +
                        (coverage[i] == minimum ? 8 : 0);
            if (item.AnyColor) gain += 7;
            if (item.FetchesLand && item.ProductionSymbols.Length == 0) gain += 2;
            if (item.Tempo == "Enters tapped") gain -= 20;
            else if (item.Tempo == "May enter tapped") gain -= 8;
            if (item.Conditional) gain -= 8;
            if (item.Restricted) gain -= 18;
            if (item.InDeckCopies > 0) gain -= 30;
            return gain;
        }
        lands = lands.Select(x => x with { GapPriority = priority(x) }).ToArray();

        bool MatchesFilter(Land x) => filter switch
        {
            Availability.RealOwned => x.RealOwned > 0,
            Availability.Proxy => x.ProxyCopies > 0,
            Availability.OwnedOrProxy => x.RealOwned > 0 || x.ProxyCopies > 0,
            _ => true
        };
        var matches = lands
            .Where(x => (includeBasics || !x.IsBasic) &&
                MatchesFilter(x) &&
                (search.Length == 0 ||
                 x.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                 x.Type.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                 x.AccessNote.Contains(search, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(x => x.InDeckCopies > 0)
            .ThenByDescending(x => x.RealOwned > 0)
            .ThenByDescending(x => x.ProxyCopies > 0)
            .ThenByDescending(x => x.GapPriority)
            .ThenByDescending(x => x.CoveredCommanderColors)
            .ThenByDescending(x => x.AnyColor)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new Result(matches.Take(limit).ToArray(),
            matches.Length, lands.Length, matches.Length > limit);
    }
}
