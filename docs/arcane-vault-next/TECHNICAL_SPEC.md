# Arcane Vault Next — Technical Architecture & Product Specification (RFC-001)

**Status:** Proposed; review-only. **Date:** 2026-11-10. **Priority:** reliability > performance > desktop-native feature parity > companion enhancements.
**Repositories:** Windows: https://github.com/thejohnfavaro/arcanevault ; Android: https://github.com/thejohnfavaro/arcanevaultmobile.
**Scope:** a native Windows-first product with an offline-capable Android companion. This document is a specification, not an implementation claim.

## 1. Executive decision

Keep Windows as the full-featured command center. Migrate its UI incrementally to native WPF, while retaining proven C# services. Android stays a narrower companion for collection lookup, owned-printing edits, set progress, deck viewing, and scanning. Prioritize a single authoritative data model with versioned synchronization **before** extensive UI rewrites. Keep current Windows and Android applications operational throughout.

**Initial engineering allocation:** approximately 75% on Windows and shared domain/data quality; 25% on the Android companion, with some overlapping work. Quality issues take precedence over any allocation.

### Non-negotiable product guarantees

1. No silent loss, duplication, or reassignment of physical copies, Arena quantities, decklists, selected printings, or preferred artwork.
2. No guessing physical printings from card names, and no unconfirmed scanner additions.
3. App upgrades and imports must be recoverable; no destructive migration on first launch.
4. A canceled, interrupted, or failed catalog sync never invalidates the last known-good catalog.
5. Offline work is usable and durable; sync can resume without applying the same edit twice.
6. Existing applications remain usable until feature parity and migration verification pass.
7. Performance and data integrity gates block release, even if all UI features appear complete.

## 2. Verified repository baseline (2026-11-10)

The facts below are based on the current repository sources; they are **not** measurements of runtime performance.

### Windows

- The existing application is .NET 8 Windows Forms hosting WebView2 (ArcaneVault.Desktop/ArcaneVault.Desktop.csproj, MainForm.cs).
- The embedded UI is a large HTML/CSS/JavaScript file (ArcaneVault.Desktop/Assets/Arcane_Vault_MTGA_Collection_v4_2.html; approximately 3,377 lines, including embedded data and styles).
- C# already implements an HTTP loopback service (LocalAppServer.cs), a local Scryfall SQLite catalog (ScryfallCatalog.cs), artwork caching (ArtworkCache.cs), GitHub update handling (UpdateManager.cs), and user-initiated one-time LAN/ADB-USB collection transfers (LanCollectionTransfer.cs).
- UI-side state still uses WebView localStorage and an IndexedDB metadata cache. Physical printing rows are included by the current Windows portableSyncSnapshot() as stored, so unidentified source rows remain unidentified when transferred.
- The project includes JavaScript regression checks and a Windows C# test project. The current Windows version observed at review is 5.21.15.

### Android

- The existing Android shell is Java + WebView with an embedded HTML/JavaScript UI (app/src/main/java/com/arcanevault/mobile/MainActivity.java; app/src/main/assets/index.html).
- CatalogStore.java maintains native SQLite tables for card names, printing variants, Arena ownership, physical ownership, and confirmed physical printings.
- Collection recovery and transfers currently involve native SharedPreferences, WebView localStorage for other UI state, and SQLite ownership mirrors. Schema-1 QR/USB imports are snapshot replacements, not a conflict-aware incremental sync protocol.
- Android already integrates an ML Kit/Google Play Services code scanner for *QR transfer links*. This is **not** a trading-card text-recognition scanner.
- Android's Set Progress intentionally counts only confirmed printing identities, so an otherwise successful name-only transfer can still yield 0% completion.
- Observed Android build metadata: versionName 2.0.25, versionCode 45.

### Immediate technology-lifecycle concern

The Windows project targets .NET 8; Microsoft's published end-of-support date for .NET 8 is **2026-11-10**. Plan and regression-test a move to .NET 10 LTS before building the new native desktop host. Do not bundle this framework migration with a simultaneous storage migration.

Reference: https://dotnet.microsoft.com/en-us/platform/support/policy

