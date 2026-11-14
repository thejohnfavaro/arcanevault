# Arcane Vault Native Windows v0.9.93 — Update recovery verification

## Added
- Native regression coverage using a real synthetic SQLite workspace and rollback file across a successful later-version installation and a rejected malformed update.

## Changed
- Update verification now explicitly checks physical, Arena, deck/revision totals, SHA-256 hashes, and recovery history after both outcomes.

## Fixed
- No runtime behavior changed. These tests guard against future changes to the updater that might overwrite user inventory or rollback files.

## Removed
- No functionality removed.

## Safety
- Synthetic offline fixture only; never touches a real user profile or collection.
- Ship only after .NET 10 native regression and Windows x64 packaging gates pass; Windows device acceptance remains required for 1.0.
