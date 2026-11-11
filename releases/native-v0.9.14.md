# Arcane Vault Native Windows v0.9.14 (Preview)

Native Windows feature and reliability update. Existing private collection databases are preserved across updates.

## Added

- Arena Collection CSV / Excel (`.csv`, `.xlsx`) importer with a name-and-quantity preview, additive import confirmation, duplicate-name aggregation, and atomic rollback protection.
- Responsive multi-column Card Art browsing with card images, printing selection, and details activation.
- Catalog sorting and role-based browsing enhancements carried forward from recent native updates.

## Changed

- Card Catalog opens in Card Art mode with the Owned Physical Printings filter by default.
- Set abbreviations are displayed in uppercase (for example TRK, KHM, ZNR).
- Remove numbered `Physical 1` / `Physical 2` ownership badges while preserving actual quantities.
- Improved vertical padding and heights of Card Catalog text fields to avoid clipping.
- Launch in one main window; Collection and Deck Library are loaded on demand from sidebar navigation instead of being initialized automatically.

## Fixed

- Fixed the confirmed Set Progress hard crash caused by binding the read-only `NativeSetProgress.SetRow.Percent` property TwoWay. ProgressBar now uses an explicit OneWay binding, with regression coverage.
- Added actionable UI error logging and safe handling for Set Progress view failures, including diagnostic output under `%LOCALAPPDATA%\\ArcaneVault\\Diagnostics`.
- Preserve existing physical collections, saved decks, and metadata during Arena imports.

## Removed

- Removed automatic loading of the full Collection / Deck Library workspace during application startup; these sections remain available through the sidebar.
- Removed the redundant numbered physical copy-count display labels in the catalog. Ownership data is unchanged.

## Testing notes

Please verify My Collection, Deck Library, Set Progress, Arena CSV/XLSX import, and Card Art multi-column layout on Windows at your display scaling. This native build is a preview, not 1.0. Updates replace executable versions rather than collection data.
