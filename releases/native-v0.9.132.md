# Arcane Vault Native Windows v0.9.132

## Added
- Optional, bounded per-deck matchHistory events (up to twenty confirmed outcomes with UTC dates) in the existing Stream Deck compatibility snapshot. Records are associated by unique Arena deck ID or unambiguous deck name.
- Read-only verified physical Set Progress summary in the same snapshot: owned collector-number slots, catalog paper slots and up to four sets with the most owned slots.
- Per-deck recent-match data enables XENEON EDGE v0.2 rolling win-rate views without exporting match IDs, opponent information or raw Arena logs.

## Changed
- The Stream Deck snapshot remains schemaVersion 1 with additive properties. Existing Companion v0.9 keys continue using the unchanged deck IDs, names, wins/losses and keyImagePath.
- Optional Set Progress calculation occurs after the initial deck snapshot and is cached against workspace/catalog file identity, with a bounded retry interval. Failures leave the deck keys functional.

## Fixed
- EDGE no longer needs to attribute a global latest match to the selected deck when its own verified history is available.

## Removed
- No functionality removed.

This export is presentation-only. It never changes recorded matches, saved decks, ownership, or catalog data. The new dashboard panels require XENEON EDGE Widget v0.2.
