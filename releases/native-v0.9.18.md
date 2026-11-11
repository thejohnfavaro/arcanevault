# Arcane Vault Native Windows v0.9.18 (Preview)

Set Progress banner artwork sizing polish.

## Changed
- Set Progress image fills each set tile edge-to-edge and crops excess illustration area instead of influencing the tile layout.
- Preserve the existing **196-pixel** set-banner height, responsive column count, full-bleed Scryfall illustration crops, official set-symbol badges, dark overlays, progress bars, and collection labels.
- Image dimensions follow the rendered tile viewport; centered `UniformToFill` cropping keeps the content area stable at different window sizes and display scaling.

## Fixed
- Prevent natural image proportions from making the set artwork box larger or leaving unwanted whitespace. The banner may deliberately crop away portions of the illustration.

## Testing
- In **Set Progress**, review sets with portrait and landscape illustrations, resize the window, and confirm the image fills the banner without enlarging it. Verify responsive columns at different window sizes.
- Physical and Arena progress caches, authoritative collection database, set checklist, and Hide Unowned behavior are unchanged.
- No collection migration or data edits. This is a Windows preview, not 1.0.
