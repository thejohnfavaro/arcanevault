# Arcane Vault Native Windows v0.9.181 (Preview)

## Added
- The shared read-only Stream Deck/Xeneon snapshot now includes each saved deck's actual format for precise downstream filtering.
- Xeneon EDGE regression coverage for Commander, Arena Brawl and other formats.

## Changed
- Xeneon EDGE deck navigation now includes **Arena Brawl** decks only. Tabletop Commander and unrelated formats remain in the saved library and shared snapshot but are omitted from the EDGE deck selector.
- The widget shows an explicit empty-state message when no Arena Brawl deck is available.

## Fixed
- Prevents tabletop Commander decks from being presented alongside Arena Brawl decks with Arena match results in the Xeneon EDGE dashboard.

## Removed
- Format-agnostic Xeneon EDGE deck selection.

## Verification
- Run Windows native workflow and the separate official iCUE widget validation/package workflow. Test the updated native snapshot and reinstall or update the EDGE widget package as necessary. Ownership, match history, saved deck metadata and Stream Deck Companion selection are unchanged.
