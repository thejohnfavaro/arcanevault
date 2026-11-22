# Arcane Vault Windows v0.9.204 (Preview)

## Added
- A native regression guard requiring startup collection/deck/shortlist projections to reuse an already verified workspace.

## Changed
- Initial native collection loading runs the full SQLite integrity, preserved-backup SHA, and exact row-projection verification once instead of repeating it for saved decks and the proxy ledger.
- Catalog startup ownership loading reuses its already-verified workspace to read proxies and the shortlist. The presentation helpers still independently hash the preserved source backup and reject changes during reads.

## Fixed
- Long Windows startup delays caused by repeated full verification passes through the same native SQLite workspace, especially for large saved Arena/physical collections.

## Removed
- Redundant nested full-workspace opens during the native collection display path and initial catalog ownership/shortlist loading.

## Verification
- Run Windows native CI and `native-visual-shell`, `native-workspace-safety`, `native-deck-library-cache` and `native-shortlist` regression groups.
- Time launch and first collection-open against v0.9.203 with an unchanged local workspace.
- Validate that a corrupted native workspace or a changed verified backup still fails closed without overwriting collection data.
- Set Progress full rebuild may still be necessary on a true cache miss or catalog update; this change does not bypass it.
