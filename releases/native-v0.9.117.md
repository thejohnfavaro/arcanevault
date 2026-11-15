# Arcane Vault Native Windows v0.9.117 — Keyword Search and Updated Native Build

## Added
- A keyword and rules-text autocomplete field in Card Catalog, with mechanic suggestions, Space/Enter acceptance, and removable keyword chips.
- Token selections feed the existing comma-delimited Oracle filtering, retaining Match All and Match Any search.

## Changed
- A newly versioned native Windows update includes the latest merged catalog and collection workspace changes.
- The native installer, update archive, version manifest and SHA-256 checksums are published under one unique versioned tag to make in-app update discovery work.

## Fixed
- Native Windows changes were previously available only in successful workflow builds while the in-app updater still saw the older v0.9.116 release tag.
- Keyword entry no longer requires manually separating every recognized mechanic by comma.

## Removed
- No existing collections, decks, sync state or user preferences were removed.

## Safety
- Collection storage and synced inventory remain unchanged; the keyword UI is a read-only query filter.
- The release workflow must pass compilation and package verification before publishing its native installer and updater archive.
