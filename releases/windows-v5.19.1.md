# Arcane Vault for Windows — v5.19.1

## Release overview

This version includes the physical inventory improvements from v5.19.0—sorting by set release date and managing multiple physical printings of the same card—plus a final Card Details navigation fix.

## Added

- **Physical Collection inventory sorting:** Card name, newest set release first, and oldest set release first. Uses actual set dates from the locally cached Scryfall printing catalog, not alphabetical set code order. Unknown dates sort last.
- **Choose printing:** Select physical editions by set, collector number and finish, with a card artwork preview and owned-copy count. Keep different printings and finishes separate.
- **Assign existing copies:** Move some or all recorded copies to the selected printing, retaining the remaining copies without doubling the collection.
- **Add copies:** Record additional copies of the selected printing; identical inventory entries consolidate.
- **Owned physical printings in Card Details**, with quantity and set/finish information, and a **Manage physical printings** shortcut.

## Changed

- Physical inventory displays a Released column.
- Printing selector uses locally downloaded Scryfall physical variants and excludes digital-only versions.
- Collection valuation continues to work from exact printing identities and quantities.

## Fixed

- The **Manage physical printings** shortcut now closes the Card Details dialog before opening the physical inventory editor, so the selector is fully visible and operable.
- Unknown set release dates stay last in both ascending and descending modes.
- Release-date sorting remains compatible with existing search, pagination, physical CSV import/export, and quantity editing.

## Removed

- Nothing. Arena data, physical collection data, decks, and match history remain unchanged.

## Important details

- A downloaded Scryfall catalog with printings is needed for set dates and printing choices. If it has not been downloaded, use **Import & Data → Sync catalog + variants**.
- Select **Assign existing copies** to identify cards you already own; use **Add copies** only when intentionally increasing ownership.
- The set release date comes from the earliest known printing of that set; new releases may require refreshing the catalog.

## Installing

Use **Import & Data → Application updates → Check for updates**. The Windows executable ZIP and SHA-256 checksum are included in the GitHub Release.
