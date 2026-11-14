# Arcane Vault Windows v0.9.112 — Multi-Mechanic Synergy Search

## Added
- Separate multiple mechanic or Oracle-text phrases with commas, e.g. `Connive, Discard`.
- **Match All** requires every phrase in a card's Oracle text; **Match Any** finds cards matching at least one.
- Regression coverage for combined phrases, AND/OR matching, literal wildcard characters, and invalid input.

## Changed
- Keyword / Rules Text searches accept up to eight nonempty comma-separated phrases, with case-insensitive literal matching.
- Mechanic match mode composes with existing color, rarity, type, set, utilization, and collection ownership filters.

## Fixed
- Previous keyword filter treated the entire comma-separated input as one literal sentence and could not search for overlapping synergies.

## Removed
- No catalog records, printing data, collection data, decks or existing filters removed.

## Safety
- Queries remain bounded, parameterized and read-only on the locally cached Scryfall catalog.
- Full Scryfall Sync remains necessary for older metadata-limited catalogs.
