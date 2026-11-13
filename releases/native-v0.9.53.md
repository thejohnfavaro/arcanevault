# Arcane Vault Native Windows v0.9.53 — Collection Integrity Comparison

Builds on the read-only v0.9.52 backup readiness checker, adding the ability to compare an explicitly selected exported schema-2 full backup to the currently active native Windows workspace. Also centers the card catalog's favorite star in its tile.

## Added

- **Options → Check backup readiness → Compare backup to active collection.** The user explicitly chooses a saved full backup; the active native workspace is opened in SQLite read-only mode with integrity and preserved-backup validation.
- SHA-256 hashes of the selected export and active preserved native backup, with an unambiguous **byte-identical** classification only when the digests match.
- Comparison of total physical/Arena copies and rows, distinct physical names, deck count, deck revision count, preferred artwork entries, and missing/invalid printing-ID copies.
- Printing-aware quantity differences: physical cards are matched using card name, set, collector number, finish and printing ID. Separate printings are never silently combined.
- Per-card physical and Arena quantity differences, capped at 20 display rows with a full differing-key count. The report distinguishes intentional changes since the historical export from actual corruption, which cannot be inferred merely from a mismatch.
- Regression tests for identical export/workspace, older export with changed Arena/deck revisions, invalid input and source-file immutability.

## Changed

- **Catalog favorite star is horizontally and vertically centered** within its existing 33×33 clickable overlay, preserving its size, appearance, click handler, owned star state and tooltip.
- Both backup readiness controls disable during a running inspection and reenable after completion. The comparison is performed off the WPF UI thread.

## Fixed

- Users can now identify inventory and deck-count differences against an exported recovery baseline without risking a restore operation.
- The comparison refuses invalid backups or attempts to compare the same file as both input and workspace.

## Removed

- Nothing. The checker remains optional and no collection, saved deck, backup file, native database, or legacy profile is modified.

## Accuracy and privacy

- This is **read-only**. No importing, merging, recovery, automatic reconciliation, or writes.
- A differing historical backup may be expected when card quantities or decks changed intentionally. A report alone does not certify or disprove collection correctness.
- Matching aggregate counts does not guarantee byte identity; only equal SHA-256 digests do.
- The displayed report includes up to 20 individual card names and printing keys (not Scryfall IDs). Copy it privately if you need to share results.
- Windows native preview only; **not v1.0**. Android companion parity is on the next parallel development track.

## On-device acceptance

1. Update using Native Updates to **v0.9.53**. Confirm the catalog overlay favorite stars appear centered in their boxes and still toggle without opening card details accidentally.
2. Export a fresh full native collection backup from the existing backup/export flow. Open **Options → Check backup readiness → Compare backup to active collection** and select it; confirm byte-identical SHA-256, zero card differences and matching counts.
3. Compare a private, older exported full backup and confirm any legitimate edits are reported separately for physical printing keys, Arena names, deck revisions and artwork counts.
4. Compare an invalid or wrong-format JSON. It must fail safely, without altering the original backup or active workspace.
5. If you have multiple printings of a card, verify they are kept as distinct lines in any quantity differences.
6. Check EDH Lab, Set Progress, favorite stars, Deck Library and update/restart still behave normally.

Release gates: Windows WPF/native regressions, checksummed portable ZIP and official release verification followed by user acceptance.
