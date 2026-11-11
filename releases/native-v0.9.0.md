# Arcane Vault Native Windows 0.9.0 (Preview)

This is a **native Windows preview** for acceptance testing, **not Windows 1.0** and not an automatic replacement for the existing Arcane Vault WebView application.

## Added
- A standalone .NET 10 Windows application with the WebView-inspired native UI, physical/Arena inventories, saved decks, Set Progress, native Scryfall catalog, and private full backups.
- **Check Native Updates** in the Windows sidebar. Download a versioned GitHub preview release only after confirmation; verify SHA-256 and bundled source/version manifest before staging in a new directory.
- Side-by-side native app versions in `%LOCALAPPDATA%\ArcaneVaultNativeApps\versions\`. The old executable remains present for rollback.
- Wait for the old native process to exit before the new one loads its collection; no concurrent update handoff.

## Changed
- Native preview executable version is now explicitly `0.9.0`; future native releases require a version increment and their own release notes.
- The native app's private data remains under `%LOCALAPPDATA%\ArcaneVaultNext\`, separate from all executable version folders.

## Fixed
- Regression coverage for invalid release metadata, corrupt checksum, ZIP path traversal, duplicate/previous versions, wrong version manifests, and no changes to private native data.

## Removed
- None. The legacy WebView Windows application, its releases and updater are not altered.

## Limitations
- This release does **not** certify migration, ownership survival across two different public native releases, signing/installer trust, or Windows real-device performance. On-device acceptance and at least one subsequent preview version upgrade are still required before a 1.0 release.
- The checksum provides download integrity against the asset published on GitHub; this preview is not yet code-signed.
- Do not delete existing native collection and recovery files. Do not install over your existing app folder. The updater never resets those files, but an external full backup is still recommended.
