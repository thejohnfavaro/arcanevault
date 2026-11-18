# Arcane Vault 1.0 readiness audit — 2026-11-17

**Status: NOT RELEASE READY.** This is an engineering inventory and verification plan, not a declaration of user-data or on-device acceptance.

## Current scope

- Windows native: WPF/.NET 10, signed-status **unsigned preview**, versioned installer, self-contained portable build, immutable prerelease artifacts and updater/launcher.
- Android: Android SDK 35, Java 17, versioned **signed release workflow** using CI secrets. Debug and production builds cannot be substituted when signing certificates differ.
- Peer inventory sync: baseline-aware reconciliation and reviewed merge; the source code explicitly prohibits unreviewed inventory overwrite. QR/USB transfer occurs over trusted local transport.
- Arena match tracking: read-only player-log ingestion into a separate match-history database. The desktop exporter supplies Stream Deck and XENEON EDGE.
- Deck Analytics: independent Windows workspace reading native-first confirmed results; Android reads a separate, explicitly imported match-summary attached to peer exports. Android does not directly parse Windows Arena logs and does not create matches.

## Sync resilience milestone — November 18, 2026

Implemented in **Windows v0.9.150** and **Android v2.1.62**:

- Windows peer UUID is created once using exclusive atomic file creation. Concurrent app/launcher startup is regression-tested; invalid persisted identities are not silently regenerated.
- Android peer ID is validated as a complete nonzero UUID, and identity creation is synchronized across peer actions. Invalid saved values fail closed instead of breaking the established relationship.
- Stored peer checkpoints are structurally verified before they are considered usable baselines: expected peer identity, valid peer envelope, schema, revision metadata and required inventory groups.
- Android now enforces the 30 MiB maximum against the **whole** peer envelope on export and review, matching Windows.
- Existing inventory merge and conflict safety gates are intentionally untouched: no implicit deletions, no automatic sync authority, and no changes to the Windows match-history or Xeneon Edge integration.

**Evidence:** Repository regression checks are added for concurrent Windows peer ID creation, checkpoint tampering, Android malformed IDs, optional analytics isolation and malformed baselines. CI success demonstrates build and automated regression coverage only, not real-device persistence or interrupted-transfer recovery.

**Still open:** Full Android two-store (native inventories plus WebView decks/artwork) transactionality, signed production upgrade validation, end-to-end recovery from interruption, authenticated peer pairing, and mobile analytics freshness/automatic differential match sync. These remain 1.0 readiness gates.

## Priority 0 — must verify before 1.0

| Area | Current safeguard | Remaining acceptance test |
| --- | --- | --- |
| Backup / restore | Native workspace snapshots and recovery journals exist | Restore a real anonymized multi-device collection after interrupted update, storage failure, and interrupted peer-merge; compare exact owned printing counts and saved decks |
| Sync conflict | Reviewed three-way merge and persistent shared baseline | Simulate concurrent physical/Arena/deck edits on both devices; verify one-click Windows/Android authority decisions, conflict rollback, and replay idempotency |
| Android upgrade | Signed release workflow verifies APK signature | Install from existing production-signed version to candidate with no uninstall; confirm no collections, app preferences, decks, widgets, or progress lost |
| Windows upgrade | Preview releases and permanent launcher | Upgrade from previous installed version in a non-default install path; verify restart, rollback, uninstaller, pinned launcher, and existing data retention |
| Match correctness | Arena-match IDs deduplicated; manual assignments protected | Verify two consecutive Arena sessions, Arena restart, log rotation, unknown/no-ID match, same-name decks, and corrected deck associations; compare native history vs Stream Deck vs EDGE |
| Safety and auditability | Optional analytics peer field is separated from ownership data | Confirm every sync preview/merge ignores analytics for ownership conflict and only imports it after explicit opt-in; reject corrupted/oversized summaries |
| Offline resilience | Local SQLite and cached catalogs | Exercise airplane/offline mode, slow Scryfall, missing art, full device storage, inaccessible logs and catalog corruption without losing saved state |

## Priority 1 — polish and parity

- Finish actual commander-cover artwork in dedicated Deck Analytics. The current report uses stored deck names; artwork needs cache-fallback and offline validation.
- Android analytics imports on an explicit peer-preview action. Full **automatic differential match-history propagation** is not yet implemented. The initial parity feature copies a bounded, read-only set of confirmed outcomes; Android does not write match history back to Windows.
- Add stable native deck identity into analytics export to differentiate genuinely distinct decks with identical display names; current view groups by deck name.
- Support deck rename without splitting historical statistics. Preserve exact match-to-native-deck identity in all platforms.
- Complete UI/accessibility review: resize to 1280×720 and 4K, high-contrast/dark themes, keyboard and touch, scrolling, long deck names, empty states, and screen reader labels.
- Consider verified sample-size/confidence labels and visual tooltips on charts; current small-sample badge is a warning only, not a formal confidence interval.
- Replace stale Android README version references to reflect the actual Gradle version, plus unify release documentation.

## Tests required for release candidate

1. Both platform CI suites green at an exact release commit.
2. Source-verified snapshot/restore invariants and post-restore hash checks.
3. Signed Android update on the user's installed channel; unsigned/debug artifacts are never promoted as release updates.
4. End-to-end peer sync on two physical devices in both directions, with and without conflicts.
5. Arena event-to-W/L-to-StreamDeck/EDGE smoke test on physical hardware and screenshot/parity check.
6. No user-data mutation while opening Deck Analytics, importing analytics, or reviewing matches.
7. Audit logs and warnings exist for every skipped match and declined merge, without writing opponent identifiers.
8. First-run, zero-collection, offline, and upgrade-from-legacy flows tested.

## Release gate

Do **not** tag a stable 1.0 until priority-0 tests have evidence and known priority-1 omissions are explicitly accepted. Existing GitHub CI compiles and performs regression checks, but it cannot substitute for interactive hardware tests or an actual production-signed Android upgrade.

## Architecture boundary

**Arcane Vault Windows match-history SQLite is authoritative for Arena matches.** Stream Deck/XENEON EDGE receive calculated display snapshots. Android receives an explicitly confirmed read-only analytic presentation of verified matches within the optional peer envelope field `matchAnalytics`. The peer envelope's `data`, `revision`, schema and inventory merge contract remain unchanged.
