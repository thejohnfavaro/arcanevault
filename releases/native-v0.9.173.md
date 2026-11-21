# Arcane Vault Native Windows v0.9.173 (Preview)

## Added
- Complete AI-review card recognition against the local, read-only Scryfall catalog, including card names mentioned in ordinary prose without Markdown emphasis.
- Full-name word-boundary checks, apostrophe normalization, and longest-first matching to reduce partial-word false positives.
- Regression coverage for The One Ring, Thoughtcast, Sphinx’s Tutelage, invented card names, and partial-word collisions.

## Changed
- The completed AI Review is parsed against all locally indexed card names, not just names extracted from bold spans, deck entries, and proposal tables.
- Catalog-backed card names become gold, clickable links while building the final WPF document, preserving the previous streaming and card-details behavior.
- The final card-link pass remains optional and time-limited: if the catalog cannot be read, the AI review remains visible.

## Fixed
- The One Ring remained plain text when mentioned normally alongside linked Esper Sentinel, Thoughtcast and Thought Monitor.
- AI card mentions in ordinary paragraphs could be missed even though the relevant cards were present in the local catalog.
- Improves completeness for valid card mentions in recommendation lists and swap tables, including typographic apostrophes.

## Removed
- Nothing. Decks, collection data, owned quantities, card printings, proxies, backups, sync, and ChatGPT credentials remain untouched.

## Verification
- Windows WPF compilation and native regression checks passed in PR #243 before merge.
- The versioned main branch release pipeline verifies installer packaging. Desktop interactive confirmation is still required.
