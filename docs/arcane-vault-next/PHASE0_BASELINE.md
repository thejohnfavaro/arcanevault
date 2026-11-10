# Arcane Vault Next — Phase 0 Reliability Audit and Baseline Runbook

**Status:** Tooling committed for review. **Scope:** Read-only Windows audit + synthetic CI fixtures. **Date:** 2026-11-10.

**Relevant tickets:** AVN-001 (state inventory + safe backup verification); AVN-002 (reproducible local API timing).

## Known persisted stores (from source review)

| Platform | Store | Current responsibility | Migration disposition |
| --- | --- | --- | --- |
| Windows | WebView2 profile: %LOCALAPPDATA%\ArcaneVault\WebView2 | Browser localStorage collection/decks/settings and IndexedDB metadata | Read-only extraction, immutable backup, staged validation |
| Windows | %LOCALAPPDATA%\ArcaneVault\scryfall-catalog.sqlite | Scryfall card and print catalog, cache/index and ownership mirror | Preserve on failure; catalog data is **not** authoritative paper ownership |
| Windows | %LOCALAPPDATA%\ArcaneVault\Artwork | Cached official images | Rebuildable cache; keep explicit preferred art choices from owned-data backup |
| Windows | Full backup JSON (via Import & Data) | Arena cards, physicalCards, preferredArtwork, deckLibrary, wildcards, settings | Primary user-accessible verification input; not necessarily exhaustive of every legacy browser key |
| Windows | Collection sync JSON (QR / Wi-Fi / USB) | Schema-1 transfer: arenaCards + physicalCards + preferredArtwork | Snapshot only; does **not** contain full deck library |
| Android | Native SharedPreferences arcane_vault_collections_v1 | Durable authoritative Arena/physical inventory JSON | Read-only extraction and exact duplicate-safe reconciliation |
| Android | arcane_catalog.db SQLite | Card print catalog + Arena/physical ownership mirrors + identified printings | Must not be used to fabricate missing physical printing identities |
| Android | WebView storage and legacy localhost origins | Decks, settings, preferences and recovery candidates | Source-by-source migration; avoid erasing prior origins before validated copy |

**Important:** This inventory is derived from the source code, not a forensic inspection of any installed machine. Additional data files/stores will be catalogued during a read-only on-device audit.

## 1. Back up your current app first

In the existing Windows app, use its **full backup/export** action. Save the JSON file in a local private directory, *outside the Git repository*. For an independent Android copy, export a backup from that device too. Do not uninstall either existing app. Do not clear caches or migrate/replace collections just to run Phase 0.

The audit commands below only **read named JSON backup files**. They do not query or modify live WebView, SQLite, Android, or Windows settings.

## 2. Verify the full backup

Install Node.js 22 or newer on Windows. In a checkout of the Arcane Vault Windows repo:

    node tools/phase0-audit.mjs --backup "C:\Private\arcane-vault-backup.json"

The report contains **aggregate counts and warning codes only** (no card names, print IDs, deck names, file paths, or content hashes). Counters include Arena copies, physical quantities, confirmed/unresolved printing rows, duplicate physical identities, deck count/revisions and preferred art. An unresolved-printing warning is not proof that data was lost; it means those entries cannot be credited to a set without further confirmation.

Typical exit codes:
- **0**: parsed and structurally valid (or exact comparison match)
- **2**: invalid input/format or unreadable file
- **3**: compared backups differ in at least one tracked category

A **successful parse is not by itself a successful restoration**. It also does not prove all browser-only preferences were exported.

To compare before/after snapshots without uploading either file:

    node tools/phase0-audit.mjs --backup "C:\Private\before.json" --compare "C:\Private\after.json"

Comparison is deliberately strict: beyond totals, it also compares normalized stored record contents so a changed printing can be caught even if total copies remain constant. Reordering inventory rows alone does not fail equality. Full backups and collection-sync snapshots are not equivalent: missing decks/art in a QR snapshot appear as differences.

**Do not commit JSON backup files, CSV exports, logs, or benchmark reports containing private user data.**

## 3. Measure the running Windows app's local read-only API

Launch the existing Windows Arcane Vault app first, allow the Scryfall catalog to complete indexing, then run:

    node tools/phase0-benchmark.mjs --iterations 20

The tool measures **p50/p95 response time in milliseconds** for:
- /api/catalog/status
- /api/catalog/search (fixed 'sol' query; first page only)
- /api/catalog/sets (aggregate set progress)
- /api/catalog/variants (fixed 'Sol Ring' query)

All requests are GET, restricted to the localhost service. No network telemetry, no API mutations, no user card names in output. A locally missing/unready catalog is reported with explicit skipped tasks. The default target is http://127.0.0.1:8765/ and the default iteration count is 20; repeat measurements 3 times and compare warm/cold cache behavior on the same machine.

    node tools/phase0-benchmark.mjs --url http://127.0.0.1:8765 --iterations 30

**Measurement limitations:** These timings do *not* measure UI paint, WebView2 startup, cold SQL initialization, scrolling, image decode, memory usage, or Android performance. Those will need instrumented native/app profiling and on-device tests. No actual on-device baseline values are claimed by this document.

## 4. Repeatable synthetic validation in CI

    node tests/phase0-audit.mjs

Windows CI runs this synthetic fixture alongside existing regression tests. It verifies:
- auditor does not alter input bytes or file modification time;
- Arena and paper counts remain independent;
- exact/unknown physical printings have correct aggregate counts;
- duplicate printing-identity warnings;
- exact snapshots match despite input row order;
- quantity or printing-identity change causes a mismatched comparison;
- schema-1 collection snapshots do not masquerade as full deck backups;
- invalid quantities are rejected;
- read-only local API sampler calculates p50/p95 against an in-process fake HTTP server.

Synthetic CI passing is necessary but is **not** a substitute for testing the current installed user's backups.

## 5. First findings and follow-up work

- Windows persists important mutable user state in WebView2 storage, and existing saves catch storage errors rather than providing a transactional commit. This is a **priority audit risk** for Phase 1, not proof that current data is corrupted.
- Windows QR/USB transfers serialize the stored physical inventory records; card names without verified print IDs remain unresolved on Android. The new domain contract must not assign arbitrary printings.
- Android maintains both native durable collection JSON and SQLite ownership mirrors, plus UI WebView state. Migration must choose explicit authoritative sources and avoid double-counting recovered legacy snapshots.
- The current stack targets net8.0-windows; evaluate .NET 10 LTS in a separate compatibility spike with restored build/test baselines.
- Time measurements, startup/memory metrics, full backup/restore on a disposable profile, and an Android camera OCR candidate pilot remain **outstanding**.

## 6. Exit criteria for AVN-001 / AVN-002

| Check | Current status | Evidence required |
| --- | --- | --- |
| Source-store inventory | Initial review documented | Confirm installed profile paths and extra keys |
| Read-only JSON backup audit tool | Implemented in review branch | CI pass plus user-run private file |
| Exact compare of backup contents | Implemented in review branch | CI pass plus pre/post backups |
| Local API benchmark harness | Implemented in review branch | CI simulated endpoint pass |
| Real Windows timings | Not measured | 3 local runs, fixed device and catalog |
| Full restore on disposable Windows profile | Not performed | Before/after exact counts, printing IDs, decks, art |
| Android durable storage check | Not performed | Private device diagnostic run |
| .NET 10 LTS feasibility | Not started | Separate branch, build/tests and package check |

Do not merge app-storage changes or claim complete Phase 0 until the on-device validation items are addressed.
