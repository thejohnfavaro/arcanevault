# Arcane Vault Native Windows v0.9.96 — Approved peer write-back

## Added
- **Review & Apply Peer Changes** in Windows Data & Sync. Select an incoming peer snapshot and the older snapshot both devices last shared; preview a candidate, then explicitly confirm applying it.
- Non-destructive three-way reconciliation for Arena quantities, exact physical printing records, saved deck IDs and preferred artwork.
- Windows native optimistic SHA checking, staged verified SQLite workspace, and retained rollback file for every approved peer merge.
- Tests covering incoming additions and quantity changes, unchanged local edits, stale approval rejection, conflicts, ambiguous deletions, and recovery preservation.

## Changed
- Incoming-only records are incorporated while locally changed records remain unchanged.
- JSON field ordering is normalized for cross-platform comparison; collection provenance fields do not masquerade as user-edit conflicts.

## Fixed
- An incoming file with records missing is never silently interpreted as an intentional deletion without a tombstone.
- Concurrent modifications are rejected rather than overwriting either user's work.

## Removed
- No existing transfer or review features removed.

## Safety
- This version **applies approved peer changes to Windows only**. It does not silently update the Android device or automatically advance a paired baseline.
- This first stage still requires the user to select a genuine common baseline manually; device pairing, authenticated checkpoint exchange, and true automatic sync remain future work.
- Close other editing windows before applying; existing backup and rollback systems remain available. Files/transfers contain potentially sensitive local collection data.