## 3. Target architecture

### Windows host

**Proposed:** WPF on .NET 10 LTS using MVVM-style presentation, dependency injection, background services, and virtualized lists. Verify the approach with a short performance prototype before committing to the complete rewrite; WPF is the recommended default, not an untested guarantee of higher performance.

Suggested projects/modules:
- **ArcaneVault.Domain** — immutable IDs, inventory/deck entities, constraints, operations, reconciliation, sync and migration contract models.
- **ArcaneVault.Data** — SQLite repositories, migrations, transactional unit-of-work, indexing, integrity checks, search and caching.
- **ArcaneVault.Services** — Scryfall/catalog jobs, image loading, deck analysis, Set Progress, import/export, updates, arena-log processing.
- **ArcaneVault.Desktop.Native** — WPF shell, card browser, collection, set progress, decks, strategy tools, diagnostics.
- **ArcaneVault.Contracts** — versioned JSON schemas, golden samples and conformance tests, shared across Windows and Android.
- **ArcaneVault.Migration** — read-only legacy extraction, staging, comparison, commit/rollback, reports.

Reuse working native code when possible. Avoid routing every native UI operation back through the local loopback HTTP server: call services directly in the new desktop host. Retain the existing server only for compatibility where needed, bound to loopback and hardened. No global UI thread locks during catalog operations.

### Android companion

- Keep the existing app identity and build/update path so installed data remains available.
- Retain native Java/SQLite initially; migrate critical collection and scanning interactions to native views incrementally rather than rewriting the entire companion.
- Implement the same **versioned contract and invariants** as the C# core, with platform-specific implementations and cross-platform fixture tests. Shared contracts do not imply that Java and C# automatically share executable code.
- Companion scope: browse/search owned cards, details, physical owned-printing edits, Set Progress, deck viewing/light edits, manual import/backup, native camera scanner, offline edit queue, synchronization status/conflicts.
- Desktop-only: full Arena log discovery, deep analysis, large bulk editing, complete Deck Studio and Strategy Workbench, advanced diagnostics and update management.
- Optional future cloud sync is explicitly out of scope for the initial release; local QR/USB/Wi-Fi must work independently of an online account.

### Data flow

**Desktop:** WPF view -> view model -> domain/application service -> SQLite authoritative store / background catalog service.

**Companion:** native or retained WebView view -> Java application service -> SQLite authoritative store -> durable outbound change journal.

**Transport:** pairing/transfer session -> versioned contract validation -> staged changes -> conflict evaluation -> atomic commit -> acknowledgment. Camera OCR and Scryfall matching only propose card identities; they never write physical ownership without user confirmation.

## 4. Authoritative domain model and storage

A single user-owned collection is distinct from the downloadable Scryfall catalog. Each has its own lifecycle and backup policy.

Proposed entities:
- **CardIdentity:** normalized Oracle/card identity, canonical name, other faces/aliases where available; never an ownership key by itself.
- **PrintingIdentity:** Scryfall printing ID, set code, collector number, language and card-face metadata. An unresolved printing may temporarily retain source name/set/number without falsely claiming a Scryfall ID.
- **PhysicalInventoryLine:** stable local UUID; printing reference nullable; finish, condition, language, location, quantity, notes, provenance, last-modified revision.
- **ArenaInventoryLine:** Arena card identity/name and owned quantity, separate from paper ownership and supported by its own import provenance.
- **Deck / DeckEntry / DeckRevision:** stable IDs, entries, commander/format metadata, favorites/tags/folders, history.
- **ArtPreference:** scoped to a card or owned printing and kept independent of ownership count.
- **CatalogSnapshot:** source version, index generation, imported timestamp, content integrity checksum; one active snapshot plus a recoverable prior one.
- **ChangeEvent:** unique operation ID, device ID, entity ID, base revision, operation type, before/after or delta, timestamp, schema version.
- **SyncCheckpoint / Conflict:** acknowledged operation IDs and explicit unresolved conflicts.

### Invariants

