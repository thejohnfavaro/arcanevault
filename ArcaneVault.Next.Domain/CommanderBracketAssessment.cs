using System.Collections.Immutable;

namespace ArcaneVault.Next.Domain;

/// <summary>
/// Read-only Commander bracket evidence. Does not modify a deck or claim that
/// a card-count calculation can determine a complete social/competitive intent.
/// The GC roster is versioned separately from the Commander legality database.
/// </summary>
public static class CommanderBracketAssessment
{
    public const string RulesVersion = "Commander Brackets beta · 2026-02-09";
    public const string SourceUrl = "https://magic.wizards.com/en/formats/commander";

    // The February 2026 list: 53 names. Always compare canonical card identity,
    // not set/collector details; front/back face spellings normalize identically.
    private static readonly ImmutableHashSet<string> GameChangers =
        (""" 
        Drannith Magistrate
        Enlightened Tutor
        Farewell
        Humility
        Serra's Sanctum
        Smothering Tithe
        Teferi's Protection
        Consecrated Sphinx
        Cyclonic Rift
        Fierce Guardianship
        Force of Will
        Gifts Ungiven
        Intuition
        Mystical Tutor
        Narset, Parter of Veils
        Rhystic Study
        Thassa's Oracle
        Ad Nauseam
        Bolas's Citadel
        Braids, Cabal Minion
        Demonic Tutor
        Imperial Seal
        Necropotence
        Opposition Agent
        Orcish Bowmasters
        Tergrid, God of Fright
        Vampiric Tutor
        Gamble
        Jeska's Will
        Underworld Breach
        Biorhythm
        Crop Rotation
        Gaea's Cradle
        Natural Order
        Seedborn Muse
        Survival of the Fittest
        Worldly Tutor
        Aura Shards
        Coalition Victory
        Grand Arbiter Augustin IV
        Notion Thief
        Ancient Tomb
        Chrome Mox
        Field of the Dead
        Glacial Chasm
        Grim Monolith
        Lion's Eye Diamond
        Mana Vault
        Mishra's Workshop
        Mox Diamond
        Panoptic Mirror
        The One Ring
        The Tabernacle at Pendrell Vale
        """).Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
          .Select(Canonical).ToImmutableHashSet(StringComparer.OrdinalIgnoreCase);

    public sealed record Result(int MinimumBracket, int GameChangerCount,
        IReadOnlyList<string> MatchedCards, string Interpretation,
        IReadOnlyList<string> Unknowns, string RulesRevision,
        bool IsFinalClassification = false);

    public static int RosterCount => GameChangers.Count;

    public static string Canonical(string? raw)
    {
        string name = ArenaDeckCardName.Identity(raw ?? "").Trim();
        // For MDFCs, match the complete front face if a catalog name contains
        // both faces; for printed flip/DFC cards, the front is the identity.
        int split = name.IndexOf(" // ", StringComparison.Ordinal);
        if (split >= 0) name = name[..split].Trim();
        return name.Replace('’', '\'').Replace('‘', '\'')
            .Normalize(System.Text.NormalizationForm.FormKC);
    }

    public static Result Evaluate(IEnumerable<string> cardNames,
        string? commander = null, bool knownMassLandDenial = false,
        bool knownEarlyTwoCardWin = false)
    {
        ArgumentNullException.ThrowIfNull(cardNames);
        var names = cardNames.Append(commander ?? "")
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(Canonical)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var matched = names.Where(GameChangers.Contains)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray();
        int minimum = matched.Length > 3 || knownMassLandDenial || knownEarlyTwoCardWin ? 4
            : matched.Length > 0 ? 3 : 1;
        var unknowns = new[]
        {
            "Deck-building intent (Exhibition versus Core)",
            "Typical turn of first credible win and interaction resilience",
            "Intentional combos, repeated extra turns, and resource denial",
            "Optimized versus competitive metagame intent"
        };
        var meaning = minimum switch
        {
            4 => "At least Bracket 4 by known restrictive signals. Bracket 5 requires competitive-intent assessment.",
            3 => "At least Bracket 3 due to Game Changers; power and play patterns may warrant Bracket 4 or 5.",
            _ => "No Game Changer floor detected. Could be any bracket depending on play patterns and intent."
        };
        return new Result(minimum, matched.Length, matched, meaning,
            unknowns, RulesVersion);
    }
}
