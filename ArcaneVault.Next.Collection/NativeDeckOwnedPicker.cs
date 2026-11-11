using ArcaneVault.Next.Domain;

namespace ArcaneVault.Next.Collection;

/// <summary>
/// A read-only picker over explicitly recorded physical or Arena ownership.
/// Never merges inventories or changes owned counts when constructing a deck.
/// </summary>
public static class NativeDeckOwnedPicker
{
    public enum Pool { Physical, Arena }
    public sealed record Card(string Name, long Owned);

    public static IReadOnlyList<Card> Search(NativeBackupView ownership,
        Pool pool, string? query, int limit = 200)
    {
        ArgumentNullException.ThrowIfNull(ownership);
        if (limit is < 1 or > 500)
            throw new ArgumentOutOfRangeException(nameof(limit));
        string filter = (query ?? "").Trim();
        if (filter.Length > 255)
            throw new ArgumentException("Search text is too long.", nameof(query));
        var counts = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        if (pool == Pool.Physical)
        {
            foreach (var card in ownership.PhysicalCards)
                if (card.Quantity > 0)
                    counts[card.Name] = checked(counts.GetValueOrDefault(card.Name) + card.Quantity);
        }
        else
        {
            foreach (var card in ownership.ArenaCards)
                if (card.Quantity > 0)
                    counts[card.Name] = checked(counts.GetValueOrDefault(card.Name) + card.Quantity);
        }
        return counts.Where(x => x.Value > 0 &&
                x.Key.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .Select(x => new Card(x.Key, x.Value))
            .ToArray();
    }

    /// <summary>
    /// Add a single selected owned card without discarding or rewriting other
    /// entries. Invalid draft text is rejected instead of silently repaired.
    /// </summary>
    public static string AddOne(string mainboard, string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 255 ||
            name.Any(char.IsControl))
            throw new ArgumentException("Invalid selected card name.", nameof(name));
        var entries = NativeDeckEditing.ParseMainboard(mainboard);
        var current = entries.FirstOrDefault(e =>
            e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (current is not null && current.Quantity >= 250)
            throw new InvalidDataException("This card is already at the editor's per-card limit.");
        if (entries.Sum(e => e.Quantity) >= 1_000)
            throw new InvalidDataException("The deck editor supports at most 1,000 copies.");
        if (current is null && entries.Count >= 500)
            throw new InvalidDataException("The deck editor supports at most 500 distinct cards.");
        var lines = entries.Select(e =>
            (e.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
                ? e.Quantity + 1 : e.Quantity) + " " + e.Name).ToList();
        if (current is null) lines.Add("1 " + name.Trim());
        return string.Join("\n", lines);
    }
}
