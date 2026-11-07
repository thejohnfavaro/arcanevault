# Arcane Vault for Windows v5.21.6

## Added
- **Fetch All Owned Card Details** in Import & Data to enrich missing Arena and physical card metadata, with optional refresh of existing details and progress reporting.
- Verified first-face fallback for split, adventure and other multi-face card lookups that are not resolved by their combined name.

## Changed
- Physical Set Progress counts ownership only when a stored Scryfall printing ID or exact set, collector number and card name matches the local catalog.
- Arena name-only imports no longer claim ownership of individual set printings, including unreleased sets. Set completion is conservative until printing identity is known.
- Bulk card-detail refresh saves completed batches and can resume after interruption without redownloading the full printings catalog.

## Fixed
- Cards with `Card Name // Other` sometimes failed Scryfall detail lookup or were assigned incorrect face metadata.
- Shared card names no longer inflate physical set completion across reprints.

## Removed
- Name-only guesses when computing set-specific ownership.
