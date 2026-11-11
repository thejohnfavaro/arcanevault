# Arcane Vault Native Windows 0.9.1 (Preview)

This unsigned prerelease is the **second versioned native Windows candidate**. Install v0.9.0 first, then use its **Check Native Updates** control to verify the 0.9.0 → 0.9.1 upgrade path on Windows. Neither build is certified 1.0.

## Added
- Versioned 0.9.1 build to exercise a real update from the published native 0.9.0 preview.
- Regression tests for safe repeated staging of an identical version and rejection of tampered existing application files.

## Changed
- Native update discovery scans multiple GitHub release pages. The older `windows-v*` release history can no longer hide a newer native preview merely because it falls outside the latest 30 releases.
- A fully identical, checksum-verified version already staged under `%LOCALAPPDATA%\ArcaneVaultNativeApps\versions\` may be reused on a second update attempt, without overwriting any files.

## Fixed
- Repeated native update check no longer fails solely because the user declined to launch a correctly staged version. Mismatched files still fail closed.

## Removed
- None. Legacy Windows app, data and updater remain unchanged.

## Test instructions
- Obtain v0.9.0 from its GitHub prerelease page, extract and launch it. Preserve an independent full native collection backup and record exact collection/deck/artwork totals.
- Click **Check Native Updates**, confirm it offers 0.9.1, and choose **Install**. Confirm it downloads the official release, verifies checksum/manifest, and stages it side-by-side.
- Decline the first **Launch** prompt; retry from v0.9.0 and confirm the identical staged 0.9.1 can be reused rather than overwritten. Then launch 0.9.1.
- Confirm v0.9.0 closes before 0.9.1 opens the unchanged collection. Verify deck revisions, physical printing IDs and quantities, Arena counts, preferences, catalog, and recovery files. Check restart and export/restore using a disposable profile.

## Limitations
- GitHub CI validates synthetic upgrade safety; this version still requires real-PC test evidence and an external full collection backup before Windows 1.0.
- Release checksums are not Authenticode code signatures. A future signed installer, update rollback UX, schema compatibility, and Windows scaling/performance checks remain open.
