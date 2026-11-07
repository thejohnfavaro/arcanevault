# Arcane Vault for Windows — v5.19.0

## Release overview

Physical Collection can now be sorted by the real release dates of Magic sets, with newest sets first. You can also choose and manage specific physical printings of each card, including owning several printings with different quantities, finishes, and editions.

## Added

- **Sort inventory:** Card name (A–Z), Set release date (newest first), or Set release date (oldest first). The selected mode persists across app launches.
- **Released column:** The physical inventory table displays the release date of the selected set from the locally synced Scryfall printing catalog. Unknown release dates appear as “—” and sort last in either release-date direction.
- **Choose printing** on each inventory entry. Select an exact physical printing by set/edition and collector number; preview artwork and release date; choose nonfoil, foil, or etched finish; and specify the quantity.
- **Assign existing copies** moves all or some existing copies to a selected printing without inflating ownership. If only part of the quantity is reassigned, remaining copies stay on the original entry.
- **Add copies of this printing** adds additional copies while retaining separate physical printing identities and finishes; identical entries consolidate using the existing inventory normalization.
- **Manage physical printings** action from Card Details opens the physical printing selector for that card.
- **Owned physical printings** listing in Card Details shows each recorded physical version, finish, set and quantity.

## Changed

- Local catalog service exposes authoritative cached set-release information. Sorting is based on the earliest known release date among Scryfall printings for each set, not alphabetical set codes or the inventory import date.
- Physical printing picker excludes digital-only variants and prioritizes recently released printings. It is backed by the existing local Scryfall printing catalog.
- Selected physical printings are retained in backups and physical CSV exports via existing set, collector number, finish, and Scryfall ID fields.
- Physical collection value calculations continue to use the exact printing and finish recorded for each entry.

## Fixed

- Printed editions of the same card are no longer forced into a single manually assigned version.
- Editing a printing assignment does not silently add another copy: reassignment moves ownership, while the separate Add action deliberately increases the quantity.
- Unknown set release dates are sorted after dated sets in both newest-first and oldest-first modes.

## Removed

- No existing functionality removed.

## Important details

- The local **Scryfall catalog + variants** must be downloaded to supply physical printings and set release dates. Without it, name-based sorting and manual inventory entry remain available, but release dates and printing choices cannot be populated.
- Release-date sorting is based on known Scryfall printing data. Newly released sets may require refreshing the Scryfall catalog.
- If a printing is already represented in the inventory, assigning or adding its copies consolidates identical matching entries. Different finishes, sets, or collector numbers remain separate.
- To preserve the integrity of your collection, do not use **Add copies** merely to identify an existing card—use **Assign existing copies**.
- All changes remain local to physical inventory and do not alter Arena ownership.

## Installing

Open **Import & Data → Application updates → Check for updates** and install v5.19.0. The Windows release ZIP includes a SHA-256 verification file.
