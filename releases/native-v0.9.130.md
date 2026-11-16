# Arcane Vault Native Windows v0.9.130

## Added
- Per-deck artwork diagnostics in the native Stream Deck compatibility snapshot, including lookup and rendering states.

## Fixed
- Stream Deck commander artwork lookups can now retry when an offline catalog becomes available, instead of remaining permanently empty until a saved deck changes.
- Commander image lookup can fall back from the catalog card-identity image to a matching official Scryfall printing image.
- Detailed per-deck artwork status now tells the Companion whether artwork was found, downloaded, rendered, or unavailable.

## Changed
- The export refreshes missing commander image metadata independently of saved-deck changes (bounded, no inventory writes).
- Manual deck refresh prioritizes the selected deck for artwork lookup and PNG generation.
- The Stream Deck JSON retains compatibility with earlier Companion releases.

All new artwork lookup and cache behavior is read-only with respect to decks, collection, pricing, and match records. The Companion v0.4 plugin separately fixes persistent Stream Deck deck selection.
