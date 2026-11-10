# Arcane Vault Next — Phased Migration & Delivery Roadmap (RFC-001A)

**Status:** Planning baseline, not a calendar commitment. **Updated:** 2026-11-10.
**Read first:** TECHNICAL_SPEC.md and FEATURE_PARITY_AND_QA.md.
**Priority order:** (1) user-data safety, (2) measured speed, (3) full native Windows parity, (4) Android scanner and companion quality.

## Proposed delivery strategy

- Preserve the two shipping apps while building new modules behind isolated branches/feature flags. Never move live user data into experimental builds by default.
- Prefer **vertical slices** (database/service/UI/tests/migration) to a single massive rewrite.
- Preserve native Windows C# services where appropriate. Do not copy the HTML/JavaScript business logic into a different UI shell without separating it into testable modules.
- The companion is not a second full desktop: its purpose is collection lookup, scanning, precise owned-printing edits, deck access, and reliable sync.
- Provisional total for one experienced full-time developer: **approximately 20–30 working weeks (5–7 months)**, including overlap and integration. External security, signing, device, or migration issues can extend this. Each phase is a range, and phase totals must **not** be naively added because workstreams overlap.

## Phase 0 — Reliability audit and baseline (weeks 1–3)

### Deliverables
- Inventory all persisted data: Windows WebView2 storage, IndexedDB, existing SQLite, embedded UI state, Android native SharedPreferences and SQLite, legacy WebView-origin recovery and backups.
- Capture an **anonymized, authorized** baseline fixture. Do not commit real user's private collection or application backups.
- Benchmark warm/cold startup, local search, card detail open, set completion, scrolling, catalog download/index, app update checks, inventory edits, QR/USB transfer.
- Reproduce legacy failure cases: missing printing identifiers, split-card name mismatch, stale card cache, Android collection loss on upgrade, duplicate snapshots, interrupted download/indexing.
- Document supported OS versions, device profiles, disk and RAM budgets.
- Create a data-integrity golden fixture test runner and performance trend report.
- **Independent platform maintenance task:** assess/implement a .NET 8 -> .NET 10 LTS upgrade on the shipping desktop app with dedicated checks; .NET 8 support ends 2026-11-10. Avoid mixing it into an ownership-schema change.

### Exit gate
- Reproducible benchmark commands and a baseline report exist.
- At least one full backup -> restore -> exact count/printing/deck comparison passes on a disposable profile.
- Priority defects and unverified assumptions are recorded as issues.
- No user-data migration performed yet.

## Phase 1 — Domain contracts, SQLite ownership and golden tests (weeks 3–7)

### Deliverables
- New domain entities and validated transformations for card identities, printing IDs, inventory lines, decks, preferences, journaled operations.
- Versioned JSON schema and golden contract fixtures consumed independently by C# and Android Java.
- SQLite schema migrations with unique IDs, foreign keys, indexes, transactions, and a rolling-back migrator.
- Extract pure collection, set-progress and printing reconciliation from UI code; keep old UI operational.
- Distinguish authoritative owned data from cached catalog records and metadata.
- Explicit unresolved-printing representation rather than speculative assignment.

### Exit gate
- Same fixture produces identical quantity/identity/Set Progress totals on Windows and Android.
- Interrupted migration leaves last known-good data intact.
- No duplicate user records when import or operation is retried.
- Benchmark shows new SQL paths are at least as fast as current baseline on representative data.

## Phase 2 — Data migration + sync v2 (weeks 6–11)

### Deliverables
- Read-only extraction and dry-run migration UI for current Windows/Android stored records.
- Backup files with schema version, source provenance, hashes and reports. Staging + atomic cutover, verified rollback.
- Device pairing and sync v2: durable operation journal, idempotency, per-entity revision checks, conflict resolution, retry, tamper/incomplete-file detection.
- Compatibility import for schema-1 Windows/Android QR snapshots, visibly labeled as full replacement/legacy.
- Inspect exact printing identity counts before applying any import; do not silently discard known selected printings.

### Required test scenarios
- Initial sync and second no-op sync; offline edits on both devices; interrupted USB; QR retry; stale device; duplicate operation ID; disjoint quantity changes; conflicting SetQuantity; remove/re-add; card with no Scryfall ID; multi-printing ownership; split/adventure cards; disk-full or app-killed mid-commit.
- No test may depend on a network Scryfall request when testing durable ownership state.

### Exit gate
- Accurate diff preview and explicit user resolution for ambiguous conflicts.
- No double-counted edits or overwritten newer printings after retries.
- Crash recovery/rollback and full restore demonstrate exact data parity.

## Phase 3 — Native Windows vertical slices (weeks 9–19)

