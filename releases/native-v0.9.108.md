# Arcane Vault Windows v0.9.108 — Choose Installation Folder & Uninstall

## Added
- The one-time Windows installer shows the normal installation-directory selection page, allowing a custom application location or drive.
- Explicit **Arcane Vault** entry in Windows Settings → Apps → Installed apps, with a standard Uninstall action, publisher and application icon.
- In-app updates resolve the verified running installation's versions directory instead of assuming the default local AppData location.

## Changed
- The installer remembers an existing application directory when reinstalling; fresh installs default to the current per-user ArcaneVaultNativeApps directory.
- Installed-app upgrade activation and previous-version retention follow the chosen installation folder; the pinned Start Menu / taskbar launcher path stays stable.
- Portable/unmanaged builds retain the old default staging behavior.

## Fixed
- Prior installer always hid the directory selection page.
- Prior in-app updates could stage new native versions under default AppData even when the app itself was installed elsewhere.

## Removed
- No collections, decks, catalog records, proxies, backups, or user preferences are removed by application uninstall.
- No existing native updater protections or rollback controls removed.

## Safety
- Uninstall affects only app binaries, application shortcuts and installed-program registration; private collection data remains in its separate user profile directory.
- A custom installed update destination is used only when the current executable and native-version manifest match the installed launcher layout.
- ZIP verification, handoff, and version-retention safety controls remain in place; on-device custom-directory install, update, pin and uninstall tests are still recommended.
- Windows installer preview remains unsigned.
