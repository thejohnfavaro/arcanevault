# Arcane Vault Native Windows v0.9.98 — one-click sync snapshot

## Added
- **Save Sync Snapshot (One Click)** in Windows **Data & Sync**. Immediately writes a timestamped, uniquely named peer-format snapshot into the app's local sync-snapshots directory without opening a file picker.

## Changed
- Saved snapshots preserve existing files and report the saved path for recovery/reference.
- Native release version advanced to v0.9.98 so the in-app **Check Native Updates** workflow can discover this build.

## Fixed
- Preparing a local pre-sync snapshot no longer requires manually exporting a peer file and selecting a save location.

## Removed
- No existing functionality removed.

## Safety
- Snapshot export checks the existing native workspace, writes a new file, flushes it, and verifies the saved byte digest.
- The active collection is not changed. A local snapshot alone does not establish that Android and Windows last had identical records; verified shared-peer baselines remain separate.
- Do not treat an unverified local snapshot as proof of a common three-way sync ancestor.
