# Arcane Vault Native Windows v0.9.164 (Preview)

## Added
- Regression tests for Arena-style deck printing annotations in deck analytics, including mixed annotated/plain entries and an annotated commander.

## Changed
- Offline deck analytics resolves card names to their canonical identity before local Scryfall catalog lookup and aggregates equivalent printings correctly.

## Fixed
- Deck Analytics previously displayed incorrect mana curve, card type, and color identity charts when MTG Arena-style deck names included set codes and collector numbers (for example `Sol Ring (CMM) 396`).
- Affected decks could display `Catalog matched 1/100 deck copies` because only the commander matched. The affected cards now resolve against the existing local catalog without editing deck data.

## Removed
- Nothing. No owned cards, proxy records, saved deck printing selections, snapshots, prices, or sync data are modified by the fix.

## Verification
- Windows WPF build and native regression suite, including the newly added annotated deck test, passed in pull request #234.
- Analytics continues to exclude genuinely unknown cards from numerical charts and reports coverage explicitly.
