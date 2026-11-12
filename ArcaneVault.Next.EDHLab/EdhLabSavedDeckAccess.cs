using ArcaneVault.Next.Domain;

namespace ArcaneVault.Next.EDHLab;

/// <summary>
/// Explicit saved-deck opt-in to EDH Lab. A saved deck's format label is not
/// evidence that its leader is actually eligible; verify against the current
/// local catalog before entering the specialized editor.
/// </summary>
public static class EdhLabSavedDeckAccess
{
    public static EdhLabFormat? FormatFor(string? savedFormat)
    {
        if (string.Equals(savedFormat?.Trim(), "Commander",
                StringComparison.OrdinalIgnoreCase))
            return EdhLabFormat.PaperCommander;
        if (string.Equals(savedFormat?.Trim(), "Arena Brawl",
                StringComparison.OrdinalIgnoreCase))
            return EdhLabFormat.ArenaBrawl;
        return null;
    }

    public static EdhLabCommanderCard? FindEligibleCommander(
        EdhLabCommanderIndex index, string? savedCommander, EdhLabFormat format)
    {
        ArgumentNullException.ThrowIfNull(index);
        string name = (savedCommander ?? "").Trim();
        if (name.Length is < 1 or > 255) return null;
        return index.Filter(new EdhLabQuery(Format: format, Search: name))
            .FirstOrDefault(card => string.Equals(card.Name, name,
                StringComparison.OrdinalIgnoreCase));
    }
}
