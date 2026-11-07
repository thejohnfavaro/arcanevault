# Arcane Vault Windows v5.21.12 — Illustrated Set Progress

## Added
- Optional illustrated set tiles in Set Progress with a saved **Show set artwork** toggle.
- A curated promotional-art source for Bloomburrow (BLB) and its Commander counterpart (BLC), using Wizards of the Coast's published set illustration without bundling image binaries.
- Distinct gold framing for fully completed sets.

## Changed
- Expansion symbols remain prominent over dark, high-contrast art overlays, alongside set name, release date, confirmed owned count, missing count and percentage.
- Set images load lazily; artwork lookup prefers a curated official promotional image, then falls back to representative Scryfall card art and finally the existing gradient when artwork is unavailable.
- Reduced-motion preferences are respected in set tiles.

## Fixed
- Prevent artwork availability from hiding set progress or expansion-symbol fallbacks.

## Removed
- No features removed.

## Data safety
- No collection, printing ID, metadata schema or set-completion calculation changes.
- Promotional image is remotely hosted by Wizards of the Coast; card art remains credited to its respective rights holders. Arcane Vault is unofficial fan content, not endorsed by Wizards of the Coast.
- Other sets continue using representative card art until verified promotional images are curated.
