# Arcane Vault Native — Final Windows 1.0 acceptance

**Candidate build:** `native-v0.9.3` (Windows x64, self-contained ZIP). This document and `Tools/NativeWindowsAcceptance.ps1` travel with the release. **Not yet a production 1.0.**

## Preparation

Keep the installed original Arcane Vault WebView application, an independent full collection export, and any existing recovery files. Do this on a private, backed-up Windows user profile. Do not run **Start Fresh** against the only copy of your collection.

Extract the release ZIP to a new folder, and verify its `.sha256` against the separate GitHub release checksum before running executables or scripts. Keep both the native app and PowerShell **closed** while capturing fingerprints, except for the PowerShell terminal executing the script.

## Before update: capture a local baseline

Close all `ArcaneVault.Next.Desktop.exe` processes. Open PowerShell in the extracted release directory. Replace the destination below with any filename in Documents that does not yet exist:

```powershell
$baseline = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'arcane-native-before.json'
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tools\NativeWindowsAcceptance.ps1 -Mode Capture -ReportPath $baseline
```

This script reads only `.sqlite` files for the active native workspace, catalog and rollback copies under `%LOCALAPPDATA%\ArcaneVaultNext`. It computes file names, byte lengths, and SHA-256; it does not read card names, open or modify SQLite, or write into the collection directory. It refuses to run while the native app is open, or if the baseline would be stored inside the collection directory. Existing baseline files are not overwritten.

## After update: compare the native data

From your existing 0.9.2 app, choose **Check Native Updates**, accept 0.9.3, close the old process, and launch the new one. Close the native app again before comparing:

```powershell
$baseline = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'arcane-native-before.json'
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tools\NativeWindowsAcceptance.ps1 -Mode Compare -BaselinePath $baseline
```

`PASS` means the tracked native workspace/catalog/recovery SQLite files have identical names, sizes and SHA-256 digests. A failure means a tracked file changed; investigate whether a legitimate write, SQLite housekeeping, or a defect occurred. It **does not automatically prove data loss**. This is a precise byte integrity check, not a semantic collection comparison.

## Mandatory human acceptance steps

| Gate | Evidence to record |
| --- | --- |
| Startup | Native application launches offline with no developer tools or separately installed .NET, reopens after closing, preserves data |
| Migration | On disposable profile, compare actual old-app ownership and full native import against preserved full backup; no guessed Scryfall printing IDs |
| Physical collection | Exact UUID, set, number, finish and quantity for representative cards; unresolved printings remain explicitly unresolved |
| Arena + decks | Arena totals and independent physical totals unchanged; saved deck count, entries, revision history and cover artwork preserved |
| Excel | Import `.xlsx` on disposable collection, compare additive counts/verified and unresolved rows; undo from native recovery |
| Scryfall | Initial sync, retry/cancel and card details/set progress; no unexpected owned-card changes |
| Recovery | Physical edit, Arena edit, deck edit, typed-confirmed fresh start then successful full restore on disposable workspace |
| Update | 0.9.2 → 0.9.3 version shown, two-step confirmation, old executable retained, new process waits for exit, fingerprints before/after |
| UX + performance | 100%/125%/150% DPI, large catalog scrolling, startup time, catalog download duration, search p50/p95, crash/hang observations |

Record outcomes and redacted diagnostics in [Windows 1.0 gate #51](https://github.com/thejohnfavaro/arcanevault/issues/51). **Never upload personal collection backups, native databases, or local file contents** to a public issue.

## Go/no-go

Do not close the Windows 1.0 issue or publish a production `native-v1.0.0` until the above device checks are explicitly confirmed and all P0 defects are resolved. If a blocker is found, fix it in a new versioned prerelease. The installed legacy Windows app remains independently usable. Android feature expansion and EDH Lab remain outside this gate.
