# Arcane Vault for Windows v5.21.2

## Added
- **Sync All Card Data** in Import & Data: one command downloads the complete Scryfall printing catalog, then refreshes owned-card details and all reported format legalities.
- Live two-stage sync progress and a clear completion/error message.
- **Hide sets with no progress** checkbox in Set Progress for both Arena and physical collections; preference is saved on this device.

## Changed
- Replaced four redundant visible card maintenance buttons with one unified action.
- Bulk catalog indexing completes before owned-card metadata and legalities are refreshed.
- Set filters only change the display; ownership quantities are never changed by maintenance.

## Fixed
- Card data maintenance actions previously required multiple manual clicks and could be started out of order.

## Removed
- Four separate visible card metadata, catalog, database and format legality sync buttons.
