# Arcane Vault Native Windows v0.9.185 (Preview)

## Added
- Shared compact numeric input style for proxy transfer quantities, proxy counts, physical and Arena quantities, land targets, and mana value filters.
- Input-layout regression checks for high-DPI-friendly text, numeric fields, and preserved multiline deck editing.

## Changed
- Native single-line textboxes now use centered input hosting, reduced vertical padding, layout rounding, and pixel snapping to avoid clipped characters.
- Editable/standard ComboBox inputs receive centered content and improved default padding.
- Search fields in Card Catalog, Collection, Deck Library, Set Progress, Commander discovery, EDH Lab, and Shortlist use consistent text padding.
- Physical, Arena and proxy edit windows use the same compact quantity-input baseline.
- Multiline deck and AI text editors retain their multiline vertical scrolling and spacing.

## Fixed
- Input text or digits appearing cropped at the bottom of short textboxes, especially at display scaling above 100%.

## Removed
- Excessive per-control vertical padding in affected single-line input fields. No data, workflows, or collection records were removed.

## Verification
- Run the native WPF compile, input-style regression checks, full native tests, and release packaging. Validate typing `1`, `9`, `g`, `p`, `q`, and `y` in proxy, Catalog, and EDH Lab inputs at 100%, 125%, and 150% Windows scaling on the target PC.
- All data edits retain their previous SHA verification, atomic write, and recovery behavior.
