# Arcane Vault Native Windows v0.9.195 (Preview)

## Added
- **Arena Standard** in the native Windows saved-deck format picker, for tracking ranked 60-card MTG Arena constructed decks alongside Arena Brawl and Commander.
- Arena Standard preflight uses the Arena digital collection, checks a minimum 60-card mainboard, nonbasic four-of limit and official cached Scryfall Standard legality.
- A saved Arena Standard deck's cover uses an illustrative mainboard card rather than requiring a commander; Deck Library and XENEON EDGE receive the same art source.
- Regression cases for the 60-card minimum, Standard illegal cards, digital shortages, banned/unavailable cards, and commander-zone exclusion.

## Changed
- The editor chooses Arena ownership by default for Arena Standard; inventory remains separate and unmodified.
- Native streamdeck.json exports Standard decks as eligible for XENEON EDGE with per-deck results, selected artwork and match history.
- **Arena Standard Edge results count only recorded Standard Ranked queue matches**. Unknown queues, Standard Play and other formats are not assumed to be ranked. Explicit Standard queue results are excluded from Brawl displays.
- Existing Match History deck ID mapping and Deck Analytics enable reviewing Standard Ranked results per saved deck and queue.

## Fixed
- The old generic 60-card label did not distinguish ranked Arena Standard decks, and the EDGE's Brawl-only list excluded Standard decks.
- Noncommander Arena decks previously had no artwork source in the deck-cover export.

## Removed
- Nothing. No saved deck, match outcome, Arena collection ownership, or Stream Deck W/L record is rewritten.

## Verification
- Create or edit a 60-card saved deck, choose Arena Standard and leave Commander blank.
- Validate 60+ mainboard cards, cached Standard legality, four-of limits, and coverage against Arena rather than physical collection.
- Assign a verified Standard Ranked Arena match to the saved deck in Match History, ideally with an exact Arena deck ID. Review Deck Analytics and XENEON EDGE widget v1.0.3.
- Confirm the EDGE's Standard record counts Ranked Standard only, can auto-switch on new results, and never includes Standard Play or Arena Brawl outcomes.
- The live Standard rotation, bans, sideboard rules and ranked event eligibility are not certified by the offline snapshot and should be checked in Arena.
