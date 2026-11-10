# Arcane Vault Next — Windows core recovery prototype (read-only)

This is a **separate experimental WPF/.NET 10 application**, not a replacement for the shipping Arcane Vault executable. The shipping Windows app and its framework/version are unchanged.

## Safe native collection refresh and recovery — AVN-022

**For Windows users who have already migrated:** The native app now supports an explicit **read / compare / replace with rollback** workflow. Close the old Arcane Vault app, select **Inspect / refresh installed data**, and review the detailed physical, Arena, deck/revision, artwork and unresolved-printing counts. If the full preserved source has no meaningful changes (ignoring the export timestamp), nothing is written. If it differs, choose whether to **replace (not merge)** the native collection. Any existing native collection is first preserved as a new `native-workspace.rollback-<timestamp>-<id>.sqlite` recovery file alongside `native-workspace.sqlite`.

The refresh stages a complete verified replacement in the private native folder, rechecks that the existing native database has not changed since the comparison, atomically swaps it while preserving the old SQLite file, and reopens the result. The original installed-app profile is never written. Use **Restore native recovery** to explicitly choose a prior native rollback and see its counts before restoring; the replaced newer native version is also retained as a *new* recovery point. **Never manually delete or rename these recovery files while the app is open.**

**Limits:** There is no automatic sync and no merging of changes. Refreshing from the old app will replace any native-only data made after that source snapshot, once native editing is introduced. Legacy data and current native data can diverge; carefully compare the on-screen counts. Rollback copies are unencrypted and consume disk space; retain them until recovery has been verified. Editing remains disabled while source-parity and on-device rollback tests continue.

## Self-contained build and safe comparison

GitHub Actions now offers **`ArcaneVaultNext-NativeStandalone`**, a Windows x64 bundle that includes the .NET 10 Desktop runtime (extract the entire artifact and run `ArcaneVault.Next.Desktop.exe`). The one-time legacy profile bridge still needs the local Microsoft Edge **WebView2 Runtime**; normal native collection/catalog viewing does not. This artifact is a development preview, not a signed installer.

The **Inspect / migrate installed data** button is safe to use even if you have already migrated using a manual backup. If `native-workspace.sqlite` exists, it first reads a clone of the installed profile **without writing**, then compares the data and offers an explicit refresh **with an automatic recovery copy** if differences exist. It never merges data or modifies the installed app. You may decline with no changes. This allows checking the importer without deleting prior native data. New users without a native workspace get the migration confirmation flow described below.

## Direct migration from installed Windows app — AVN-021

**Preferred for first-time native setup:** Open the new native EXE and select **Migrate installed Windows data**. **No manual backup export or old app running is required.** The old Arcane Vault Windows program **must be CLOSED** during migration; its localhost port 8765 must be free. The one-time extraction still uses the installed Microsoft Edge **WebView2 Runtime**, but Arcane Vault Next's collection and catalog afterwards use native SQLite.

The importer clones **only** the old WebView2 localStorage LevelDB to a private temporary browser profile, serves a minimal local-only migration page at the same 127.0.0.1:8765 origin, and reads the copied localStorage. It does **not** execute the original HTML application or mutate the original profile. The browser clone is deleted after the attempt. Required `vault_cards` and `vault_physicalCollectionV1` keys must be present: no source means **stop**, not import the bundled demo collection or silently count zero owned cards.

The native app previews Arena and paper quantities, decks/history, preferred art and unresolved printing-ID copies; confirm totals before it creates a **new** independent `%LOCALAPPDATA%\\ArcaneVaultNext\\native-workspace.sqlite`. The full migrated document is assembled **in memory** and stored transactionally with checksum and raw extra browser settings. The native Scryfall catalog is copied independently if available. Subsequent launches open the native data directly without the old application, manual file export, or migration bridge.

**Limits:** This is a **one-time** migration, not automatic synchronization. It currently supports the installed **127.0.0.1:8765 / Default profile** origin; older/different browser profiles or absent ownership keys intentionally fail without altering data. If it fails, use the existing **Import backup once** action. An existing native workspace is **never silently overwritten**. A verified refresh is now opt-in with a retained recovery copy. Native editing, automated sync, signed Windows releases and eliminating WebView2 from legacy profile inspection remain future tasks. Do not delete the installed old app or backup yet; device-level validation is still required.

## One-time native setup — AVN-020