- Quantities are nonnegative integers; no transfer or scanner can increase totals without an explicit edit.
- A Scryfall print ID (or validated set+collector fallback) is required before assigning set-specific progress.
- The exact finish and printing can differ for multiple copies of the same card.
- Missing/ambiguous identity stays **unresolved** and visible, not silently assigned.
- A duplicate incoming change operation ID is ignored after the first successful application.
- Shared collection snapshots cannot overwrite newer local work without a conflict/merge decision.
- Physical and Arena inventories never share an ownership counter.
- Deleted inventory lines and deck entries need tombstones or equivalent versioned delete operations so older devices cannot resurrect them.

### Persistence

Use transactional SQLite as the authoritative storage layer for mutable collections and decks on each platform. Separate catalog datasets and artwork cache from user-owned information. Enable WAL and foreign keys where appropriate, control connection lifetimes, use bounded caches, and make index/schema changes transactional. **Do not write two independent authoritative copies** to WebView storage and SQLite. Keep WebView data readable during migration as a recoverable source, not as a continuously competing database.

Proposed query indexes: print_id, (setcode,collector,language), normalized card name, ownership by printing and collection, deck_id, change_event operation_id and sequence, catalog freshness. Benchmark index design on real distributions before locking it in.

## 5. Data migration and rollback

Migrate **from both currently installed applications**, not just their GitHub source files. The user may have valuable local WebView-origin data and device-only selections not represented in exported CSVs.

1. **Inventory:** enumerate all known user data sources (Windows localStorage/IndexedDB, Windows catalog/store, Android durable SharedPreferences, native SQLite ownership, Android WebView settings/decks). Record data-shape and source-version information. Never equate a cached catalog entry with ownership.
2. **Immutable export:** create versioned full backup and per-source read-only snapshots; record byte hashes and timestamps.
3. **Dry run:** import to a new SQLite staging database. Produce a human-readable report of row counts, quantity totals, deck counts, identified/unidentified printings, duplicate IDs, missing mappings and conflicting sources.
4. **Verification:** compare totals per card and printing; verify representative known cards, deck revisions, preferred artwork and formats against source exports. Ambiguous rows block automatic destructive commit.
5. **Atomic cutover:** after user approval, close source writes, apply/verify a bounded final delta, switch to the new authoritative database in one recoverable step. Preserve old storage and a rollback marker.
6. **Rollback:** provide one-step restoration to the prior app/profile and snapshots after a failed migration. Do not remove legacy data until explicitly permitted after multiple verified launches/backups.

Migration fixtures must include split/adventure cards, multiple printings, duplicate names, name-only ownership, partial catalogs, changed quantities, corrupted JSON, duplicate source snapshots, wrong finishes, and newer versions of the sync contract.

## 6. Local sync protocol v2

Existing schema-1 collection snapshots remain **importable in compatibility mode**; they must not be silently interpreted as conflict-aware v2 changes.

Proposed v2 payload fields: protocol version, exporter build, device ID, collection ID, base revision/checkpoint, snapshot or batch ID, source timestamp, change operation IDs, print identities, item UUIDs, quantity operations, deck changes, hash/checksum, and feature flags.

Sync behavior:
- An initial full snapshot creates a clear baseline, with printed before/after totals and user confirmation.
- Subsequent sync exchanges durable change events rather than blindly replacing a collection.
- Add-copy deltas can merge when distinct and valid; absolute SetQuantity/printing changes based on stale data trigger explicit conflict resolution.
- Every applied operation is recorded before acknowledging receipt; retrying a QR/USB transfer is idempotent.
- A disconnect leaves pending operations journaled and allows retransmission; a hash mismatch or incomplete payload fails closed without modifying active records.
- Pairing tokens are short-lived, user-approved, scoped to a device/session; do not expose a general unauthenticated LAN writer.
- Provide per-device last-sync status, pending edits, unmatched printing count and actionable conflict list.
- Provide recovery through file export/import when QR/USB/Wi-Fi is unavailable.

Define a stable contract test suite replayed on both Windows and Android. A full protocol security review must precede enabling unattended device pairing.

## 7. Performance budgets and measurement

These are **proposed release targets**, not observed benchmarks. Measure cold and warm behavior separately, on specified real devices, with p50/p95 and memory/CPU peaks. Do not declare the rewrite faster until measured.

