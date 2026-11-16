# Arcane Vault Native Windows v0.9.131

## Added
- Official Scryfall `art_crop` artwork selection for Stream Deck deck keys, using only validated Scryfall CDN images.
- Source-aware rendering of the illustration region when falling back to a full card picture.

## Changed
- Commander art crops fill the entire 144×144 Stream Deck tile behind the existing deck name, W/L and win-percentage overlay.
- The tile cache key includes the artwork source and crop-renderer version, so a previously generated full-card tile will not prevent the updated crop from appearing.

## Fixed
- Stream Deck deck tiles no longer default to displaying the whole card frame, nameplate and rules text at button scale when official art crops are available.

## Removed
- No features removed.

This is a native Windows export/renderer update. **Stream Deck Companion v0.9** continues to work without changing its hardware-key deck cycling or settings behavior. The importer, inventory, saved deck content and recorded match results are not altered.
