# Arcane Vault Next — Feature Parity & QA Acceptance Matrix (RFC-001B)

**Windows-first scope:** core Windows recovery and collection parity are active. EDH Lab QA work is backlogged until the Windows recovery gate in WINDOWS_FIRST_GATE.md is met. Android new features are deferred unless blocking data safety.

**Status:** Proposed test contract; not evidence of passing tests. Cross-reference TECHNICAL_SPEC.md and MIGRATION_ROADMAP.md.

## A. Feature parity ledger

**Legend:** F = full desktop parity; C = Android companion implementation; V = view/read-only companion; N/A = intentionally desktop-only. A "target" below is not a shipped feature.

| Area | Existing behavior that must be preserved / improved | Windows native | Android companion | Gate |
| --- | --- | --- | --- | --- |
| Card Catalog | Search all/owned/unowned, rarity, types, mana identity, roles, sorting, paginated results | F | C | Q-PERF-01, Q-DATA-01 |
| Card Details | Oracle text, card faces, image/variant gallery, metadata, legality, owned counts | F | C | Q-DATA-02 |
| Physical inventory | Per-printing quantities, finishes, print selection, location/condition, edit & bulk import | F | C (essential edits) | Q-DATA-03 |
| Arena inventory | Separate ownership and CSV/log-based evidence | F | V | Q-DATA-04 |
| Set Progress | Confirmed printings, set art/symbol, chronological sorting, collector sort, owned/missing filtering | F | C | Q-DATA-05 |
| Collection valuation | Price fetch, value totals, sorting and failure states | F | V (cached) | Q-DATA-06 |
| Collection import/export | CSV, sync snapshots, full backup, restore, rollback, provider variant handling | F | C (backup/basic) | Q-MIG-01 |
| Deck Studio | Create/edit, Brawl/Commander rules, counts, colors, ownership, validation, recommendations | F | C (simple edit) | Q-DECK-01 |
| Deck Library | Save/rename, covers, favorites, folders/tags, revisions and restore | F | C (read/light edit) | Q-DECK-02 |
| EDH Lab | All legal Commander/Brawl commanders, exact/inclusive color-identity filters, random selection, collection-aware builds, validation and import/export | New F | C (browse, random, light edit) | Q-EDH-01, Q-EDH-02, Q-EDH-03 |
| Goldfishing | Hand/draw/mulligan/land/spell simulation, state restoration | F | N/A initially | Q-DECK-03 |
| Strategy Workbench | Mana/curve/probability, functional/creature searches, deck comparisons | F | V optional | Q-ANALYSIS-01 |
| Synergy/archetypes | Combo and role recommendations, advisor, owned matches | F | N/A initially | Q-ANALYSIS-02 |
| MTG Arena integration | PC log reading, automatic deck/match assignment, performance analytics | F | V synced stats | Q-ARENA-01 |
| Scryfall sync | Bulk catalog, variants, legality, metadata reconciliation, caching, rate limiting | F | C offline catalog | Q-CAT-01 |
| Artwork | Preferred printings/images, offline caching, set art, fallback, credits | F | C | Q-CAT-02 |
| Update/install | Compatible signed release detection, staged install, data preservation | F | C | Q-REL-01 |
| Cross-device data | Existing QR/USB/Wi-Fi, exact printing identity, safety preview | F + sync v2 | C + sync v2 | Q-SYNC-01 |
| Theming/settings | Persisted UI preferences, navigation, accessibility, shortcuts | F | C (appropriate subset) | Q-MIG-02 |
| Mobile scanner | Not currently a physical-card OCR feature; new capability | N/A initially | New C | Q-SCAN-01 |

**Do not call the migration feature-complete** while a Windows F target is missing or missing a documented replacement. Any deliberate removal requires explicit product sign-off and release notes.

## B. Required golden regression cases

### Identity and collection correctness

- **Q-DATA-01:** A 25,000-line collection import/search round-trip retains unique physical inventory lines and all quantities.
- **Q-DATA-02:** Split, adventure, double-faced, and alternate card-name normalization do not conflate distinct printings or faces; metadata failure cannot remove ownership.
- **Q-DATA-03:** Same card with 3 different printings/finishes survives Windows save, full backup, Android sync, offline quantity edit, reverse sync, and app restart.
- **Q-DATA-04:** Physical and Arena owned counts are disjoint; import of one never changes the other.
- **Q-DATA-05:** Set Progress counts only verified specific printings and agrees across platforms; an unresolved name-only card appears as unresolved, never as an owned card in every reprint set.
- **Q-DATA-06:** Price lookup failure leaves previously saved collection/printing metadata intact, reports stale values rather than converting unknown prices to zero.

### Catalog and performance

- **Q-CAT-01:** Kill the app mid catalog import; prior catalog remains queryable and next sync retries safely. Inject network timeout, HTTP 429, malformed JSON and storage-full states.
- **Q-CAT-02:** Artwork cache is bounded, doesn't download 1000 offscreen images, has no permanent memory growth after list scrolling.
- **Q-PERF-01:** Search, detail, set summary and startup meet documented warm/cold p95 budgets; results are compared against existing releases, not only static thresholds.