| Operation | Proposed acceptance target |
| --- | --- |
| Warm indexed search, local | p95 < 150 ms |
| Cached card detail display | p95 < 250 ms |
| Cached Set Progress aggregation | p95 < 1,000 ms |
| Search filter interactions | p95 < 200 ms to visible update |
| Windows warm startup to usable UI | p95 < 3 seconds on reference device |
| Android owned-card search offline | p95 < 250 ms on reference phone |
| 100 consecutive inventory edits | 100% durable, exactly-once after restart |
| Catalog update interruption | previous complete catalog still usable |
| Large collections | no unbounded UI item/image instantiation; smooth scrolling measured separately |

Reference fixtures: actual backed-up user collection where authorized; synthetic 1k/5k/25k owned printing rows, 100k+ catalog printings, 500/5k decks or deck entries, split-faced names, multi-set reprints, and null/partial metadata. The benchmark harness should use a fixed machine/device profile, clean catalog generation, warm/cold cache definitions and repeatable input.

Performance work:
- SQLite query plans, appropriately targeted indexes, paginated/virtualized results, prepared queries.
- WPF list virtualization and container recycling; **also** implement data virtualization where SQL result sets are large.
- Asynchronous image decoding/thumbnails, bounded memory/disk caches, cancellation when scrolling, no full-resolution art for invisible items.
- Background catalog staging with progress and cancel/retry, bounded network rate, no UI-blocking transactions.
- Avoid repeated full collection scans and redundant JSON serialization on every keystroke or tile draw.
- Trace startup, searches, query counts, cache hit rate, sync duration, memory high-water, unmatched and unresolved identity counts.

References:
- WPF virtualization: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/optimizing-performance-controls
- Scryfall rate limits and bulk-data recommendation: https://scryfall.com/docs/faqs/i-m-having-trouble-accessing-the-scryfall-api-or-i-m-blocked-17

## 8. Android card scanner specification

### MVP user journey

1. Tap **Scan card**, grant camera permission, see a native CameraX preview with card-edge guidance and focus/lighting feedback.
2. Capture an image or analyze selected frames with throttling. Run **on-device ML Kit Text Recognition v2**, preferably bundled so first-use offline scanning works.
3. Extract candidate card name, set code/symbol where feasible, collector number (including suffixes), language hints, and card orientation. OCR is untrusted input, not a verified ID.
4. Rank exact local printing-catalog candidates using title + set/collector + layout/art image and optional face information.
5. Display the top candidates with clear uncertainty; require manual selection/confirmation of printing, quantity, finish, and destination collection.
6. Save one atomic inventory transaction and one outbound sync change event; show success, allow undo and rapid next scan.
7. Queue offline edits; sync later with the same idempotent protocol. Never upload full camera photos by default.

### Quality thresholds for the pilot

- **Zero silent unconfirmed inventory writes.**
- **Zero duplicate additions** when the same save/sync operation is replayed.
- Provisional top-3 correct-printing candidate recall >= 98% on an agreed test set of legible, supported English cards. Measure separately for foil glare, double-faced cards, old frames, tiny collector text, obscured set symbols and promo variants; if the pilot misses, adjust scope/UX rather than guessing.
- p95 capture-to-candidate list under 2 seconds on agreed reference device for locally indexed metadata, subject to pilot measurement.
- Camera and OCR continue without network after the bundled model and local catalog are installed.
- Explicit handling for unknown cards, OCR failure, crop error, partial catalog, duplicate/owned printing, and cards that Scryfall has not cataloged yet.

Later scope: continuous rapid scan, binder-page scanning, bulk undo, optional camera-art similarity model, richer edition recognition. These are **not required for MVP**.

References:
- CameraX preview and image analysis: https://developer.android.com/media/camera/camerax/architecture
- ML Kit Android Text Recognition v2: https://developers.google.com/ml-kit/vision/text-recognition/v2/android

## 9. Feature parity and boundaries

