# Arcane Vault Windows v5.21.9 — Card Details Click Hotfix

## Added
- Regression coverage ensuring Card Details tab insertion references a live element in the details container.

## Changed
- Card Details panels are inserted in their intended order after moving content into the tab panels.

## Fixed
- Clicking cards in Card Catalog & Collection could appear to do nothing because the detail dialog layout threw a DOM NotFoundError before showModal.
- The detail tab layout now anchors the tab bar against the still-attached actions row, allowing the modal to open.

## Removed
- No features removed.

## Data safety
- No collection, metadata, printing, or sync records are modified.
