# Arcane Vault Native Windows v0.9.127

## Added
- Regression checks for Arena ownership displayed in EDH Lab while retaining the selected format's legality and card-availability rules.
- Separate cached ownership-pool identities for Physical and Arena, even when a deck format stays unchanged.

## Changed
- EDH Lab's selected Physical/Arena owned-card pool now controls recorded quantities, independently of Commander/Brawl eligibility checks.
- Changing the owned-card pool refreshes the EDH Lab suggestions and displayed ownership status, instead of retaining a format-derived ownership snapshot.
- The same fix applies to both the embedded deck-editor workspace and the standalone editor.

## Fixed
- Cards recorded as owned in the native Arena inventory could incorrectly display as missing in EDH Lab when the selected owned-card pool differed from the deck's format-derived pool.
- A cached EDH catalog could continue presenting ownership from the wrong selected pool after switching between Physical and Arena.

## Removed
- No features removed.

No inventory, saved deck, proxy or printing data is changed by this update. All changes are read-only presentation, catalog caching and regression coverage. Existing native Stream Deck and Arena match tracking from v0.9.126 are preserved.