### Sequence
1. WPF/.NET 10 native shell: navigation, windows/dialogs, themes and accessibility, release channels, dependency injection.
2. Native Card Browser and Card Details: paginated/virtualized data and images, color/type/rarity filters, variants, ownership and card actions.
3. Native Physical Collection: multiple owned printings, quantities, finishes, preferred art, date/price sorts, import/export, pricing.
4. Native Set Progress: printing-confirmed counts, set imagery/icons, collector-number order, missing/owned filters.
5. Keep legacy WebView functionality reachable during conversion, in a contained compatibility view.

### Exit gate
- Visual and behavior parity snapshots for each migrated feature.
- Benchmarks meet p95 targets or have an explicit improvement plan and no significant regressions.
- No startup migration is destructive; users can roll back.
- Card Browser, Physical Collection and Set Progress run native without relying on a browser-hosted UI.

## Phase 4 — Android companion + card scanner MVP (weeks 12–20; overlaps Phase 3)

### Deliverables
- Single source of truth for physical ownership and deck-access operations on Android; offline edits queued reliably.
- Native scanner activity using CameraX and bundled ML Kit Text Recognition v2.
- Local printing-candidate lookup with name, set/collector, art and manual verification.
- Confirmation screen: selected exact printing, quantity, finish, destination, already-owned warning.
- Transactional save + undo + outgoing sync journal, entirely functional offline.
- QR/USB/Wi-Fi sync v2 status and conflict workflow.

### Pilot test corpus
- At least 200 real card images/frames spanning common/rare, old/new frames, multiple editions of the same name, foils/glare, small collector text, split/adventure/double-faced cards, promo IDs, and missing-catalog examples. Capture benchmark device, lighting and language.
- Include unknown/low-confidence results; **never auto-save an unconfirmed candidate**.
- Targets in TECHNICAL_SPEC.md are provisional until this pilot validates them.

### Exit gate
- Scanner cannot silently write ownership.
- Correct printings remain distinct through app restart and Windows sync.
- No duplicate items from replayed scans.
- Offline and permission-denied workflows degrade cleanly.

## Phase 5 — Full native Windows functional parity (weeks 16–26)

### Deliverables
- Deck Studio, Deck Library, deck revisions, favorites/organizers, validation, goldfishing, Strategy Workbench, upgrade tools, match analysis, and all current export/backup tools as native Windows views.
- Keep Arena-only PC log reading on Windows; expose relevant read-only stats to Android via sync where desired.
- Replace remaining browser-dependent state and workflows after golden tests pass.
- Retire the compatibility WebView for everyday use only when all existing workflows are accounted for; preserve last-resort migration/recovery access if needed.

### Exit gate
- Every feature in FEATURE_PARITY_AND_QA.md has a release-approved status.
- No data-loss or major feature-parity issues.
- Old backup restore still supported and tested.

## EDH Lab — Commander and Arena Brawl deckbuilding toolbox (Phase 5 product work)

**Product target — not a shipped feature.** EDH Lab is a dedicated workspace over the shared Card Browser, Deck Studio, ownership and legality services. It supports two independent rule profiles: **paper Commander** and **100-card MTG Arena Brawl**, with an optional separate Competitive Brawl legality profile if available. Every saved deck retains its rule profile; switching modes never silently alters or deletes a deck.

### Commander discovery and random picker
- **Browse all eligible commanders** from the current synced catalog (not just a 200-row preview). Search by name and ability text, with virtualized/paginated results and art previews.
- **Color-identity filters**: W/U/B/R/G plus colorless; exact identity, contains all selected colors, or contains any selected color. Include named pairs, shards, wedges, four-color and five-color combinations.
- Availability: all format-eligible, physical collection only, Arena collection only, and owned/unowned; distinguish a card being on Arena from the player actually owning it. Multiple printings display as one commander identity with selectable art.
- **Random Commander / Surprise Me** selects uniformly from the *currently eligible, filtered pool*; reroll preserves chosen filters. No result? Show an explicit empty state and reset suggestion. Provide favorites, recent rolls, and optional no-repeat-until-exhausted behavior.
- Commander eligibility follows the selected format rather than merely checking a `legendary` string: ordinary paper Commander commanders are legendary creatures or explicitly permitted exceptions, while Arena Brawl has its own allowed commander categories and ban list. Partner, backgrounds and other multi-commander constructions need explicit compatibility checks.

