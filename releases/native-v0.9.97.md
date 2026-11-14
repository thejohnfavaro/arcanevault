# Arcane Vault Native Windows v0.9.97 — QR wireless peer apply

## Added
- **Receive & Apply Phone Sync (Wi-Fi)** in Windows Data & Sync. Generates a scannable five-minute, one-use QR link.
- Once Android sends the snapshot, Windows validates it and prompts you to select the previously shared common baseline and explicitly approve a three-way merge.
- Uses the existing SHA-verified, staged native workspace replacement and retained rollback file for applied collection, deck and artwork changes.

## Changed
- After a read-only Wi-Fi comparison shows **zero record differences**, Windows can save the matching Android snapshot as a local common baseline (explicit approval required). Previous checkpoint versions are retained.
- On future wireless write-back, the previously approved baseline may be reused with confirmation instead of browsing for an older file.
- The wireless listener only receives and validates snapshot bytes. Application writes occur separately after the LAN transfer finishes and the user reviews the merge.
- Read-only wireless transfer, manual file export and existing peer comparison remain available.

## Fixed
- No longer necessary to save/copy an Android JSON file onto the PC just to use Windows' approved peer write-back.

## Removed
- No existing features removed.

## Safety
- **Not automatic pairing or automatic bidirectional sync.** User must select a genuine common snapshot as baseline.
- Wi-Fi uses one-time HTTP links on a trusted home network, not encrypted pairing. Do not use on public Wi-Fi.
- Windows applies approved changes only to its native workspace. Android is not changed by this action.
- Conflicting edits, ambiguous deletions and stale local workspace SHA continue to be rejected.