This is the first Windows slice that **reopens your own native collection and local Scryfall catalog on subsequent launches**. AVN-021 adds direct one-click migration, so the manual full-backup import described below is now the **fallback**, not the normal route. It remains a **separate experimental application**, not an installed-app replacement or a bidirectional sync.

1. Open your installed (old) Arcane Vault app, use **Data & Sync → Save full backup**, and keep the downloaded `arcane-vault-backup.json` safe.
2. Launch `ArcaneVault.Next.Desktop.exe`. Select the existing `%LOCALAPPDATA%\\ArcaneVault\\scryfall-catalog.sqlite` with **Open a read-only catalog snapshot**.
3. Click **Keep catalog natively**. Arcane Vault Next makes its own transactional, read-only-verified copy into `%LOCALAPPDATA%\\ArcaneVaultNext\\native-catalog.sqlite`. If a native copy already exists, it will not overwrite it.
4. Click **Import backup once**, select the complete JSON export and approve creation of `%LOCALAPPDATA%\\ArcaneVaultNext\\native-workspace.sqlite`. This is a private, independently staged SQLite database with physical/Arena inventory, the full preserved backup, decks/revisions and artwork preferences. Import verifies exact quantities/rows and source SHA before publishing.
5. Close **Arcane Vault Next**. Open it again: **the saved native catalog and native collection open automatically**, no old app, JSON prompt, or catalog selection needed. Use **Open native collection** to bring the view back if closed.

**Important safeguards:** Only the initial creation of the *private* native workspace writes a file. Subsequent launches are read-only, and the prototype never edits, replaces or deletes the installed legacy app profile. Import is disabled after a native workspace exists, rather than replacing or merging anything silently. Preserve your original exported backup. **Don't manually delete `native-workspace.sqlite`** to force reimport; it contains your separate native copy. A safe refresh/reimport and transactional editing/rollback are future milestones. Native SQLite data is local but **not encrypted**; protect access to your Windows user profile and any backups.

If your existing full backup has missing printing IDs, those rows stay unresolved; even after import, Set Progress credits only trustworthy printings. This first persistent workspace does **not** read the legacy WebView localStorage automatically and does not keep itself synchronized with changes in the old app.

## Current functionality

- **Native Set Progress (AVN-019):** after opening a full backup **and** a read-only Scryfall catalog snapshot, the collection view includes Set Progress with verified owned/total **paper collector-number slots**, a percentage, a set-name search, an optional “include empty sets” toggle, and a per-set list sorted by collector number. All catalog paper slots are included (including numbered promos and tokens); this is **not** a standard booster-only set checklist. Unknown/invalid/conflicting Scryfall IDs and digital/Arena-only printings do not count, and several foil/copy rows from one slot count only once toward completion. Set details open offline card text. No real collection data is modified.
- **Catalog verification inside the collection viewer (AVN-018):** when a read-only catalog snapshot has been opened first, the Physical Collection tab verifies every supplied Scryfall printing UUID against the **catalog's** name, set code, and collector number. Unmatched, invalid, absent, or conflicting IDs stay unresolved; valid UUID syntax alone is not enough. A **Verified Sets** tab shows only proven distinct printings and quantities by set (not completion percentages).
- **Offline Card Details:** double-click a physical/Arena card or select it and click **Card details**. Show cached local Scryfall Oracle text, type, rarity, mana value, color identity and legality, alongside ownership context. No network or inventory edits; without a selected catalog, the details dialog safely shows owned quantities and a missing-metadata message.


- **View collection backup:** open an explicitly exported **schema-2 full Windows backup** to view physical and Arena card quantities in a separate **native WPF collection window**. Browse printing-ID status, sets, collector numbers and finishes in a virtualized grid, filter missing/invalid printing IDs, and see deck, revision and preferred-artwork totals. This is **not a live-collection view, import, restoration, or ownership edit**. Backups remain entirely local and unchanged.
- **EDH Lab is backlogged and hidden from the prototype launcher**, preserving its existing code until Windows core stability and parity are verified. See `docs/arcane-vault-next/WINDOWS_FIRST_GATE.md`.

