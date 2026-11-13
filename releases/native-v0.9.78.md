# Arcane Vault Native Windows v0.9.78 — Official WUBRG and exact mana-cost audit

## Added
- One shared, responsive **official Scryfall mana-symbol strip** for **exact mana costs** and **color identities**, both drawing from existing locally cached verified `svgs.scryfall.io` art. The modes deliberately never reinterpret costs as identity or identity as mana production.
- **Exact casting cost pips** in Card Details, the persistent Card Catalog inspector, the saved Deck Library card inspector, the EDH Lab preview, and the selected Commander inspector. E.g., `{2}{U}` displays generic-two followed by blue, not two blue mana. Includes `{W/U}`, `{2/U}`, Phyrexian `{U/P}`, `{C}`, numeric generic, snow, X, and intact split-face separators.
- A detailed [mana symbol surface audit](../docs/arcane-vault-next/NATIVE_MANA_SYMBOL_AUDIT.md) and regression assertions for symbol order, exact token preservation, all twelve interactive WUBRG filters, old-catalog fallback, and no data changes.
- The local `CardDetails` read model now has a separate `ManaCost` field sourced from cached Scryfall `mana_cost`. Older SQLite catalog snapshots without that column remain fully readable; a missing cost stays blank, not fabricated.

## Changed
- **Main Catalog color-identity filter:** W/U/B/R/G/colorless plain-letter checkboxes replaced with official SVG icons plus readable color names. Existing checkbox names, options, and selection events are preserved.
- **Commander Browser color filters:** official mana symbols and names replace plain W/U/B/R/G letters.
- **Commander Browser result rows:** official symbols now represent WUBRG identity (including colorless), replacing concatenated raw-letter labels. The result list remains virtualized.
- **EDH Lab card preview:** its raw `Identity: WU` text is now a distinct official color-identity symbol strip, separate from casting cost. A loading/unverified preview doesn't temporarily pretend the card is colorless.
- **Commander inspector** mana-cost metadata reads are asynchronous with an explicit stale-selection guard. Preview remains read-only; no extra deck or collection mutations.

## Fixed
- Cards with `{2}{U}` or `{U}{U}` used to lack a dedicated, exact graphical casting-cost display in native inspectors; a reader could only infer from mana value and a WUBRG text identity. Now the original Scryfall tokens are displayed as distinct official pips.
- Color identity in several native filter/result surfaces was still plain letters even after Card Details/Oracle had moved to official icons. These are standardized through the shared renderer.
- Long/hybrid/Phyrexian/split costs and absent/offline symbol SVGs retain their literal token strings; no invented alternative shapes are drawn.

## Removed
- Removed the old Commander result-list `ColorText` WUBRG-letter presentation property. No saved cards, actual mana-cost metadata, inventory, printings, prices, deck revisions or original Oracle text were removed.
- No custom/hand-drawn symbols, new arbitrary SVG providers or network requests per card row were introduced. Cached Scryfall symbols remain the only graphical source.

## Windows acceptance
1. Open native-v0.9.78 and visit Card Catalog → color identity filters. Verify each W/U/B/R/G/colorless option has a **real official glyph** and a color name, with selection behavior unchanged.
2. Open Commander Browser. Verify identity filters and candidate rows use official WUBRG icons in canonical order (including full WUBRG), without truncating the last pip.
3. Select cards with `{2}{U}`, `{U}{U}`, `{W/U}`, `{2/U}`, `{U/P}`, `{X}`, and `{C}`; compare inspector casting-cost icons against printed/card image cost. The former two must **not** look equivalent.
4. Visit EDH Lab Cards, Manage, Commander inspector, Card Details and Deck Library saved-card inspector. Check exact mana costs, **distinct** color-identity icons, and correctly rendered Oracle symbols across these views.
5. Disable internet after warming a few symbols, restart and confirm cached icons work offline; uncached symbols fall back to intact literal `{token}` strings rather than fake icons.
6. Check split/modal double-faced costs, colorless cards, old catalogs missing `mana_cost`, light/dark themes and 100/125/150% DPI. No card selection should add a card or mutate ownership.
7. Verify v0.9.77 proxy counts and excluded valuation, deck-saving preflight, mainboard searches and package double-click actions still work.

**Next increments:** commander-color/collection-aware land discovery (#168) and per-deck Mainboard printing selection (#169). Both will reuse exact official glyphs and preserve the proxy/owned-printing boundary.