**Windows must retain** the current Card Browser/Details, owned and unowned search, variants/artwork choices, multiple physical printings, pricing/value refresh, Set Progress, Deck Library/Studio, deck versioning/validation, synergy/strategy tooling, goldfishing, Arena-log/match features, collection history, CSV imports/exports, full backups, settings/themes, and update delivery. Every screen is tracked in the companion parity matrix.

**Android must reliably support** physical inventory browsing and editing, offline local card catalog, Set Progress, printing-specific ownership, decks read-only or lightweight edits, QR/USB/Wi-Fi sync, backup/export, and scanning. Platform-inappropriate desktop log scraping is excluded.

All existing functional workflows require golden-data comparisons against the current app before migration of each screen is declared complete. Regressions in accuracy, ownership, data access, or startup are blockers.

## 10. Release engineering and telemetry

- PR checks: compile, unit tests, schema contract tests, golden-set parity tests, simulated migration/recovery tests, smoke UI tests; add device tests where hardware integration matters.
- Nightly optional performance run produces baseline-versus-candidate reports and flags regressions.
- Backup and restore tests must verify **contents**, not just file existence.
- Fatal startup/migration failures show a recovery path, do not reset local storage, and preserve diagnostics without exposing personal collection data by default.
- Release artifacts must be correctly signed, versioned, checksummed, and update-compatible; retain current signing identity on Android. Canary migration is opt-in.
- No production release while any documented P0/P1 data-loss or repeatable corruption scenario remains open.

**Minimum release blockers:** incomplete migration totals, non-idempotent sync, unconfirmed printing assignments, broken offline reads, corrupted catalog rollback, missed signed updater identity, performance worse than current baseline beyond agreed tolerance, or a failing regression fixture.

### EDH Lab: shared commander discovery and deck-building domain (planned)

EDH Lab is a native Windows-first Commander/Brawl workspace using catalog, collection, deck persistence and format-validation services, not another unrelated data silo. Its discovery engine must enumerate **all format-eligible commanders** from the active card catalog, deduplicated by card identity while retaining owned printing variants. Commander candidates support search, exact/contains-all/contains-any color-identity filters (including colorless), legality, Arena availability and owned status.

The `Random Commander` feature samples the current legal filtered candidate pool with stable, documented eligibility rules, a clear empty state, and optional no-repeat-until-exhausted behavior. Selecting or rerolling does not mutate user ownership or existing decks. Creating a deck explicitly chooses a paper Commander or MTG Arena Brawl ruleset and checks commander exceptions, multi-commander combinations where supported, color identity and deck validity. Format bans, Arena availability, and ownership/wildcard costs are **separate** checks.

The toolbox should offer collection-first card recommendations, deck-role coverage, ramp/draw/removal/curve guidance, bracket or power-goal settings for Commander, and ownership-aware upgrades. Native Windows handles deep analysis and import/export; Android receives commander browsing/randomization and safe lightweight editing once data synchronization is reliable. This is **planned future functionality**, not implemented in the migration-staging prototype.

## 11. Open decisions / spikes

1. Validate WPF on .NET 10 LTS with a representative virtualized card grid before finalizing the UI framework.
2. Inventory all actual local data keys and sources; choose per-field authoritative origin and migration policy.
3. Determine whether Windows should keep a compatibility HTTP listener after the final native cutover.
4. Define first-pair trust and conflict UI for offline multi-device edits.
5. Benchmark the scanner on the target Android device and real card photo corpus before setting definitive recognition targets.
6. Confirm backup file/asset storage constraints on both systems and Scryfall art licensing/attribution.
7. Decide telemetry opt-in and privacy policy; no external accounts or photo upload assumed.

## 12. Definition of Done

This RFC is accepted when its data model, migration plan, quality gates, platform responsibilities, and protocol v2 are reviewed. Implementation is complete only when:
- Native Windows reaches documented full functional parity and beats agreed p95 baselines without data loss;
- Android offline ownership/scanning and v2 sync pass device tests;
- legacy-to-native migrations pass comparison checks and rollback drills;
- user data remains readable during transition;
- both release/update mechanisms remain verifiably safe.

See **MIGRATION_ROADMAP.md** for phasing and **FEATURE_PARITY_AND_QA.md** for acceptance matrix.