- [Backlogged, launcher hidden] **EDH Lab — Commanders** contains a second native WPF window after you load a full Scryfall catalog. Browse every eligible identity through 50-row virtualized pages (without the original card search's 200-result cap); choose paper Commander or Arena Brawl, filter exact/includes-all/includes-any mana identities, search names or Oracle text, and use **Surprise Me!** with optional no-repeats. This is an offline, read-only prototype: no deck creation, collection edits, ownership detection, or live legality downloads.


- On a **fresh** install, the native catalog list starts with 25,000 deterministic synthetic cards. **After an explicit one-time native import, the app auto-opens its independent catalog and collection on later launches.** The installed legacy Arcane Vault profile is never opened or changed automatically.
- **Open a read-only catalog snapshot** lets you explicitly choose the existing Scryfall catalog SQLite file. The app opens that file in SQLite read-only mode, uses SQLite's transactionally consistent online backup to make a private temporary snapshot, then queries only the copied printing catalog.
- The first **200** matching printing rows are displayed. Search is a **name-prefix search**, not the full production Card Browser filter engine; percent signs, underscores, and backslashes are treated as literal characters. Queries are parameterized and can be canceled. Query milliseconds are displayed.
- **Use 25,000 synthetic cards** closes and deletes the isolated catalog snapshot and restores the prior synthetic view.
- The snapshot is deleted when the prototype closes normally; after a crash, the OS temporary directory may retain an orphaned SQLite file. Do not rely on automatic cleanup after a forced termination.
- The snapshot copies the entire local catalog database (including its cached ownership mirror), but **never imports, changes, or displays user ownership**; the snapshot is kept under the user's temporary directory, not uploaded.

## Build and try on Windows

Use the .NET 10 SDK.

    dotnet restore ArcaneVault.Next.Desktop/ArcaneVault.Next.Desktop.csproj
    dotnet run --project ArcaneVault.Next.Desktop/ArcaneVault.Next.Desktop.csproj -c Release

Select the catalog from:

    %LOCALAPPDATA%\ArcaneVault\scryfall-catalog.sqlite

If the catalog has never been downloaded in the current Windows app, return to the shipping Arcane Vault app and complete a Scryfall sync first. This prototype does not download or index cards.

Or retrieve the **ArcaneVaultNext-NativeUI-SPIKE** artifact from the passing GitHub Actions workflow. This preview is not signed or packaged as a shipping Arcane Vault update, and no user should replace their existing Windows executable with it.

## Synthetic automated tests

    dotnet run --project ArcaneVault.Next.Tests/ArcaneVault.Next.Tests.csproj -c Release

CI creates a synthetic SQLite printing catalog (25,202 rows), verifies source hash remains unchanged after the read-only snapshot, checks isolation from a later synthetic source edit, tests literal prefix handling and bounded result limits, and asserts temporary snapshot cleanup. It also reports p50/p95 query timing from the CI machine. No user's cards or backups are uploaded.

## EDH Lab candidate rules and known coverage gaps

- Eligibility requires an explicit local Scryfall format legality of `legal`. Brawl uses current `brawl`, with `historicbrawl` used only when `brawl` is absent. Cards must also be available on Arena for Brawl. An unknown/malformed legality excludes the card; the local catalog may be stale.
- Paper Commander includes legendary creatures, plus explicit `can be your commander` / `may be your commander` exceptions. Arena Brawl additionally supports legendary planeswalkers. Standalone Background enchantments are not treated as commanders.
- A qualified Vehicle or Spacecraft requires validated printed power/toughness, but the current legacy `cards` table does **not** preserve it. Such special cases are conservatively excluded until catalog schema enrichment. **This preview is not yet an exhaustive commander legality checker**, especially for paired commanders.
- A commander name appearing on a list does **not** mean that you own it. Paper and Arena ownership remain separate and are not read by this window.
- The random picker uses uniform secure random index selection from the current legal and filtered pool; filters are retained when rerolling. A result set with zero candidates returns a clear message instead of a card.

## Current limitations and next engineering gates

- The native workspace persists collection rows, full backup contents, deck history and artwork choices locally, but **does not support inventory edits, live sync, replayable refresh, restoring a full backup into the installed app, or on-device migration cutover**. Card artwork rendering, rich card browser and Scryfall network sync remain future work.
- The timing displayed is **SQL query timing**, not end-to-end input latency or first paint.
- A temporary snapshot of a large catalog consumes extra free disk space. Verify disk availability before testing on a low-space device.
- WPF scrolling, artwork memory, cold/warm startup, and 1k/5k/25k realistic browsing need explicit reference-device performance measurements before deciding to replace the current UI.
- The existing WebView2 source-of-truth storage requires its own migration dry-run and rollback verification; **this prototype does not read it**.