### Migration, synchronization, updates

- **Q-MIG-01:** Read-only dry-run migration reports exact quantities, deck counts, per-printing matches/unmatched, and saved art; a failed commit is recoverable.
- **Q-MIG-02:** Legacy localStorage/IndexedDB and Android SharedPreferences/state migration does not reset themes, deck history, or existing data; retains old profile until verified.
- **Q-SYNC-01:** Replaying same USB QR or partial data packet 10 times makes one logical edit; no duplicates. Concurrent desktop and Android edits on the same inventory line surface an explicit conflict when unsafe to combine.
- **Q-REL-01:** Update keeps data, signed identity, persisted settings and DB compatibility; on failed startup recovery is visible and working.

### Decks, analysis and Arena

- **Q-DECK-01:** Ownership/format/color-identity validation yields same golden results as the legacy app for a representative deck corpus.
- **Q-DECK-02:** Deck revision restore works with folder/tags, cover art, commander and card quantities intact.
- **Q-DECK-03:** Goldfishing actions on deterministic seeds match existing functional behavior.
- **Q-ANALYSIS-01:** Probability, ramp/land and card-role calculations match approved numerical fixtures within agreed tolerance.
- **Q-ANALYSIS-02:** Analyzer cache misses and disconnected network degrade gracefully without invented combo results.
- **Q-ARENA-01:** Log replay, deck assignment and match result calculation are stable and don't conflate Bo3 games with matches.

### EDH Lab

- **Q-EDH-01:** Complete paginated/virtualized commander registry from synced printings without artificial caps. Include colorless, WUBRG, pairs, 3-/4-color and five-color identities. Exact, contains-all and contains-any filters return mathematically correct pools; ownership filters never invent availability.
- **Q-EDH-02:** Random picker samples **only** the current legal, filtered commander pool; repeat rolls honor retained filters, optional no-repeat mode, and zero-result fallback. Paper Commander and Arena Brawl commander eligibility, current bans and special multiple-commanders rules are tested separately.
- **Q-EDH-03:** Starting EDH Lab deck from any selected/rolled commander, saving, reopening and exporting retains exact color identity, format, 100-card count, singleton constraints, commander configurations and owned/missing quantities; legalities and ownership warnings are visible, not silently 'fixed'.

### Camera OCR and companion

- **Q-SCAN-01:** Scan confirmation is mandatory; selected print ID, set, collector, finish and quantity are preserved. Duplicate clicks and background/resume don't produce duplicate inventory writes.
- **Q-SCAN-02:** No network and no camera permission produce usable fallback paths. Scanner photos aren't uploaded by default.
- **Q-SCAN-03:** Low-confidence collector numbers or foils produce confirmation prompts, not automatic selections.
- **Q-SCAN-04:** Scanner candidate recall measured against balanced real-photo test corpus; unexpected card cannot create incorrect printing ownership.

## C. Data-integrity reconciliation worksheet

Run before and after every migration or major sync change. Publish counts, not the user's private card list, unless explicitly approved.

| Metric | Before | After | Required relationship |
| --- | --- | --- | --- |
| Physical distinct names | capture | capture | equal unless explicitly edited |
| Physical distinct printing rows | capture | capture | equal or documented verified split |
| Physical copies (sum qty) | capture | capture | **exactly equal** unless explicit edit |
| Unresolved physical copies | capture | capture | never silently mapped |
| Arena copies | capture | capture | equal unless explicit Arena import |
| Saved decks | capture | capture | equal |
| Deck revisions | capture | capture | equal |
| Preferred art mappings | capture | capture | equal or migrated losslessly |
| Match/history events | capture | capture | equal |
| Duplicate event IDs | capture | capture | zero effective duplicate applies |
| Last verified catalog generation | capture | capture | present after interrupted update |

## D. Test environments

- **Windows:** clean profile; populated/migrated profile; low-disk fixture; reference mid-range Windows 11 laptop/desktop; optional Windows 10 only if chosen as a supported release target.
- **Android:** physical reference phone, emulator, no-network, limited storage, app background termination, app update from older signed APK and disconnect during QR/USB transfer.
- **Dataset tiers:** 1k, 5k and 25k physical inventory rows; 100k+ catalog printings; varying deck counts; ambiguous printing identities.
- **Test artifacts:** versioned schema fixtures, benchmark JSON/CSV reports, screenshot comparisons for UI, signed release hashes and restore validation report.

## E. Merge/release gates

1. Any change touching authoritative inventory/decks requires at least one new or updated golden regression.
2. A performance-sensitive UI change requires a before/after p95 report or an explicit benchmark exemption.
3. A protocol change requires fixture compatibility on both Windows and Android.
4. A release cannot proceed with unresolved P0/P1 data-integrity errors, hidden partial catalog corruption, or invalid signed updater packaging.
5. Completion requires realistic device validation; a green compile badge is only one prerequisite.

**Initial state of all Q-* cases:** Not yet run against the Arcane Vault Next architecture. Set each to Planned/In progress/Pass/Fail with evidence links as work proceeds.
