# Arcane Vault Native Windows v0.9.61 — Fixed-size responsive Card Catalog tiles

## Changed

- **Card-art widgets no longer stretch when resizing the app.** Every catalog tile is exactly **218 DIPs wide**, including the existing large 200 × 278 artwork, with three DIPs of horizontal margin on either side.
- **More cards per row as the window grows.** The number of columns is computed from the actual catalog viewport, the 224-DIP tile slot and a reserved scrollbar/chrome margin. The former hard five-column cap is removed; the result now supports up to 16 columns for 4K/ultrawide use without forcing every card to become an oversized panel.
- **Same size when the window shrinks.** The grid drops a column when necessary rather than reducing or enlarging the card widget. Individual card artwork, name footer and overlaid shortlist/rarity controls retain their layout.
- **Same catalog behavior and data.** All 200 matching entries on the current page are re-grouped into virtualized rows in their original order. Pagination, filters, shortlist actions, card details, native collection quantities, printing metadata and cached artwork are unchanged.
- **Performance remains bounded.** The outer ListView still virtualizes rows and the per-row card count has an explicit cap. Artwork continues to use the existing Scryfall image cache.

## Regression and on-device acceptance

1. Open **Card Catalog → Card art** and resize the window horizontally. Individual widgets should remain 218 DIPs wide; more fit in each row on larger viewports.
2. At ~1,440 DIPs of usable catalog viewport, expect roughly six cards rather than five unnecessarily stretched cards. At ~1,600 DIPs, expect seven. DPI scaling can affect the raw pixel dimensions, but WPF device-independent proportions should remain stable.
3. Confirm tiles have the same appearance and image/footer size before and after resizing, with no horizontal scrollbar or overlapping cards.
4. Browse later pages, filter to different subsets, and use the shortlist star and double-click Card Details. Verify data and order remain unchanged.
5. On narrower windows, columns should drop without changing card dimensions; at 4K, the extra width should add columns rather than ballooning blank tile backgrounds.

## Verification

Automated layout regression covers viewport widths 365, 675, 955, 1367, 1368, 1600, 2600 and 3840; exact six-column fit boundary; maximum row-card cap; and stable ordering/no omissions across 200 cards through each reflow. Native Windows CI is required before release.

**Scope:** Native Windows v0.9.61. No changes to import, sync, ownership, deck or recovery storage.
