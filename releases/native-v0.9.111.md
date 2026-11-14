# Arcane Vault Native Windows v0.9.111 — Mechanics and Rules-Text Search

## Added
- Main Card Catalog search now finds MTG keywords such as **Banding**, **Connive**, and **Replicate** in Scryfall Oracle text as well as matching card-name prefixes.
- Separate **Keyword / Rules Text** filter to combine ability searches with rarity, ownership, set, color identity and utilization tags.
- Case-insensitive, literal text matching (typed percent signs and underscores are not SQL wildcards), with bounded query length and regression coverage.

## Changed
- Typed Card Catalog filters debounce before searching, avoiding a full metadata query on every keystroke.
- Normal blank/legacy catalog searches retain existing indexed prefix behavior.

## Fixed
- Cards with the requested mechanic in their rules text but not in their name could not previously be found from the main catalog search.

## Removed
- No cards, collection records, decks, proxies, artwork or existing search filters were removed.

## Safety
- The catalog remains read-only. Mechanics require the locally synced Scryfall Oracle text; use Full Scryfall Sync for older catalogs.
- No new external search service or ownership data changes.
