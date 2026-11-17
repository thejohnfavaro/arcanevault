# Arcane Vault Native Windows v0.9.133

## Added
- Read-only list of up to 2,000 verified physical set progress summaries in the Stream Deck compatibility snapshot. Each entry includes only set code, display name, owned collector-number slots, total collector-number slots, and percent completion.
- XENEON EDGE widget v0.3 can browse available physical sets without new database permissions or a local HTTP server.

## Changed
- The existing optional, cached Set Progress calculation now provides both its original top four sets and an alphabetical set roster. Existing Stream Deck Companion keys and EDGE v0.2 panels ignore the added field.
- Verified Set Progress exports even when no saved Commander decks are present.

## Fixed
- An empty deck library no longer prevents the physical Set Progress snapshot from being refreshed.

## Removed
- No features removed.

This is additive presentation data only. It does not alter collection ownership, card printings, saved decks, match records, or valuation history. Set progress is based on verified physical collector-number slots and the existing catalog cache.
