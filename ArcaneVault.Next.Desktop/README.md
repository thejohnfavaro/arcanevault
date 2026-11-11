# Arcane Vault Next — Windows native collection workspace (preview)

This is a **separate experimental WPF/.NET 10 application**, not a replacement for the shipping Arcane Vault executable. The shipping Windows app and its framework/version are unchanged.

## Native saved Deck Library editor — AVN-028

From **Open native collection**, select the **Deck Library** tab. The native Windows app can now **create, edit, and delete saved decks** without opening the installed legacy application, connecting WebView2 or exporting a backup.

- **New deck:** enter a name, optional Commander (for non-Commander formats leave blank), and a mainboard with one line per card like `1 Sol Ring` or `12 Island`. Paste the mainboard lines, not the `About / Commander / Deck` headers. Card names including split card `//` are preserved. Repeated names in one pasted list combine case-insensitively. This is **not** a deck-legality, color-identity, Arena-availability or owned-card verification tool yet.
- **Edit selected deck:** uses the exact persisted row and optimistic workspace SHA to prevent stale saves. A prior version is appended to the deck's **existing revision history** before saving; earlier revisions, stable deck IDs, favorite flags, folder, tags, artwork cover, linked Arena ID, and unknown future fields remain intact. Prior per-card metadata is retained for cards whose names stay in the edited list.
- **Delete selected deck:** explicitly asks for confirmation and removes only that deck from the active library. All its older revisions remain recoverable in the complete previous native SQLite workspace.
- **Every successful save/delete** retains a uniquely named full native recovery file under `%LOCALAPPDATA%\\ArcaneVaultNext`. Use **Restore native recovery** on the main screen to undo a change. A no-op, unsupported legacy entry, stale selection or invalid input cannot silently overwrite existing data. Deck changes **never** change physical or Arena ownership, preferred art, or the Scryfall catalog.

**Limitations:** This first native editor is designed for a plain mainboard + optional commander, supports up to 500 lines / 1,000 mainboard copies, and does not yet offer drag-and-drop editing, sideboards, format legality or color identity warnings, deck artwork, import of a complete Arena export including headers, or revision-by-revision restore UI. Older malformed entries remain visible but are deliberately read-only until safely repaired. The installed original app remains untouched; this is still a separate Windows preview awaiting real-device verification.

## Built-in Scryfall catalog download and refresh — AVN-027

Use **Download / update Scryfall catalog** on the Arcane Vault Next main window. This feature requires an internet connection, but **does not require the old Arcane Vault application, an existing Scryfall database, a JSON backup, or WebView2**.

The native Windows app fetches the **official Scryfall bulk-data manifest**, selects the English-first **default_cards** printing dataset (not `oracle_cards`, which lacks distinct printings), and downloads the bulk file over HTTPS with a recognizable User-Agent. Import accepts **gzipped JSONL** and legacy **JSON array** formats, streaming one printing at a time into a *new* local SQLite catalog under `%LOCALAPPDATA%\\ArcaneVaultNext\\native-catalog.sqlite`. The data may be **hundreds of MB**; allow ample disk space for download, staging and a retained older catalog. It does not request each card individually, scrape images or cache images during this operation.

The catalog provides card names/details, exact printing IDs, set/collector/finish metadata and Set Progress. **Cards are never automatically marked owned during a catalog refresh.** All physical/Arena ownership, decks, art preferences and user data remain in the separate native workspace database. A data integrity check, minimum catalog-size guard, and separate readback must pass before the new catalog becomes active; an existing catalog is retained under a uniquely named `native-catalog.catalog-rollback-*.sqlite` file. If a download fails, is canceled, is malformed or is incomplete **before publication**, the existing catalog remains intact. The app also skips repeated imports when Scryfall's dataset timestamp has not changed.

The **Cancel Scryfall sync** button cancels a running download or build and removes its scratch files. The installed legacy Windows catalog is never modified. Manual catalog selection and one-time legacy catalog copy remain available only as fallback routes.

## Native collection editing — Physical and Arena (AVN-024/025/026)

You can now create a native workspace using **Start fresh collection**, then manage ownership directly in its **Physical Collection** and **Arena Collection** tabs. Open **Open native collection** to find these controls.

- **Physical Collection → Add physical printing:** requires a loaded local Scryfall catalog (no installed legacy app). Search a card name, select the exact paper set/collector/printing UUID, choose nonfoil/foil/etched and the number of copies. Re-adding the *same verified printing and finish* increases that exact row's quantity; other printings are not merged. The app does not guess name-only printings.
- **Physical Collection → Edit physical quantity:** select the exact owned row, change its quantity to **0–99,999**; zero removes that row and requires typing **REMOVE**. Increasing a selected unresolved legacy row does not invent any printing identity; it remains unresolved.
- **Arena Collection → Add Arena card:** does **not require the local Scryfall catalog**. Type a card name and number of copies; an unambiguous matching Arena name gains copies or a new name-only row is created. Arena rows do not claim a physical printing or Arena format legality.
- **Arena Collection → Edit / remove selected Arena card:** select one exact row, change its name and/or absolute quantity, or set quantity to **0** to remove it. Imported duplicate Arena names cannot be silently combined; an exact selected row can still be edited.
- **Every successful edit** uses a version-checked, staged SQLite replacement and preserves the *entire previous native workspace* in a separately named recovery file. Choose **Restore native recovery** in the main window to undo. A stale selection, invalid quantity, ambiguous duplicate, or failed validation is rejected without updating the collection.

After an edit, the viewer reloads physical/Arena totals, Card Details and Set Progress from the saved native collection. Existing deck library, revisions, artwork choices, annotations, unknown backup keys, legacy app data, and local Scryfall catalog are preserved. Editing does **not** write to the old Windows app or sync to Android. **This is not yet a fully shipped replacement**: native deck editing and Scryfall catalog syncing are available in the separate preview, but scanning, signed updater/installer and full user-device validation remain outstanding.

## Start a completely fresh native collection — AVN-023

Use **Start fresh collection** in the native Windows app. This works even on a new computer with **no installed legacy Arcane Vault application, no Scryfall catalog, and no exported collection backup**. No sample cards or owned printings are inserted.

- With **no existing native collection**, it creates an empty, verified `%LOCALAPPDATA%\\ArcaneVaultNext\\native-workspace.sqlite` directly.
- With an **existing native collection**, the confirmation dialog shows the current physical/Arena copies, deck/revision count and preferred-artwork entries. It states exactly what will be reset. Type **`START FRESH`** to enable the final button. The app atomically creates a completely empty active database and saves the entire former native database in a uniquely named `native-workspace.rollback-*.sqlite` recovery point.
- **Cleared from the active collection:** all physical card ownership, Arena card ownership, deck library/history, favorite choices, preferred art, wildcard counts, and collection-specific/legacy settings. The **Scryfall catalog is retained separately** and is not wiped or changed; the original legacy Windows app, its user data, and old exports are not touched.
- An already-empty native collection is a **no-op**; resetting it again does not generate needless backups. **Restore native recovery** can restore all original data, and the intervening empty collection is then also preserved as another recovery point.
- **After starting empty:** You can add physical cards from a loaded Scryfall catalog and add Arena names manually. Native deck authoring and scanning are not implemented yet; the native catalog now downloads independently. Do not delete recovery files.

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
