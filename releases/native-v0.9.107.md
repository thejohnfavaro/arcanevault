# Arcane Vault Windows v0.9.107 — Installed App & Stable Taskbar Launcher

## Added
- One-time, per-user Windows installer (ArcaneVaultSetup-win-x64.exe) with a permanent ArcaneVault.exe launcher and Windows Apps & Features uninstall entry.
- Start Menu shortcut and optional desktop shortcut, with a stable AppUserModelID for pinning Arcane Vault to the Windows taskbar.
- A small launcher activation record: once a newer verified native window loads successfully, the same permanent shortcut starts the newer build.
- Release pipeline builds and checksums the unsigned installer alongside the existing native updater ZIP and SHA-256 sidecar.

## Changed
- The native app keeps versioned application binaries in %LOCALAPPDATA%\\ArcaneVaultNativeApps\\versions but uses the permanent launcher in %LOCALAPPDATA%\\ArcaneVaultNativeApps\\ArcaneVault.exe for ordinary launches.
- Previously established in-app update confirmation and validated download/restart remain unchanged. No more manual EXE replacement after the first installer use.
- Running native Windows windows use the installed shortcut's explicit application identity.

## Fixed
- Existing taskbar shortcuts pointing directly at versioned executable paths could become stale after a native update.
- An unsuccessful upgrade cannot change the pinned launch target: only a verified loaded window advances it.

## Removed
- No legacy collection, deck, proxy, catalog, or recovery features removed. No automatic uninstall/migration of the old WebView edition.

## Safety
- Existing per-user collection and catalog data remains in %LOCALAPPDATA%\\ArcaneVaultNext, outside the install/uninstall directory.
- The launcher accepts only version-shaped folders with a matching native-version.json (including source SHA and channel), a real executable and no symlinked directories.
- The installed active version does not move backwards when an older build is launched manually. Update activation is ignored in portable mode.
- Existing ZIP SHA-256 verification, side-by-side upgrades, previous-version rollback retention and explicit in-app update consent remain intact.
- Initial preview installer is unsigned. On-device installation, pinning, two-version update and uninstall-preserves-data checks remain required before production certification.
