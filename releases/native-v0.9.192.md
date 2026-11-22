# Arcane Vault Native Windows v0.9.192 (Preview)

## Added
- Deck Analytics includes **By your deck color identity** with matches, wins, losses and win rate, derived from official commander metadata in the local Scryfall catalog.
- **Against opponent color identity** remains separately available, based on explicitly recorded opponent tags in Match History.
- Regression coverage for exact native deck IDs, name fallback, duplicates, unknown vs truly colorless decks and confirmed results.

## Changed
- Match History's analytics reader retains the stable native saved-deck ID instead of discarding it; archived name-only matches use only unique saved-deck names as a fallback.
- Deck Analytics shows unresolved commander identities as Unknown and does not treat missing identity as colorless.
- Saved commander printing choices now flow through the Stream Deck / XENEON EDGE exporter and Windows Deck Library, including Arena Brawl cosmetic art.
- Deck Library tiles have consistent size.

## Fixed
- Color-identity analytics were incomplete: own-deck colors were absent and stable saved deck IDs were discarded.
- XENEON EDGE continued showing generic commander art even after a selected printing was saved.
- Arena Brawl saved deck covers could ignore the chosen cosmetic Scryfall printing.

## Removed
- Nothing. No match outcomes, collection quantities, deck ownership or Arena styles are changed.

## Verification
- Choose an Arena Brawl deck in Match History; add a verified opponent color identity to a recorded result.
- Open Deck Analytics, Refresh, select the same deck and verify both **your deck** color identity and the **opponent** identity tables under the date/format filters.
- Confirm Unknown opponent identities remain separate from colorless C and results never double count an Arena match ID.
- Save a commander printing; confirm Deck Library and EDGE show the correct saved artwork when the native export refreshes.
