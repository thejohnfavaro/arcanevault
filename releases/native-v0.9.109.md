# Arcane Vault Windows v0.9.109 — Clean Install Folder

## Added
- A conventional default install folder named `ArcaneVault`: `%LOCALAPPDATA%\Programs\ArcaneVault`.

## Changed
- The installer uses the selected destination as **the application root**. For example, choosing `D:\Games\ArcaneVault` installs `ArcaneVault.exe` directly there, rather than creating an `AppData\Local\ArcaneVaultNativeApps` path beneath it.
- Start Menu, taskbar and uninstall shortcuts continue targeting the permanent executable in that root.
- Existing installations retain their previous selected location during upgrades unless the user explicitly changes it.

## Fixed
- New installations no longer default to an unintuitive `ArcaneVaultNativeApps` folder.
- The directory picker represents the actual application destination rather than an internal version-staging location.

## Removed
- The old `ArcaneVaultNativeApps` **default name** for fresh installer destinations. Existing installs and their data are not removed or silently moved.

## Safety
- A managed `versions\vX.Y.Z` directory remains immediately beneath the chosen Arcane Vault root for safe in-app updates and rollback.
- Collections, card catalogs, proxies, decks and backups still live separately in `%LOCALAPPDATA%\ArcaneVaultNext`, never inside the installation folder.
- Custom-location update routing, uninstall registration and checksum verification from v0.9.108 remain unchanged.
- Installed-app relocation should be tested locally using the installer; no automatic relocation of existing installations is attempted.
