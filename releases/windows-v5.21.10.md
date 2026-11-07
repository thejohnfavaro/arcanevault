# Arcane Vault Windows — Color Identity Filters and Rarity Styling

## Added
- Exact color identity multi-select picker covering colorless and all 31 non-empty color combinations.
- Quick selection for mono-red, mono-white, and Boros red/white together.
- Rarity badges: Common (gray), Uncommon (silver), Rare (gold), Mythic Rare (orange).

## Changed
- Color selections persist across sessions, can be combined using OR matching, and are cleared by Clear Filters.
- Both locally owned cards and the full Scryfall catalog use the same exact identity semantics, including pagination and total counts.
- Rarity labels are title-cased for display without changing the underlying Scryfall rarity field used for sorting, filters, or crafting costs.

## Fixed
- Previous dropdown did not support more than one color identity at a time.
- Catalog display no longer shows raw lowercase rarity codes in primary card views.

## Removed
- Replaced the old single-select color dropdown with a multi-select identity picker.

## Data safety
- No ownership, sync, pricing, or collection database schema changes.
- Exact identities: selecting R, W, and WR includes those three identities, not every card with red or white somewhere in a larger identity.
