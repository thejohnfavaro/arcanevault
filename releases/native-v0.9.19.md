# Arcane Vault Native Windows v0.9.19 (Preview)

Corrected hotfix for the v0.9.18 native installation failure.

## Added
- Regression coverage for update archives containing UTF-8 byte-order marks from Windows PowerShell 5.1.
- Local release package validation that rejects newly written BOM-prefixed manifests before upload.

## Changed
- Local Windows packaging emits UTF-8 **without a BOM** for `native-version.json`, including when run under Windows PowerShell 5.1.
- Native updater supports both standard BOM-free UTF-8 JSON and legacy UTF-8-BOM manifests without relaxing the release version, source SHA, official host, archive digest, or path safety checks.
- Includes the fixed-height, full-bleed Set Progress artwork cropping originally introduced in v0.9.18.

## Fixed
- **"0xEF is an invalid start of a value"** during native update verification when installing a package created through the local Windows release script.
- Retains the original executable and physical/Arena collection data whenever update verification fails.

## Removed
- PowerShell's implicit BOM-writing path for locally packaged native release manifests.

## Installation and validation
- v0.9.18 was published with a BOM-prefixed manifest and cannot be safely modified in place. Publish this fix as a **new version, native-v0.9.19**.
- Existing v0.9.17 users should select v0.9.19 directly in the native updater. The corrupted v0.9.18 package is not required for this upgrade.
- Test local build with `tools/PublishNativeWindowsLocal.ps1 -PackageOnly`, then publish only after tests and extracted ZIP verification pass.
- This is a preview, not version 1.0; no collection migration or destructive data action.
