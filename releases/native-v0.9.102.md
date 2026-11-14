# Arcane Vault Native Windows v0.9.102 — automatic first USB sync and shared baseline

## Added
- **First USB Sync / Create Shared Baseline** in Data & Sync, with automatic first-use routing from standard USB sync actions when Windows has no remembered peer checkpoint.
- Two-phase USB peer exchange: compare Windows and Android, synchronize only after approval, verify the resulting collection, decks and preferred artwork, and save a real matching shared checkpoint.
- Automatically merge compatible, disjoint additions on both devices, including physical and Arena records, saved decks and artwork preferences.
- When a record differs on both devices, show a clear source choice: keep Windows or keep Android, with the number and sample of conflicting records.

## Changed
- The USB connection now establishes a shared baseline after the first matching or successfully reconciled sync; later USB transfers use the existing differential three-way merge.
- An Android-authoritative first pairing writes through the existing native workspace backup/rollback mechanism.
- An automatic union only merges identical overlaps and uniquely present records; ambiguous edits are never overwritten silently.

## Fixed
- No manual copying of a saved baseline file is needed after first USB pairing succeeds.
- First sync no longer requires a previously created baseline when the devices are new to each other.

## Removed
- No Wi-Fi, USB, manual-baseline, snapshot or recovery features removed.

## Safety
- Existing native rollback and Android recovery snapshots remain intact.
- A local record missing on the other device is preserved as an addition in baseline-less union; deletions or conflicting values need explicit user review.
- Both devices save peer checkpoints only after a second transfer verifies identical recognized records. Cable round-trip testing is required to validate the workflow end to end.
