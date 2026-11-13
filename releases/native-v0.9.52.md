# Arcane Vault Native Windows v0.9.52 — Backup Readiness Diagnostics

This native preview advances the Windows 1.0 release-readiness checklist by making full-backup integrity inspection accessible from the app. It does not perform migration, import, recovery or restore.

## Added

- **Options → Check backup readiness…**, opening a dedicated, scrollable window with clear read-only warnings.
- Explicit **Choose full backup to check…** picker for a previously exported schema-2 JSON full backup. The check runs off the UI thread; no active collection, catalog, database or application profile is modified.
- Reports exact physical copies, physical printing rows and distinct names; Arena copies and rows; deck and deck revision counts; preferred artwork entries; and unresolved/missing printing ID copy totals.
- Computes and displays a streaming SHA-256 hash and file size, suitable for manually comparing the same exported file before and after copying it.
- **Copy report** includes only aggregate counts and hash—no card names or user file path—so results can be pasted into a private bug report.
- Checks that the export's size and last-write time stay constant during inspection. Changes or invalid schema fail closed.
- Regression coverage for parsed counts, checksum, unchanged source hash and timestamp, malformed exports, and read-only window bindings.

## Changed

- The Options screen includes the backup readiness entry point. Existing theme previews and Save/Cancel/Restore appearance behavior are unchanged.
- Native backup checks use the existing strict schema-2 parser and never invent printing IDs or combine individual physical printings.

## Fixed

- Backup-readiness gaps can now be identified before a 1.0 upgrade without opening a recovery flow or making a change to the active workspace.
- Invalid or non-schema-2 exports show a clear failure message instead of silently appearing verified.

## Removed

- Nothing. No existing card, collection, deck, Set Progress, EDH Lab, update, or backup/export functionality has been removed.

## Safety and scope

- **Read-only, opt-in diagnostics only.** The backup picker does not restore, edit, reimport, clean, merge, migrate or overwrite a file.
- This check confirms the *selected exported file* parses and can be hashed. It does not certify that it matches the current active workspace, is complete against the user's intended inventory, or will restore successfully.
- A syntactically valid Scryfall printing ID is not verified against Scryfall; unresolved printing counts must remain visible.
- No personal card list, backup path, or card-level data is included in the copied report.
- This remains a preview; passing a diagnostic does not by itself mark native Windows 1.0 ready.

## Local acceptance

1. Upgrade to native v0.9.52 with Native Updates, or run the portable Windows build.
2. Open **Options → Check backup readiness…**. Confirm theme settings remain unchanged when closing without saving.
3. Export a private full backup from the existing native collection workflow to a new JSON file (do not point diagnostics at the active SQLite workspace).
4. In the readiness window, choose that exported schema-2 JSON file. Check Arena/physical counts, deck/revisions, preferred artwork and unresolved ID copy totals.
5. Copy the report to a private text file if desired; verify it contains aggregate counts and SHA-256 but no card names or machine file paths.
6. Run the checker twice against the same unchanged export and confirm identical hashes and totals.
7. Select an invalid, truncated, or older-format transfer JSON. It must show an error without modifying the source or active collection.
8. Return to Card Catalog, EDH Lab, Deck Library and Set Progress, confirming the existing data and navigation work as before.

Release gates: Windows/native regression CI, checksummed portable ZIP, versioned release verification, then on-device acceptance before promoting 1.0.
