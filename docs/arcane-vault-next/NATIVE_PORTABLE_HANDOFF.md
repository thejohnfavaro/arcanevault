# AVN-032 — Windows native portable test-build handoff

This is a **standalone, self-contained Windows x64 test build**, not a Windows 1.0 release or a replacement for the installed legacy Arcane Vault.

## Download and verify

1. Open [Arcane Vault Next native workflow](https://github.com/thejohnfavaro/arcanevault/actions/workflows/arcane-next-native.yml) and choose the latest **successful run** of the intended commit on `main`. Its tests must be green.
2. Under **Artifacts**, download **ArcaneVaultNext-NativePortable-TestBuild**. Extract the downloaded artifact to a private staging directory; inside it are `ArcaneVaultNext-NativePortable-win-x64.zip`, `.zip.sha256`, and `BUILD-IDENTITY.txt`.
3. Verify the outer ZIP using PowerShell from that directory:

   ```powershell
   Get-FileHash .\ArcaneVaultNext-NativePortable-win-x64.zip -Algorithm SHA256
   Get-Content .\ArcaneVaultNext-NativePortable-win-x64.zip.sha256
   ```

   The hexadecimal hash must match. `BUILD-IDENTITY.txt` gives the exact source commit and workflow run. These workflow artifacts expire after 30 days.
4. Extract `ArcaneVaultNext-NativePortable-win-x64.zip` into a **new directory**, not over any existing installed Arcane Vault folder. Run `ArcaneVault.Next.Desktop.exe`. .NET 10 desktop runtime is bundled. Windows may still show an unsigned-app warning because this is not a signed installer.
5. **Keep the original WebView-based Windows app installed** and preserve a private full collection backup. The new app maintains its independent data under `%LOCALAPPDATA%\ArcaneVaultNext`, **not** in the ZIP folder. Moving or deleting the executable does not erase that workspace; reset operations inside the app still affect it.

## Required real-Windows acceptance before 1.0

Use a disposable Windows profile or a rigorously backed-up data copy. **Do not use Start Fresh against your only live collection.** Record the original full backup and baseline Arena quantities, exact physical Scryfall IDs/set/collector/finish/quantities, decks and revisions, preferred art and unresolved printings.

- Launch the new executable without developer tools or an external .NET runtime, close it, reopen it, and ensure data persists.
- **Fresh path:** start a *new empty* native workspace, download Scryfall bulk printing data, cancel/retry, and confirm cards can be searched and opened; no cards become owned from catalog download.
- **Migration path:** with the old app closed, explicitly inspect/migrate an installed legacy Windows profile into a separate native workspace. Compare records and counts with the independent full backup. Never infer IDs for unresolved legacy printings.
- Add and edit a known verified physical printing and an Arena row. Save/duplicate/edit a deck. Force-close, relaunch and compare exact rows and deck revisions.
- Export the **current native full backup**, inspect the exported JSON from a private location, and test import into another empty disposable native workspace. Compare all exact physical rows, Arena totals, deck/revision and preferred-art counts.
- Confirm **Restore native recovery** reverses each controlled edit. Confirm **Start fresh collection** requires typed confirmation, then restore the pre-reset workspace; compare exact records.
- Check Card Details, art offline fallback, Set Progress (including unresolved not credited), scrolling a large collection, 100%/125%/150% DPI and narrow windows.
- Record startup time, search latency, Scryfall sync duration, basic CPU/memory, crashes, and any migration errors. Attach **redacted** results to [Windows 1.0 gate #51](https://github.com/thejohnfavaro/arcanevault/issues/51). Never upload private collection backups to GitHub.

## Explicit release blockers

This artifact is **not** a GitHub Release, signed installer or automatic in-app updater. Versioned native distribution and upgrade-from-older-native-build tests are still required. The existing shipping-app updater is a **separate** pipeline and cannot be assumed to update this native app. Do not close #51 or tag Windows 1.0 on synthetic CI alone. Android feature expansion and EDH Lab stay deferred.

## CI guardrails

The native workflow compiles the .NET 10 WPF app, executes its regression suite, publishes a self-contained Windows x64 single-file executable, compresses the entire publish payload into a ZIP, writes its SHA-256 digest and source/build identity, extracts the archive to check for the EXE, and uploads the package for 30 days. No deployment, legacy profile change, or destructive operation occurs during CI.
