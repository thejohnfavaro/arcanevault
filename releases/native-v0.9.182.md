# Arcane Vault Native Windows v0.9.182 (Preview)

## Fixed
- Corrected an old Deck Library presentation cache that displayed **Commander bracket: Not applicable** even for saved Commander decks. Deck summaries now invalidate the pre-bracket cache and verify bracket fields before using cached rows.
- Hidden the Commander bracket badge entirely on Arena Brawl and non-Commander deck cards and in the saved-deck inspector instead of displaying Not applicable.

## Changed
- Only tabletop Commander decks render minimum-bracket labels. Existing Commander decks recalculate provisional minimums from their saved card lists without modifying ownership or saved deck content.

## Added
- Regression coverage for stale legacy deck-summary cache invalidation and restored Commander bracket metadata.

## Removed
- Unnecessary Commander bracket messaging for formats where Commander Brackets do not apply.

## Verification
- Run WPF compilation, native regression checks and release packaging. Verify a Commander deck shows Minimum bracket and Arena Brawl decks show no Commander bracket badge; no collection data is changed by the cache rebuild.
