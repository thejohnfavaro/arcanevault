# Arcane Vault Windows v0.9.202 (Preview)

## Added
- A regression fixture requiring the artwork resolver to choose DSK collector #21 over another printing of Optimistic Scavenger when the saved card entry is `Optimistic Scavenger (DSK) 21`.

## Changed
- The shared deck-cover metadata lookup now interprets a terminal Arena-style `(SET) collector-number` suffix as an artwork lookup hint, without changing saved mainboard text or the selected custom cover.
- Lookups first try the corresponding official Scryfall printing, then base-card artwork and available printing artwork.
- The disposable deck cover metadata cache schema is incremented so failed earlier lookups are not retained.

## Fixed
- Previously an Arena-decorated saved card name was queried verbatim against Scryfall's base card-name column, returning no official artwork URL. Custom Deck Art in Deck Library and XENEON EDGE now use the same corrected resolver.

## Removed
- Nothing. Physical/Arena inventories, saved deck identities, custom cover choices, and working Ranked Standard match W/L records are unchanged.

## Verification
- In Deck Library choose Mono-White Auras and retry artwork. The previous message about no official URL for Optimistic Scavenger (DSK) 21 should no longer appear when an official image is present in the local Scryfall catalog.
- Confirm DSK #21 is used instead of a different printing; verify both the Deck Library and XENEON EDGE after snapshot refresh.
- Run the native Windows regression suite and build/package checks before installing. If the local catalog lacks the printing, a full Scryfall catalog sync may still be needed.
