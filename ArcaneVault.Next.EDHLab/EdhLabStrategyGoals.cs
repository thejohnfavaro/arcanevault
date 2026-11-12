namespace ArcaneVault.Next.EDHLab;

/// <summary>
/// Transient, user-selected direction for advisory EDH Lab rankings.
/// Changing focus never modifies a deck, collection, or saved preference.
/// The baseline is intentionally compatible with earlier balanced advice.
/// </summary>
public enum EdhLabStrategyFocus
{
    Balanced,
    FastStart,
    CardAdvantage,
    Interaction,
    CommanderSynergy
}

public static class EdhLabStrategyGoals
{
    public static string Label(EdhLabStrategyFocus focus) => focus switch
    {
        EdhLabStrategyFocus.Balanced => "Balanced",
        EdhLabStrategyFocus.FastStart => "Faster starts",
        EdhLabStrategyFocus.CardAdvantage => "Card advantage",
        EdhLabStrategyFocus.Interaction => "Interaction & protection",
        EdhLabStrategyFocus.CommanderSynergy => "Commander synergy",
        _ => throw new ArgumentOutOfRangeException(nameof(focus))
    };

    public static string Description(EdhLabStrategyFocus focus) => focus switch
    {
        EdhLabStrategyFocus.Balanced =>
            "Balance curve, scarce roles, and recognized commander mechanics.",
        EdhLabStrategyFocus.FastStart =>
            "Prefer cheap setup and ramp; preserve key payoffs rather than forcing low mana value.",
        EdhLabStrategyFocus.CardAdvantage =>
            "Prefer affordable draw and tutor effects that keep resources flowing.",
        EdhLabStrategyFocus.Interaction =>
            "Prefer efficient removal and protection while retaining critical utility roles.",
        EdhLabStrategyFocus.CommanderSynergy =>
            "Prefer cards matching recognized commander-text mechanics; preserve existing role coverage.",
        _ => throw new ArgumentOutOfRangeException(nameof(focus))
    };

    /// <summary>
    /// Deterministic advisory weighting over already eligible candidates.
    /// The classification is from local card text, not a power-level claim.
    /// </summary>
    public static int Bias(EdhLabStrategyFocus focus,
        IReadOnlySet<string> roles, int synergyScore, double manaValue)
    {
        ArgumentNullException.ThrowIfNull(roles);
        if (!double.IsFinite(manaValue) || manaValue < 0)
            return 0;
        bool Has(string role) => roles.Contains(role);
        return focus switch
        {
            EdhLabStrategyFocus.Balanced => 0,
            EdhLabStrategyFocus.FastStart =>
                (Has("Ramp") ? 32 : 0) + (manaValue <= 1 ? 18 : 0) +
                (Has("Removal") ? 5 : 0),
            EdhLabStrategyFocus.CardAdvantage =>
                (Has("Card Draw") ? 46 : 0) + (Has("Tutor") ? 31 : 0) +
                (Has("Ramp") ? 6 : 0),
            EdhLabStrategyFocus.Interaction =>
                (Has("Removal") ? 46 : 0) + (Has("Protection") ? 34 : 0) +
                (Has("Board Wipe") ? 15 : 0),
            EdhLabStrategyFocus.CommanderSynergy =>
                Math.Clamp(synergyScore, 0, 20) * 13,
            _ => throw new ArgumentOutOfRangeException(nameof(focus))
        };
    }
}
