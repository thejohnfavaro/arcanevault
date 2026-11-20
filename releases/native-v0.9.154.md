# Arcane Vault Native Windows v0.9.154 (Preview)

## Added
- Regression coverage for annotated MTG Arena deck entries in Arena and physical ownership calculations, proxy coverage, and duplicate prevention.

## Changed
- EDH Lab now treats an optional `(SET) collector-number` suffix from legacy Arena decklists as a display annotation during read-only card identity lookup, without rewriting saved deck entries or their selected printings.
- Card browser `In deck` counts, draft search filters and commander mechanic hints recognize annotated saved entries.

## Fixed
- EDH Lab card previews can now load official offline metadata and artwork for cards such as `Assembly Specialist (TRK) 4` using the base catalog card name.
- Arena and physical owned-copy previews and shortages now reconcile annotated deck names against recorded inventory names instead of showing false missing copies.
- Paper proxy counts recognize annotated deck entries while remaining separate from real ownership and valuation.
- Double-click/add-one does not duplicate a card that already exists with a printing suffix.

## Removed
- Nothing. Physical holdings, Arena inventory, proxy ledger, deck text and match history remain unchanged by these read-only identity lookups.

The Windows build is a preview. If a card truly isn't in the installed offline Scryfall catalog, or is filtered out by its selected format, it can still be locally unverified. Keep a private full collection backup before updating.
