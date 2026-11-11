# AVN-030 — Export native full backup

## Purpose

Users can save a **complete, portable schema-2 full JSON backup of the active native collection** without opening the original Arcane Vault Windows application. This file includes native edits that were made after importing an older backup or starting a fresh collection.

## Workflow

1. Launch the standalone Arcane Vault Next Windows executable and open or create your native collection.
2. Select **Export native full backup** on the main window.
3. Choose a **new** filename under a private backup folder and confirm that the full backup is unencrypted.
4. The app validates the current `%LOCALAPPDATA%\\ArcaneVaultNext\\native-workspace.sqlite` (SQLite integrity, preserved source SHA, typed physical/Arena rows, deck/revision and art counts), writes the *exact current* schema-2 JSON to a sibling temporary file, flushes it, verifies its SHA and moves it into place **without overwrite**.
5. To prove portability, use the original full JSON with **Import backup once** into a separate fresh native profile (do not replace an existing live native workspace). The backed-up printing IDs, physical/Arena quantities, deck revisions and artwork keys must match.

## Safety

- No automatic deletion, overwrite or rename of existing user backup files. Filename collisions abort without touching the old file.
- Export refuses an unexpected extension, stale native workspace SHA, corrupt source, canceled operation or a destination inside the old installed application's profile. Temporary sibling files are removed on failure where possible.
- Native SQLite, Scryfall catalog, rollback history and installed legacy WebView profile are not modified by an export. All full source JSON fields, including unknown prior settings, are included in the export; no derived or guessed printing information is invented.
- The exported full backup is unencrypted. Store it with the same care as collection metadata, deck lists and preferences.
- This is not automatic cloud backup, bidirectional sync, archive rotation or in-place active-workspace restore. Those are separate features.

## Synthetic regression gate

Create native dataset containing multiple physical printings, Arena cards, a deck with history, artwork and unknown preferences; make native Arena/deck edits; export current JSON; compare SHA and all counts and unknown fields; re-import into independent native SQLite and compare; reject existing backup path, invalid extension, canceled export and stale source SHA; verify all source/destination bytes and timestamps unchanged for failures.

## Remaining 1.0 gate

Complete real user Windows device export/import/recovery drill; versioned installer/updater release, performance acceptance and ownership/format legality validation.
