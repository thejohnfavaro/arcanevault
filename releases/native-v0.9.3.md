# Arcane Vault Native Windows 0.9.3 — 1.0 Acceptance Candidate

This is the final **Windows 1.0 acceptance candidate**, not a certified 1.0 release. The original Windows WebView app remains available and is not replaced. Use this build to complete the real-PC data integrity, migration, recovery and performance checks defined in issue #51.

## Added
- A packaged, **read-only local Windows acceptance fingerprint tool**: `Tools/NativeWindowsAcceptance.ps1`. It takes a local before/after SHA-256 fingerprint of native SQLite workspace, catalog and recovery files while Arcane Vault Native is closed, without recording individual card names, importing decks, modifying data or uploading anything.
- A repeatable Windows release-gate walkthrough that pairs byte-integrity diagnostics with actual card, set, deck, recovery and performance checks.

## Changed
- Native preview publishing refuses to silently reuse an existing `native-vX.Y.Z` release from a **different source commit**. New source changes require a new version.
- The acceptance script is distributed inside the verified Windows x64 ZIP and tested in CI against synthetic data, including unchanged files and intentional mismatch.

## Fixed
- Release engineering now catches a previously invisible failure mode: a future edited source commit cannot claim an already-published version without producing new files.

## Removed
- No features removed. Android and EDH Lab remain deferred until Windows 1.0 device acceptance.

## How to validate before approving 1.0
1. Export a **separate full native collection backup** and keep your existing working Windows application installed. Extract this preview into its own folder.
2. Close Arcane Vault Native and run `Tools/NativeWindowsAcceptance.ps1` in `Capture` mode to save a fingerprint OUTSIDE `%LOCALAPPDATA%\ArcaneVaultNext`.
3. Test a versioned upgrade into 0.9.3, then close the native app and run `Compare` against the baseline. A PASS proves only that tracked SQLite files have not changed byte-for-byte, not that the full functional/UI test has passed.
4. Manually compare physical print IDs, quantities, foil finishes, unresolved rows, Arena ownership, deck revision counts, preferred artwork, Set Progress and catalog behavior against your backup. Test an Excel import and then restore from recovery on a disposable native profile.
5. Exercise fresh Scryfall sync/cancel, standalone launch, 100/125/150% scaling, search latency, failure paths and real-data migration on Windows. Record redacted results in issue #51.

## Limitations
- The acceptance report contains **file names, byte counts and hashes** but no individual collection rows. Do not publish your real collection, backup, or other private data.
- An intentional native edit or SQLite maintenance may alter file hashes and needs investigation, not an automatic conclusion that collection data was lost.
- Still an unsigned self-contained preview, **not** a production 1.0 tag or signed installer. No device tests have been performed by the CI runner.