### EDH Lab build and optimization workspace
- Starting a build from any commander or random roll preloads the **correct color identity, format, card count and singleton rules**; supports legal multi-commander configurations.
- Filters card pool by actual format legality, color identity, Arena availability, and physical/Arena owned quantities. Never claim a card is owned by inferring a printing from its name.
- Deck roles, synergy packages, mana curve, color sources, ramp, removal, interaction, draw, board wipes and win conditions; adjustable goals for casual / Commander bracket / high-power play.
- **Collection-first recommendations** with visible owned vs missing quantities, wildcard cost for Arena, optional physical purchase/budget view, and warnings for illegal cards.
- One-click add/replace/remove, deck comparison, probability/goldfishing integrations, and import/export (Arena Brawl, Commander text formats) with rules-validated results.
- Android companion: browse commanders, randomize, view saved EDH Lab lists and make simple verified edits after sync v2 is reliable. Advanced optimization remains Windows-first.

### EDH Lab acceptance
- Entire catalog appears with no artificial result cap; filter math is correct for colorless through five colors. Zero results never causes random picker crashes.
- Random rolls cannot return illegal/banned or excluded commanders; rerolls use the same current filter set.
- Paper Commander and Arena Brawl rule differences, multi-commander exceptions, singleton counts, color identity, card availability, and exports have golden tests.
- Missing print IDs and missing card metadata are surfaced without fabricated ownership or legality claims; save and reopen preserves chosen commander(s), rule profile and all 100 cards.

**Sequencing:** design rules/contracts and UI information architecture after Phase 1; implement native EDH Lab in Phase 5 after authoritative inventory, deck persistence and format-validation services. EDH Lab must not delay P0 data reliability or native collection migration.

## Phase 6 — Hardening, regression and controlled release (weeks 22–30)

### Deliverables
- Threat review for localhost/server endpoints and device pairing; verified signed Windows updater and Android install/update identity.
- Automated Windows CI and Android device tests, replayable migrations, offline/interrupt stress tests, crash loops and performance comparison.
- Opt-in migration preview, staged rollout, rollback button, support diagnostics, accessible user documentation.
- Release notes distinguishing changed/added/removed workflows; retained user data validated after upgrades.

### Exit gate
- All P0/P1 data-integrity issues closed.
- 100% of critical golden paths pass (backups, restores, printing selection, Set Progress, deck saves, sync), no silent data reassignment.
- Versioned data migration works on two prior representative releases; signed update and rollback verified.
- Performance meets baseline and agreed target thresholds on reference devices.
- Pilot user acceptance for desktop and companion.

## Critical path and dependencies

1. Phase 0 measurements precede performance claims or tool/framework finalization.
2. Domain contracts and single-authority data store precede sync v2 and bulk UI port.
3. Migration and v2 journal precede two-way Android writes.
4. Scanner writes must go through the same validated inventory service as manual edits.
5. All native desktop screens must use shared services and golden fixtures instead of duplicating ownership logic.
6. No automatic UI cutover before verified backups and reversibility.

Parallel work: scanner UI mock/test corpus and WPF shell proof-of-concept can begin after the Phase 1 contract is stable. A single engineer should limit work-in-progress; dual-feature rush increases reliability risk.

## First six implementation tickets (suggested)

| ID | Priority | Work item | Done when |
| --- | --- | --- | --- |
| AVN-001 | P0 | Inventory persisted state and export full baseline | Every known store inventoried; verified backup + restore totals |
| AVN-002 | P0 | Build golden parity tests and perf harness | Repeatable CI command + baseline report on reference hardware |
| AVN-003 | P0 | .NET 10 LTS compatibility/spike | Supported target builds/tests with rollback plan |
| AVN-004 | P0 | Define printing-aware ownership schema and unresolved states | Both platforms pass fixture consistency tests |
| AVN-005 | P0 | Build non-destructive migration dry-run | Exact count/identity/deck comparison; no writes to old profile |
| AVN-006 | P1 | Prove Android camera OCR + local printing candidate lookup | Functional proof of concept, no inventory writes |

Next tickets: AVN-007 durable change journal, AVN-008 v2 sync/retry/conflicts, AVN-009 WPF virtualized browser, AVN-010 physical collection parity, AVN-011 Set Progress, AVN-012 scanner save flow, AVN-013 decks and strategy parity. EDH Lab follow-ups: AVN-014 full commander registry + color-identity filters + random picker, AVN-015 Brawl/Commander rules and collection-aware builder, AVN-016 native EDH Lab and Android companion handoff.

## Reporting cadence

For each PR or milestone, publish:
- scope and old/new workflow comparison;
- data migration/backup implications;
- test matrix and CI status;
- p50/p95 timing and memory comparison;
- known regressions and rollback instructions;
- explicitly what is not yet implemented.

A milestone is **not** "done" based on successful compilation alone. Device-only and user-data workflows need their own passing evidence.

## Explicitly out of scope for the first release

Cloud account/login infrastructure, automated online image matching services, full Android parity with every Windows analytical tool, marketplace transaction integration, binder-page multi-card recognition, and broad UI redesign unrelated to reliability/performance goals.
