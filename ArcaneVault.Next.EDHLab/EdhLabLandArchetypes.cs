using System.Text.RegularExpressions;

namespace ArcaneVault.Next.EDHLab;

/// <summary>
/// Conservative printed-text classifications for land discovery. Labels are
/// heuristics, never identity, printing ownership, or deck legality assertions.
/// </summary>
public static class EdhLabLandArchetypes
{
    public sealed record Classification(string Category, string EntryNote,
        string Detail, bool EntersTapped, bool MayEnterTapped);

    private static bool Has(string oracle, string phrase) =>
        oracle.Contains(phrase, StringComparison.OrdinalIgnoreCase);
    private static bool Rx(string oracle, string pattern) =>
        Regex.IsMatch(oracle, pattern,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(60));

    public static Classification Classify(string? cardName, string? typeLine,
        string? oracleText, bool fetchesLand = false)
    {
        string name = (cardName ?? "").Trim();
        string type = typeLine ?? "";
        string oracle = oracleText ?? "";
        bool tapped = Rx(oracle, @"\benters (?:the battlefield )?tapped\b");
        // Recent Scryfall Oracle uses "As [name] enters, you may pay 2 life.
        // If you don't, it enters tapped" for shock lands. Older text
        // commonly used "enters tapped unless you pay 2 life".
        bool shockPayment = Rx(oracle, @"\bunless you pay (?:\{2\}|2) life\b") ||
            (Rx(oracle, @"\bmay pay (?:\{2\}|2) life\b") &&
             Rx(oracle, @"\bif you (?:don't|do not)[^.\r\n]{0,100}\benters (?:the battlefield )?tapped\b"));
        bool conditionalEntry = tapped &&
            (shockPayment || Rx(oracle, @"\bunless\b|\bif\b[^.\r\n]{0,100}\buntapped\b"));
        string entry = !tapped ? "No tapped-entry clause found" :
            conditionalEntry ? "May enter tapped" : "Enters tapped";

        if (Rx(type, @"\bBasic\s+(?:Snow\s+)?Land\b") ||
            Rx(type, @"\bSnow\s+Land\b") && Rx(type, @"\bBasic\b"))
            return new("Basic land", entry,
                "Basic subtype grants its intrinsic mana ability", tapped, conditionalEntry);

        bool fetch = fetchesLand || Rx(oracle,
            @"search your library for[^.\r\n]{0,180}\b(?:land|plains|island|swamp|mountain|forest)\b");
        if (fetch && Rx(oracle, @"\bsacrifice\b") &&
            Rx(oracle, @"search your library for"))
            return new("Fetch land", entry,
                "Fetch targets determine the actual mana access", tapped, conditionalEntry);

        if (tapped && shockPayment &&
            Rx(type, @"\bLand\s*[—-]\s*[^\r\n]*(?:Plains|Island|Swamp|Mountain|Forest)\b"))
            return new("Shock land", "Pay 2 life or enter tapped",
                "Shock-style dual land with basic land types", true, true);

        if (tapped && Rx(oracle,
            @"\bunless you control (?:two|2) or fewer other lands\b"))
            return new("Fast land", "Untapped with two or fewer other lands",
                "Fast land: often tapped later in the game", true, true);

        if (tapped && Rx(oracle,
            @"\bunless you control (?:two|2) or more other lands\b"))
            return new("Slow land", "Untapped with two or more other lands",
                "Slow land: often tapped early in the game", true, true);

        if (tapped && Rx(oracle,
            @"\bunless you have (?:two|2) or more opponents\b"))
            return new("Bond land", "Untapped with two or more opponents",
                "Multiplayer-oriented entry condition", true, true);

        if (tapped && Rx(oracle,
            @"\bunless you control[^.\r\n]{0,140}\b(?:Plains|Island|Swamp|Mountain|Forest)\b"))
            return new("Check land", entry,
                "Requires control of specified basic-land types", true, true);

        if (tapped && Rx(oracle,
            @"\bunless you reveal[^.\r\n]{0,140}\b(?:card|hand)\b"))
            return new("Reveal land", entry,
                "May enter untapped if a card is revealed", true, true);

        if (Rx(oracle, @"\breturn a land you control to its owner's hand\b"))
            return new("Bounce land", entry,
                "Returns a controlled land to hand", tapped, conditionalEntry);

        // Pathway / modal face text is usually two separately indexed faces.
        // A delimiter in the name is not proof of an MDFC; require faces in
        // the card's type as well, and keep the label conditional.
        if (name.Contains(" // ", StringComparison.Ordinal) &&
            Rx(type, @"\bLand\b") && Has(oracle, "//"))
            return new("Modal land faces", entry,
                "Review each face for its distinct mana production", tapped, conditionalEntry);

        if (Rx(oracle, @"\bdeals 1 damage to you\b") &&
            Rx(oracle, @"\badd\s+\{[WUBRG]\}"))
            return new("Pain land", entry,
                "Colored mana can cost 1 life", tapped, conditionalEntry);

        if (Rx(oracle, @"\{1\}\s*,\s*\{T\}\s*:\s*Add\s+\{[WUBRG]\}"))
            return new("Filter land", entry,
                "Filters paid mana into colored mana", tapped, conditionalEntry);

        if (tapped && Rx(oracle, @"\bcycling\b") &&
            Regex.Matches(type, @"\b(?:Plains|Island|Swamp|Mountain|Forest)\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(60)).Count >= 3)
            return new("Triome-style land", entry,
                "Three basic land types and cycling", true, conditionalEntry);

        if (tapped && !conditionalEntry)
            return new("Tap land", "Enters tapped",
                "Unconditional tapped entry", true, false);
        if (tapped)
            return new("Conditional tap land", entry,
                "Review the exact condition for entering untapped", true, true);

        if (Rx(type, @"\bLand\s*[—-]\s*[^\r\n]*(?:Plains|Island|Swamp|Mountain|Forest)\b"))
            return new("Typed dual land", entry,
                "Basic land types grant intrinsic mana production", false, false);

        if (fetch) return new("Land tutor", entry,
            "Fetch/search access depends on targets and costs", tapped, conditionalEntry);

        if (Rx(oracle, @"\badd\s+one mana of any color\b"))
            return new("Rainbow land", entry,
                "Produces any color subject to printed restrictions", tapped, conditionalEntry);

        return new("Utility / other land", entry,
            "Review Oracle text for activation and entry restrictions",
            tapped, conditionalEntry);
    }
}
