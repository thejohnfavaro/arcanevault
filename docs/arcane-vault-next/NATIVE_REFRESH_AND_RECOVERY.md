# AVN-022 — Native Windows refresh and recovery contract

The old Windows application and `%LOCALAPPDATA%\\ArcaneVault\\WebView2` remain the untouched source for opt-in refresh. The native application only writes under `%LOCALAPPDATA%\\ArcaneVaultNext`.

### User flow

1. Close legacy Arcane Vault. Launch the self-contained Arcane Vault Next executable and open your native collection.
2. Choose **Inspect / refresh installed data**. The app reads an isolated copy of legacy WebView localStorage. The old app and its catalog remain untouched.
3. Compare physical/Arena copies, distinct rows, decks/revisions, artwork and unverified printing counts. A full payload hash excluding only volatile `exported` and `source` fields detects changes to unknown preferences and deck/art contents.
4. If data differs, explicitly approve **replace**, not merge. **Do not approve** if the legacy source appears stale or unexpectedly empty.
5. The new SQLite file is built separately, checked against the source, and atomically published with Windows `File.Replace`. The prior verified native file becomes `native-workspace.rollback-YYYYMMDD-HHMMSS-<id>.sqlite`. After the operation, both versions must reopen and verify.
6. To undo, choose **Restore native recovery**, select a listed recovery file and confirm after comparing counts. This also retains the displaced newer native version under a new rollback name.

### Recovery invariants

- No replace without comparing to a verified native workspace SHA. A changed or tampered current database fails closed.
- A no-op when just the generated export timestamp changed; no extra rollback.
- Native source's full unknown JSON fields, all physical printings and finishes, Arena rows, deck history, artwork and formatting stay in the preserved manifest and typed tables.
- Automatic rollback filenames are unique and never overwritten. Failed staging is removed; existing rollback files are never deleted by the app.
- Restore accepts only a known rollback located alongside the active native workspace. It checks SQLite integrity and exact row/payload parity before replacing the current version.
- **Post-swap verification failure:** do not attempt to auto-clear or rebuild. Preserve both files and inspect the current workspace/recovery files. The original legacy profile is never modified.
- These files are not encrypted and can be large. Keep adequate free disk space and do not open two independent native app instances during a refresh.
- This is intentionally a **collection replacement**, not incremental merge or bidirectional synchronization. Once editing is introduced natively, it will require separate conflict prevention and edit journaling.

### Synthetic CI

Regress timestamp-only no-op, strict per-row inventory and deck count diff, missing/malformed backups, wrong version guard, verified refresh (physical/Arena/deck copies), recovery creation, invalid and corrupted recovery rejection, version re-restore, preservation of older and newer rollback files, and failed-operation source integrity. **CI is not user-machine validation.**

**Still not shipped:** native collection edits, legacy app removal, signed standalone installer, automated sync, scanner, EDH Lab, Android changes.
