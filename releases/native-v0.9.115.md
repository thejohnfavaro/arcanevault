# Arcane Vault Native Windows v0.9.115 — Quick Add to Physical Collection

## Added
- Add to Collection action directly from the Card Details window for physical card printings.
- Prefilled printing lookup for the selected card, with set, collector number, quantity, and finish selection.

## Changed
- Reuse the verified native paper-printing writer and full recovery backups.
- Refresh the native collection and catalog ownership after a successful direct card addition.

## Fixed
- Adding a single card no longer requires navigating away to the Physical Collection workspace or importing a CSV.

## Removed
- No inventory, deck, proxy, or catalog records are removed.

## Safety
- Exact Scryfall printing ID is revalidated before adding copies.
- Existing copies of the same printing and finish are incremented, never duplicated by name.
- Native recovery files and optimistic concurrency guard remain in use.
