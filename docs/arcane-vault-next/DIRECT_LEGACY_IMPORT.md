# AVN-021 — Direct migration without user export

## User workflow

1. **Close the old Arcane Vault Windows app**, including any older localhost server on port 8765.
2. Launch **Arcane Vault Next** (new native artifact). If no native workspace exists, click **Migrate installed Windows data**.
3. Confirm the read-only extraction of the legacy browser profile.
4. Review the detected physical and Arena inventories, deck/revision counts, preferred-artwork counts, and missing/invalid printing-ID copies.
5. Approve creation of the native SQLite workspace. The app optionally copies the Scryfall SQLite catalog into the separate native directory.
6. Restart Arcane Vault Next and verify that inventory, details and Set Progress open immediately from native storage.

No old app process, manual JSON export, QR transfer or USB cradle is needed for future native viewing. The **old installation remains intact** throughout.

## Isolation and data reliability

- **Origin:** the shipping desktop app's `http://127.0.0.1:8765` localStorage inside `%LOCALAPPDATA%\\ArcaneVault\\WebView2\\Default\\Local Storage\\leveldb`.
- **Source:** copied as files using **read access only**, checking each file's SHA before/after copy. Source and scratch paths cannot overlap; source LevelDB `LOCK` is not copied.
- **Read:** a minimal isolated one-shot local HTTP page on the same origin, hosted only while the temporary WebView2 instance is active. Its code reads **only the clone's** browser localStorage; the original legacy HTML is not executed. The old app must be closed because the port is exclusive.
- **Validation:** require both exact `vault_cards` and `vault_physicalCollectionV1` keys and validate JSON schema; never use bundled demo data or guess ownership. Preserve recognized decks, revisions, artwork, wildcard and other browser keys in the complete native source document. Missing/invalid card print IDs remain unresolved.
- **Commit:** build the complete snapshot in memory (no plaintext JSON export file), display aggregate preview and require confirmation. Create the independent native SQLite workspace transactionally with an atomic no-overwrite publish. Native workspace already present => no new import. Native catalog is independently copied with SQLite online backup if source catalog exists.
- **Cleanup:** destroy the temporary browser instance and attempt deletion of its isolated profile; if Windows temporarily holds files open, a scratch copy might persist under the OS temp directory and should be removed only after confirming no migration is running.
- **Security:** the migration is localhost-only and uses no network downloads. One-time bridge still requires the local Microsoft Edge WebView2 Runtime. Native SQLite files are not encrypted; keep Windows account and backups protected.

## Known limitations / validation required

The source layout and origin are based on the current shipped app code; browser profile variations, old Python builds, running old app, missing storage keys and inconsistent LevelDB files must **fail safely**. Synthetic CI tests cover structure, checksums, negative paths, and native DB persistence. They **do not** execute an actual Chromium/WebView2 profile on the user's machine. On-device approval must include exact physical/Arena/deck/art quantities, name identity, offline catalog and restart. Do not erase the old user profile or enable native editing until validated.

## Fallback

If migration is unsupported, **Data & Sync → Save full backup** in the old app, then **Import backup once** in Arcane Vault Next is still available. Never automatically revert to a partial USB/QR snapshot or the mirrored Scryfall catalog.
