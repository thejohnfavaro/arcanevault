using ArcaneVault.Next.Collection;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// A presentation-only list of saved deck card names. It never infers
/// physical printings, Arena card styles, or inventory ownership. Commander
/// metadata is distinguished from mainboard quantities rather than silently
/// double-counted as another copy.
/// </summary>
public static class NativeDeckPreviewOutline
{
    public sealed record Line(string Name, int Quantity, bool IsCommander)
    {
        public string QuantityLabel => IsCommander
            ? (Quantity > 0 ? $"CMD · {Quantity}×" : "CMD")
            : $"{Quantity}×";
    }

    public static IReadOnlyList<Line> Build(NativeDeckEditing.DeckRow selected)
    {
        ArgumentNullException.ThrowIfNull(selected);
        var mainboard = NativeDeckEditing.ParseMainboard(selected.MainboardText);
        string commander = selected.Commander.Trim();
        var results = new List<Line>(mainboard.Count + 1);
        if (commander.Length > 0 &&
            !mainboard.Any(c => c.Name.Equals(commander, StringComparison.OrdinalIgnoreCase)))
            results.Add(new Line(commander, 0, true));
        results.AddRange(mainboard.Select(card =>
            new Line(card.Name, card.Quantity,
                commander.Length > 0 &&
                card.Name.Equals(commander, StringComparison.OrdinalIgnoreCase))));
        return results.AsReadOnly();
    }
}
