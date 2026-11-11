# AVN-034 — Side-by-side native Windows updates

## Design

**Separate application binaries from private collection data.** Native WPF versions live under `%LOCALAPPDATA%\ArcaneVaultNativeApps\versions\vX.Y.Z\`. The complete native workspace, original imports, decks/revisions, artwork preferences, owned printings, independent Scryfall catalog, exports and rollback files stay in `%LOCALAPPDATA%\ArcaneVaultNext\`. The old shipping WebView app and its updater are independent and never written by native updates.

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
- This initial step is a side-by-side executable upgrade, not an MSIX installer, system-wide app replacement, downgrade data-schema migration, or automatic background updater.
- No old version folders are deleted automatically. Older executables remain available, but running an older binary after a **future incompatible data-schema upgrade** may not be safe; future data-schema migrations require explicit versioned backup and rollback support.
- No 1.0 certification without real-device two-version tests and exact no-loss checks. Android expansion and EDH Lab remain backlogged.
