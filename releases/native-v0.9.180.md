# Arcane Vault Native Windows v0.9.180 (Preview)

## Added
- A Commander minimum bracket indicator on saved deck cards and the Deck Library inspector overview.
- Per-deck persisted `bracketAssessment` metadata, including minimum bracket, Game Changer count, rules version, and computation time.
- Read-only provisional bracket computation for existing Commander decks that have not yet been resaved.

## Changed
- Every explicit Commander deck save refreshes the rules-backed bracket assessment without changing card ownership, printings, sync identities, or unrelated deck fields.
- Saved and provisional minimums are visibly distinguished; neither is presented as a final power bracket.
- Arena Brawl and non-Commander decks do not receive tabletop Commander bracket ratings.

## Fixed
- Metadata regressions cover saved-versus-provisional calculations, preserving other decks, and format changes.

## Removed
- Nothing. Existing deck data is preserved, and merely opening a deck does not write to the collection.

## Verification
- Validate the WPF build and native regression suite. Open the Deck Library to inspect a provisional minimum; save a Commander deck to persist its assessment. Final bracket computation remains a separate future milestone.
