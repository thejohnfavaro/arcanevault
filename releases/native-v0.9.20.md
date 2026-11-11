# Arcane Vault Native Windows v0.9.20 (Preview)

Set Progress artwork cover-rendering correction following user verification of v0.9.19.

## Added
- Native Set Progress image-brush background that fills the fixed tile independently of WPF image layout measurements.
- Regression check that set art uses a centered UniformToFill brush inside a fixed-height clipped tile.

## Changed
- Replaced layout-sized image presentation with a tile background image brush. Excess artwork is cropped on all sides as necessary; the set banner stays 196 pixels high.
- Reuses the existing artwork downloader, local cache, cancellation, decode and the official Scryfall art-crop image URL.
- Preserves cached Physical/Arena set progress summaries, the official set symbol, dark gradient overlays, set labels and progress bars.

## Fixed
- Set preview illustrations can no longer determine tile measurement, leave layout bars, or increase tile height on the basis of their aspect ratio.
- Previously published v0.9.19 width/height image bindings offered no meaningful change to the artwork presentation.

## Removed
- Image width/height bindings to the tile's ActualWidth/ActualHeight, which could produce a measure/arrange feedback cycle.

## Verification
- Check Set Progress with normal and narrow windows. The background should fill the entire banner, cropped to its bounds, without growing the 196-pixel tile.
- Preview only; no collection, cache-schema, or data migration changes.
