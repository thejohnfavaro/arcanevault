namespace ArcaneVault.Next.EDHLab;

/// <summary>
/// Read-only presentation of the UNSAVED mainboard, using only local eligible
/// catalog hints. The deck text remains the source of truth and no inventory
/// or saved-deck data is written by grouping/filtering.
/// </summary>
public static class EdhLabMainboardView
{
    public enum Section { All, Lands, Creatures, Artifacts, Enchantments, Spells, Other }
    public enum Order { Type, Name, Quantity }

    public sealed record Row(string Name, int Quantity, string Category,
        string Details, bool IsHeader = false, bool IsVerified = true)
    {
        public string NameLabel => IsHeader ? Name : Name;
        public string QuantityLabel => IsHeader ? "" : "×" + Quantity;
        public string DetailsLabel => IsHeader ? "" : Details;
    }

    private static readonly string[] SectionOrder =
    [
        "Lands", "Creatures", "Artifacts", "Enchantments",
        "Instants", "Sorceries", "Planeswalkers", "Other", "Unverified"
    ];

    private static string CategoryFor(EdhLabDraftCatalog.Card? card)
    {
        if (card is null) return "Unverified";
        string type = card.Type;
        if (type.Contains("Land", StringComparison.OrdinalIgnoreCase))
            return "Lands";
        if (type.Contains("Creature", StringComparison.OrdinalIgnoreCase))
            return "Creatures";
        if (type.Contains("Artifact", StringComparison.OrdinalIgnoreCase))
            return "Artifacts";
        if (type.Contains("Enchantment", StringComparison.OrdinalIgnoreCase))
            return "Enchantments";
        if (type.Contains("Instant", StringComparison.OrdinalIgnoreCase))
            return "Instants";
        if (type.Contains("Sorcery", StringComparison.OrdinalIgnoreCase))
            return "Sorceries";
        if (type.Contains("Planeswalker", StringComparison.OrdinalIgnoreCase))
            return "Planeswalkers";
        return "Other";
    }

    private static bool InSection(string category, Section section) => section switch
    {
        Section.All => true,
        Section.Lands => category == "Lands",
        Section.Creatures => category == "Creatures",
        Section.Artifacts => category == "Artifacts",
        Section.Enchantments => category == "Enchantments",
        Section.Spells => category is "Instants" or "Sorceries",
        Section.Other => category is "Planeswalkers" or "Other" or "Unverified",
        _ => throw new ArgumentOutOfRangeException(nameof(section))
    };

    public static IReadOnlyList<Row> Build(EdhLabDraftCatalog? catalog,
        IReadOnlyList<(string Name, int Quantity)> entries,
        Section section = Section.All, Order order = Order.Type,
        string? search = null)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (!Enum.IsDefined(section)) throw new ArgumentOutOfRangeException(nameof(section));
        if (!Enum.IsDefined(order)) throw new ArgumentOutOfRangeException(nameof(order));
        search = (search ?? "").Trim();
        if (search.Length > 255)
            throw new ArgumentException("Mainboard search must be 255 characters or fewer.", nameof(search));

        var cards = entries.Select(entry =>
        {
            var found = catalog?.Find(entry.Name);
            var category = CategoryFor(found);
            var detail = found is null
                ? "Not verified in this local commander/format catalog"
                : found.Type + (found.RoleHints.Length == 0 ? ""
                    : " · " + found.RoleHints);
            return new Row(entry.Name, entry.Quantity, category,
                detail, IsVerified: found is not null);
        }).Where(x => InSection(x.Category, section) &&
            (search.Length == 0 ||
             x.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
             x.Details.Contains(search, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        if (order == Order.Name)
            return cards.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Name, StringComparer.Ordinal).ToArray();
        if (order == Order.Quantity)
            return cards.OrderByDescending(x => x.Quantity)
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();

        var rows = new List<Row>(cards.Length + SectionOrder.Length);
        foreach (var category in SectionOrder)
        {
            var group = cards.Where(x => x.Category == category)
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();
            if (group.Length == 0) continue;
            rows.Add(new Row(category + " · " + group.Length + " names · " +
                group.Sum(x => x.Quantity) + " copies",
                0, category, "", IsHeader: true));
            rows.AddRange(group);
        }
        return rows;
    }
}
