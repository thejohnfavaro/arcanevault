# Arcane Vault Windows v0.9.228 (Preview)

## Added
- Complete, individually listed ownership shortages in the scrollable deck editor panel.
- Regression checks preventing the shortage list from reverting to a fixed first-six display.

## Changed
- Show every missing card with required, owned, proxy and remaining-shortage quantities in the relevant inventory pool.
- Keep both the saved-deck editor and embedded EDH Lab shortage panels scrollable.

## Fixed
- Removed the obsolete "… plus N more card names." truncation of the ownership shortage report.

## Removed
- The first-six shortage display cap and abbreviated remaining-card count. No saved cards, decks, proxies, collection records or backups are removed.

## Verification
- Build the Windows application and execute the native regression suite.
- Verify a deck with more than six shortages displays all names when scrolling to the bottom in both deck editors.
- Confirm ownership pool changes recalculate the full displayed shortage list without modifying deck or collection data.
