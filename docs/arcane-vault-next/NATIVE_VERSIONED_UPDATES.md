# AVN-034 — Side-by-side native Windows updates

## Design

**Separate application binaries from private collection data.** Native WPF versions live under the selected install root, e.g. `%LOCALAPPDATA%\Programs\ArcaneVault\versions\vX.Y.Z\` for new installations or `D:\Games\ArcaneVault\versions\vX.Y.Z\` for a custom location. The complete native workspace, original imports, decks/revisions, artwork preferences, owned printings, independent Scryfall catalog, exports and rollback files stay in `%LOCALAPPDATA%\ArcaneVaultNext\`. The old shipping WebView app and its updater are independent and never written by native updates.

## One-time installed app (v0.9.107 and later)

Since v0.9.108, the installer offers the standard folder picker. Starting with v0.9.109, the default folder is `%LOCALAPPDATA%\Programs\ArcaneVault`. The chosen directory is the application root, not a parent under which the installer recreates `AppData\Local\ArcaneVaultNativeApps`. Custom drives and folders are supported. The in-app updater follows the verified installed version's own `versions` folder and will not split the installed app between drives. Windows Settings → Apps → Installed apps lists **Arcane Vault** with its Uninstall option; uninstall removes app files and registered shortcuts, not your separately stored collection.\n\nUse the **ArcaneVaultSetup-win-x64.exe** asset on the versioned `native-vX.Y.Z` GitHub release for the first installation. The unsigned preview installer installs per-user, without administrator privileges, to the chosen `ArcaneVault` folder. It registers **Arcane Vault** in Installed Apps and the Start Menu, with an optional desktop shortcut. Pin the Start Menu **Arcane Vault** entry to the taskbar, not a versioned `ArcaneVault.Next.Desktop.exe`.

The permanent `ArcaneVault.exe` launcher remains at the install root. It resolves `active-version.txt` to a versioned binary only after validating the folder name, non-reparse filesystem objects, and native-version manifest. On startup the application sets the installed shortcut's fixed `RNGesus.ArcaneVault` application identity. After a new native window has successfully loaded, it atomically advances the launch pointer; a failed startup never promotes the new version. If the pointer is missing or invalid the launcher searches for a verified installed version. Portable mode never writes this pointer.

The current **in-app** upgrade still verifies the official ZIP SHA-256, stages in `versions\vX.Y.Z`, asks before restart, and retains the previous version for recovery. This is intentionally not an in-place overwrite of a running EXE: the *visible installed app and pin are stable* while only the underlying version changes. The installer/uninstaller acts only on its dedicated binaries root; it must not delete or move `%LOCALAPPDATA%\ArcaneVaultNext` user data. Legacy WebView installation remains independent.

**Windows acceptance still required:** install the signed-or-unsigned preview explicitly; pin from Start; run app; update through the in-app prompt; confirm the same pin opens the new version; cancel an update; confirm old version remains; uninstall and verify that all collection/deck/proxy/catalog/backup data is unchanged. The unsigned preview may trigger Windows security reputation warnings.

## How native updates work

1. Open **Check Native Updates** in the native Windows left sidebar. The current version comes from the built assembly's version property; releases are fetched from `https://api.github.com/repos/thejohnfavaro/arcanevault/releases`.
2. The app considers only strictly named **`native-vMAJOR.MINOR.PATCH` GitHub prereleases** from this repository with the exact `ArcaneVaultNative-win-x64.zip` and `.sha256` browser assets. It rejects drafts, untrusted hosts, missing assets and versions not newer than the running one. It never uses the legacy `windows-v*` channel.
3. After explicit confirmation, fetch the published checksum and ZIP over HTTPS from the expected GitHub release URLs (or the controlled GitHub release-asset redirect host). The download has a size limit, is SHA-256 verified, and is rejected before installation if altered.
4. Extract to a unique staging folder under the separate app-binaries directory. Reject unsafe archive paths, symbolic links, duplicate paths, oversized payloads and incorrect `native-version.json` version/source metadata. Confirm the executable exists, then atomically rename the staging directory to a new `vX.Y.Z` folder. Existing version folders are **never overwritten**.
5. If a version is **already staged**, the updater unpacks a fresh copy of the checksum-verified release into temporary staging and compares **every relative file name and SHA-256** against the existing version. Only a completely identical directory is reused; altered or extra files cause a safe refusal and no overwrite.
6. After a second explicit prompt, launch the new EXE with the old process ID. The new process **waits until the old process exits** before opening the unchanged native workspace. If the old process refuses to exit, the new process declines to launch. Canceling either prompt leaves the existing executable and collection untouched.

## Preview release publication

The native Windows Actions pipeline now embeds a `native-version.json` file containing **version, source commit and preview channel** in the ZIP, verifies the extracted ZIP and its manifest, publishes the ordinary 30-day test artifact, and on a successful push to `main` publishes a **GitHub prerelease** only if the exact `native-v<Version>` tag does not already exist. The version is from `ArcaneVault.Next.Desktop.csproj`; matching `releases/native-v<Version>.md` release notes are required. Existing release files/tags are immutable by workflow convention. All native regression and package checks run before publication.

**Update acceptance builds:** v0.9.0 supplies the initial baseline; v0.9.1 provides an actual two-version update target, longer paginated native release discovery, and safe retry of a previously staged identical version. These CI-validated release builds do **not** prove a full in-app upgrade on the user's machine; actual use and an unchanged-collection comparison remain required.

## Windows acceptance gate for 1.0

- Keep an independent full backup and the old shipping Windows app. Record exact Arena card counts, physical set/collector/finish rows, deck history and artwork keys before updating.
- From a verified v0.9.0 preview run **Check Native Updates** after v0.9.1 is published. Verify the displayed version and both prompts; cancel one attempt and confirm no changes; then install a second time.
- Confirm the newer version launches after the old one exits, the older version remains on disk, and native workspace/rollback/catalog SHA-256 and all user record counts are unchanged **across the executable-only upgrade**.
- Force-close and restart. Test card catalog, owned inventory, deck library, set progress, export current full backup, and Restore native recovery on a disposable collection copy.
- Confirm altered ZIP/checksum, no internet, disk-full, denied permissions, already installed version, and an old process that fails to exit do not silently change the user collection.
- Measure startup and UI responsiveness on Windows and check a release at 100%, 125% and 150% scaling.

## Limitations

- Preview releases are unsigned and checksums come from the same GitHub release; this verifies downloaded bytes against publisher metadata, **not a code-signing certificate**.
- The native preview now has a per-user installer and permanent launcher, but the updater still stages side-by-side binaries rather than overwriting a running EXE. It is not an MSIX installer, system-wide deployment, downgrade data-schema migration, or automatic background updater.
- No old version folders are deleted automatically. Older executables remain available, but running an older binary after a **future incompatible data-schema upgrade** may not be safe; future data-schema migrations require explicit versioned backup and rollback support.
- No 1.0 certification without real-device two-version tests and exact no-loss checks. Android expansion and EDH Lab remain backlogged.
