# Windows-first stabilization gate (effective 2026-11-10)

**Owner directive:** Finish restoring a dependable, usable Windows Arcane Vault before resuming EDH Lab and new Android feature development.

## Priority order

1. **Shipping Windows stability first.** Verify installed app launches, in-app update works, local collections survive update, card browser/card details can be opened, and backup/restore is recoverable. A green GitHub compile is not a device acceptance test.
2. **Core native Windows parity next.** Reliable local Scryfall catalog, browsable physical + Arena inventory, confirmed-printing identification, Set Progress, card details, and safe backups/restores; then gated edits and rollback. Do not replace the installed app by default.
3. **Data loss/crash/performance gates.** Preserve physical printing rows and quantities, decks/revisions, artwork and Arena inventory; test live-profile compatibility on a disposable copy and measure startup/search p95 on Windows.
4. **Android companion maintenance only if blocking collection safety.** Do not start new scanner/feature work before Windows core is stable.
5. **EDH Lab paused in backlog.** Existing EDH Lab source and Issue #33 are retained, but no new EDH Lab features, testing expansion or shipping UI exposure until the Windows gate below passes.

## Windows re-entry gate for EDH Lab

All of the following must be validated: shipping Windows version installs/launches and updates without data loss; real full backup/restore (disposable profile) passes quantity/printing/deck/artwork comparison; core native card catalog, physical collection, Arena inventory, card details and Set Progress are functional; unresolved printings are visible rather than guessed; performance and stability have been measured; recovery route documented and tested. At that point, EDH Lab can be reprioritized.

## Current implementation slice: AVN-023 — new empty native collection

Arcane Vault Next adds **Start fresh collection** for both an uninitialized native profile (no legacy app/JSON export required) and an existing native collection (verified staged replacement plus non-overwritten rollback file). The action clears active physical and Arena ownership, decks/history, preferred art, favorites, wildcards and legacy settings; the independently stored Scryfall catalog, old Windows app and its data are not touched. The user is shown the data being replaced and must type `START FRESH`; **Restore native recovery** can undo the reset. CI covers first-run blank, double reset no-op, stale SHA rejection, reset/restore count conservation, and catalog isolation. **Native ownership editing is not yet available**, so this is not full standalone collection-management parity.

## Current implementation slice: AVN-022 — native refresh and rollback

An explicitly authorized **refresh from the installed legacy collection** can now replace an existing native SQLite workspace without requiring JSON export, but **only after a read-only diff and confirmation**. A complete verified native replacement is staged, the current native version's checksum is rechecked, and the previous database is retained as an automatically named recovery file. An explicit **Restore native recovery** action lists retained recovery files, verifies source/target, and preserves the newer version in another recovery point during restore. Stale comparisons, incomplete snapshots, missing printings and corrupted recovery inputs fail closed. No native editing or auto-sync. Acceptance still requires confirming real Windows data and reboot/restore behavior on the user's machine.

## Current implementation slice: AVN-021 — direct installed Windows migration

Native UI has a **Migrate installed Windows data** button which, with explicit consent and the old Windows application closed, reads the old app's origin-local browser storage using an isolated temporary WebView2 profile clone. A complete schema-2 backup is synthesized **only in memory**, verified for paper/Arena quantities and deck/art counts, then imported into the independent native SQLite workspace. The optional Scryfall catalog is safely copied to its own native store. Existing installed profile and native workspace are never overwritten. No manual full-backup export is needed for this one-time bootstrap, but the bridge currently depends on the installed WebView2 Runtime and matching legacy origin. The old app is not required for subsequent native launches.

**This is NOT validated as a working migration on the user's actual Windows machine yet.** Protect original data, test full counts with the user and keep rollback available before enabling native writes.

## Current implementation slice: AVN-020 — persistent native Windows startup

A user must explicitly import the complete legacy backup **once** and explicitly preserve a copy of the Scryfall catalog **once**. Arcane Vault Next then opens these **private native SQLite files** on launch without selecting a backup/catalog again. The imported native collection includes all physical and Arena rows plus preserved decks/history/artwork; source and native rows are checked for equality and no replace-on-import is allowed. Existing installed app data is never modified. **This is not native live editing, automatic source WebView ingestion, sync, rollback, or completed migration.** User-device restart/recovery validation is still required.

## Earlier slice: AVN-017 through AVN-019

AVN-019 adds a **read-only native Set Progress screen** from the explicitly exported schema-2 backup and an isolated Scryfall catalog snapshot. Numerators credit only catalog-verified, paper-printing collector-number slots (not repeated copies/foils or unverified name hints); denominators include all numbered paper catalog slots for the set. Search, hide-empty and collector-number drilldown are now part of the native preview. **This is not an in-place Set Progress repair for the shipping app.** User-device and data parity acceptance is still needed.

## Earlier slice: AVN-017

The next native prototype can display an explicitly exported **schema-2 full backup** in a read-only Physical/Arena collection window. This is **not** a live collection reader, migration, restore or verified printing credit. Printed IDs in the export are marked **supplied, not verified**; missing/invalid IDs remain unresolved. Deck and artwork **counts only** are shown.

The prior EDH Lab preview remains in source control, but its launcher is hidden from the Windows prototype to reinforce the scope freeze. No installed-app files are modified by this work.

This document is a project priority decision; successful CI does not establish the Windows re-entry gate has passed.
