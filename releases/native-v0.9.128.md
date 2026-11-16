# Arcane Vault Native Windows v0.9.128

## Added
- Stream Deck companion refresh-request bridge. A Refresh Deck Stats key or the deck key itself can request a fresh snapshot while the native app is running, without editing any user collection or deck.
- Fully composited 144 × 144 native PNG tiles showing commander card artwork, saved-deck name, and verified Arena match win/loss record.
- Stable Arena deck IDs in native deck summaries to match confirmed games even when the MTG Arena deck name differs from the saved Arcane Vault name.

## Changed
- The Stream Deck exporter checks for a manual refresh request once per second and continues its automatic 15-second refresh cadence.
- Requested deck tile is prioritized for artwork and stat rendering. Additional decks are generated incrementally without blocking the first snapshot.
- Stream Deck artwork tiles are generated as ordinary PNG files, avoiding embedded image data inside SVG backgrounds.

## Fixed
- Stream Deck buttons that kept showing the same art or stats after choosing a different deck can now display a unique pre-rendered tile for each selected deck.
- Confirmed matches can associate by unique native Arena deck ID before falling back to an unambiguous saved deck name.
- Pressing the Deck W/L key can request updated results from the running native application.

## Removed
- No native features removed.

This release requires the separately distributed Stream Deck Companion plugin v0.2 for the new Refresh Deck Stats action. Existing v0.1 buttons continue to read the compatible snapshot but cannot use the explicit refresh action or pre-rendered PNG tile. Collection and saved-deck records are never altered. Match totals include only confirmed results that can be linked unambiguously to a saved deck.
