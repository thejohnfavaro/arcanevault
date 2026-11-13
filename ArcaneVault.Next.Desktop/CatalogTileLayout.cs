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
    // A card never grows with the window. The XAML tile is exactly 218 DIPs
    // wide with 3 DIPs of margin on each side. Available width determines HOW
    // MANY fixed-size tiles fit; the outer ListView still virtualizes rows.
    // Reserve space for the vertical scroll bar and ListView chrome so the
    // final card is never clipped when a scroll bar appears.
    public const int TileWidth = 218;
    public const int TileMarginEachSide = 3;
    public const int SlotWidth = TileWidth + TileMarginEachSide * 2;
    public const int ViewportReserve = 24;
    // A 4K window can show more than five cards without stretching the row.
    // Keep a bound on the number of art controls in a visible virtualized row.
    public const int MaximumColumns = 16;

    public static int ColumnsForWidth(double availableWidth)
    {
        if (double.IsNaN(availableWidth) || double.IsInfinity(availableWidth) ||
            availableWidth <= 0) return 2;
        return Math.Clamp((int)Math.Floor(
            (availableWidth - ViewportReserve) / SlotWidth), 1, MaximumColumns);
    }

    public static IReadOnlyList<CatalogTileRow> BuildRows(
        IEnumerable<object> cards, int columnCount)
    {
        ArgumentNullException.ThrowIfNull(cards);
        int columns = Math.Clamp(columnCount, 1, MaximumColumns);
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
