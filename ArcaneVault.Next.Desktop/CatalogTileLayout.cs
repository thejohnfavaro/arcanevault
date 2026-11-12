using System;
using System.Collections.Generic;
using System.Linq;

namespace ArcaneVault.Next.Desktop;

/// <summary>
/// A fixed-width row of card tiles. The outer WPF ListView virtualizes these rows,
/// rather than creating artwork controls for every matching card at once.
/// </summary>
public sealed record CatalogTileRow(IReadOnlyList<object?> Cards);

public static class CatalogTileLayout
{
    // Compact tile widths leave room for more cards at typical desktop sizes.
    // Five tiles maximum still bounds per-row artwork controls, while the
    // outer ListView virtualizes rows and image loading stays throttled.
    public static int ColumnsForWidth(double availableWidth)
    {
        if (double.IsNaN(availableWidth) || double.IsInfinity(availableWidth) ||
            availableWidth <= 0) return 2;
        return Math.Clamp((int)Math.Floor((availableWidth - 16) / 225), 1, 5);
    }

    public static IReadOnlyList<CatalogTileRow> BuildRows(
        IEnumerable<object> cards, int columnCount)
    {
        ArgumentNullException.ThrowIfNull(cards);
        int columns = Math.Clamp(columnCount, 1, 5);
        object[] all = cards.ToArray();
        var rows = new List<CatalogTileRow>((all.Length + columns - 1) / columns);
        for (int i = 0; i < all.Length; i += columns)
        {
            var slots = new object?[columns];
            for (int j = 0; j < columns && i + j < all.Length; j++)
                slots[j] = all[i + j];
            rows.Add(new CatalogTileRow(slots));
        }
        return rows;
    }
}
