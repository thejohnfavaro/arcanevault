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
    /// Exact owned-by-name count for previewing a line in a mainboard.
    /// Unlike bounded picker Search, this also finds unowned/off-screen
    /// names and never conflates Arena with physical inventory.
    /// </summary>
    public static long CountOwned(NativeBackupView ownership, Pool pool,
        string cardName)
    {
        ArgumentNullException.ThrowIfNull(ownership);
        if (string.IsNullOrWhiteSpace(cardName)) return 0;
        long copies = 0;
        if (pool == Pool.Physical)
        {
            foreach (var row in ownership.PhysicalCards)
                if (row.Quantity > 0 &&
                    row.Name.Equals(cardName, StringComparison.OrdinalIgnoreCase))
                    copies = checked(copies + row.Quantity);
        }
        else
        {
            foreach (var row in ownership.ArenaCards)
                if (row.Quantity > 0 &&
                    row.Name.Equals(cardName, StringComparison.OrdinalIgnoreCase))
                    copies = checked(copies + row.Quantity);
        }
        return copies;
    }

    public sealed record Shortage(string Name, int Required, long Owned)
    {
        public long Missing => Math.Max(0, Required - Owned);
    }

    public sealed record DraftSummary(int DistinctCards, int MainboardCopies,
        int TotalWithCommander, int CoveredCopies, long MissingCopies,
        IReadOnlyList<Shortage> Shortages)
    {
        public string Header => $"{MainboardCopies:N0} mainboard copies · " +
            $"{DistinctCards:N0} names · {TotalWithCommander:N0} incl. commander · " +
            $"{MissingCopies:N0} missing copies";
    }

    /// <summary>
    /// Immediate read-only deck-versus-inventory accounting. The separate
    /// commander is included only in Commander and Arena Brawl formats.
    /// Coverage is per card and cannot be transferred between names or pools.
    /// </summary>
    public static DraftSummary Review(NativeBackupView ownership, Pool pool,
        string mainboard, string? commander, string? format)
    {
        ArgumentNullException.ThrowIfNull(ownership);
        var entries = NativeDeckEditing.ParseMainboard(mainboard);
        bool commanderFormat = string.Equals(format?.Trim(), "Commander",
            StringComparison.OrdinalIgnoreCase) ||
            string.Equals(format?.Trim(), "Arena Brawl",
                StringComparison.OrdinalIgnoreCase);
        string leader = commanderFormat ? (commander ?? "").Trim() : "";
        var required = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var card in entries)
            required[card.Name] = checked(required.GetValueOrDefault(card.Name) + card.Quantity);
        if (leader.Length > 0)
            required[leader] = checked(required.GetValueOrDefault(leader) + 1);
        // Unlike bounded picker results, accounting must cover every owned name.
        var owned = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        if (pool == Pool.Physical)
            foreach (var card in ownership.PhysicalCards)
                if (card.Quantity > 0)
                    owned[card.Name] = checked(owned.GetValueOrDefault(card.Name) + card.Quantity);
        if (pool == Pool.Arena)
            foreach (var card in ownership.ArenaCards)
                if (card.Quantity > 0)
                    owned[card.Name] = checked(owned.GetValueOrDefault(card.Name) + card.Quantity);
        var shortages = required.Select(x => new Shortage(
                x.Key, x.Value, owned.GetValueOrDefault(x.Key)))
            .Where(x => x.Missing > 0)
            .OrderByDescending(x => x.Missing)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        int mainboardCopies = entries.Sum(x => x.Quantity);
        long missing = shortages.Sum(x => x.Missing);
        int total = checked(mainboardCopies + (leader.Length > 0 ? 1 : 0));
        return new DraftSummary(entries.Count, mainboardCopies, total,
            checked((int)(total - missing)), missing, shortages);
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
    /// <summary>
    /// EDH Lab's double-click quick-add is idempotent by card name:
    /// clicking any existing mainboard card (including a basic land or a
    /// card with a special deck copy limit) leaves the draft untouched.
    /// The manual Add (+1) action intentionally retains its usual limits.
    /// No inventory, saved deck or commander slot is modified.
    /// </summary>
    public static string AddIfAbsent(string mainboard, string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 255 ||
            name.Any(char.IsControl))
            throw new ArgumentException("Invalid selected card name.", nameof(name));
        var entries = NativeDeckEditing.ParseMainboard(mainboard);
        if (entries.Any(x => x.Name.Equals(name.Trim(),
                StringComparison.OrdinalIgnoreCase)))
            return mainboard;
        return AddOne(mainboard, name);
    }

    /// <summary>
    /// Decrement one draft copy. Remove the row entirely at quantity one.
    /// Ownership is never consulted or modified. Refuse malformed drafts or
    /// unlisted card names rather than silently discarding user text.
    /// </summary>
    public static string RemoveOne(string mainboard, string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 255 ||
            name.Any(char.IsControl))
            throw new ArgumentException("Invalid selected card name.", nameof(name));
        var entries = NativeDeckEditing.ParseMainboard(mainboard);
        var current = entries.FirstOrDefault(x =>
            x.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));
        if (current is null)
            throw new InvalidDataException("This card is not in the current mainboard.");
        return string.Join("\n", entries.Where(x =>
                !x.Name.Equals(current.Name, StringComparison.OrdinalIgnoreCase) ||
                x.Quantity > 1)
            .Select(x => (x.Name.Equals(current.Name, StringComparison.OrdinalIgnoreCase)
                ? x.Quantity - 1 : x.Quantity) + " " + x.Name));
    }

    /// <summary>
    /// Set one EXISTING mainboard entry to an exact quantity (0 removes it).
    /// The caller enforces current format-specific copy limits on increases.
    /// Parsing prevents partial text edits or invalid deck structures.
    /// There is no collection, card catalog or saved-deck write.
    /// </summary>
    public static string SetQuantity(string mainboard, string name, int quantity)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 255 ||
            name.Any(char.IsControl))
            throw new ArgumentException("Invalid selected card name.", nameof(name));
        if (quantity is < 0 or > 250)
            throw new ArgumentOutOfRangeException(nameof(quantity),
                "A mainboard entry must have 0–250 copies.");
        var entries = NativeDeckEditing.ParseMainboard(mainboard);
        var current = entries.FirstOrDefault(x =>
            x.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));
        if (current is null)
            throw new InvalidDataException("The selected card is no longer in the mainboard.");
        if (current.Quantity == quantity) return mainboard;
        if (entries.Sum(x => x.Quantity) - current.Quantity + quantity > 1_000)
            throw new InvalidDataException("The deck editor supports at most 1,000 copies.");
        return string.Join("\n", entries
            .Where(x => !x.Name.Equals(current.Name, StringComparison.OrdinalIgnoreCase) ||
                quantity > 0)
            .Select(x => (x.Name.Equals(current.Name, StringComparison.OrdinalIgnoreCase)
                ? quantity : x.Quantity) + " " + x.Name));
    }

    /// <summary>
    /// Normalized, deterministic sorting for name/quantity-only mainboards.
    /// Parsing first prevents incomplete or unsupported lines being lost.
    /// </summary>
    public static string SortAlphabetically(string mainboard)
    {
        return string.Join("\n", NativeDeckEditing.ParseMainboard(mainboard)
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .Select(x => x.Quantity + " " + x.Name));
    }

}
