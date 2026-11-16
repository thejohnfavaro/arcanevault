# Arcane Vault Native Windows v0.9.126

## Added
- Native Arena Player.log match tracking with persistent, deduplicated match IDs in `%LOCALAPPDATA%\ArcaneVaultNext\match-history.sqlite3`.
- Stream Deck per-deck confirmed wins/losses, recent match, and uniquely identified Arena-selected deck.
- Read-only historical match counting from a prior Arcane Vault match-history database, with match-ID deduplication across native and historical records.

## Changed
- Native Stream Deck Companion exporter writes deck data and artwork in the existing Alpha 0.1 snapshot format at `%LOCALAPPDATA%\ArcaneVault\streamdeck.json`.
- Native app is the active integration; the retired WinForms application is not needed.
- The native collection and artwork are read using existing verified APIs; no deck or inventory migration is performed.

## Fixed
- Arena tracking source compilation requires explicit System.IO imports.
- Previously exported native Stream Deck snapshots could lack ongoing match tracking.
- Historical and new confirmed Arena matches now combine without double counting the same match ID.

## Removed
- No native features removed.

The Stream Deck export and historical match reads are read-only. Native MTG Arena match tracking writes only the new native match-history database, not inventory/decks. It requires MTG Arena Detailed Logs; unknown results and unidentifiable decks remain uncounted. Collection value remains based on the latest native valuation history, and artwork appears incrementally after the initial snapshot.
