# Arcane Vault Native Windows — v1.0 Release Readiness Checklist

Status: **candidate checklist, not a v1.0 release approval**.

### Verified Windows v0.9.22 Deck Studio smoke test — user reported 7/7 PASS

The installed native v0.9.22 passed all seven manually reported tests:
1. Open Deck Library and create/edit a saved deck.
2. Search owned cards and add one to the mainboard.
3. Switch between Physical and Arena ownership pools.
4. Verify live card counts and missing-copy warnings.
5. Analyze the mana curve, color identity, and primary card types.
6. Change a card and verify old charts are invalidated.
7. Review/save and reopen the deck.

This closes the **v0.9.22 Deck Studio smoke-test gate only**. It does not certify the new v0.9.23 card-preview UI, cached commander artwork, updater migration, remaining v1.0 feature parity, or backup restore on a real installation.
Baseline: published native v0.9.22; planned v0.9.23 Deck Studio usability update in PR #101. Android and the previously shipped legacy WebView Windows channel are outside this native Windows release gate.

## 1. Blockers and objective exit criteria

| Gate | Required evidence | Current status |
| --- | --- | --- |
| Source + regression | Latest `main` native Windows build and .NET 10 native tests both green, including release/visual/update checks | Required for each release; latest branch results must be inspected |
| Install/update | Checksummed GitHub preview ZIP, correct source SHA/version in manifest, side-by-side install, rollback path, no legacy profile writes | Automated fixtures exist; real update smoke test pending |
| Collection integrity | Import a real full backup, reopen, export and compare quantities, printing IDs, deck revision history, preferred art, unknown fields | Synthetic coverage exists; real collection acceptance pending |
| Large-data reliability | Stress a synthetic collection; verify original backup is byte-identical after import/reopen/export; record parse/import/open/export timings | Test added in hardening PR; CI pending |
| Functional coverage | Card Catalog, Physical + Arena inventories, Set Progress, deck library, import/export, sorting, filtering, details, preferred art, updater, native navigation | End-to-end manual matrix pending |
| Deck Studio | Create/edit, +1/-1, sort, ownership deficits, draft analytics, selecting card previews, save/reopen | v0.9.22 analytics available; PR #101 preview and editing pending merge + QA |
| Failure handling | Bad manifest, checksum mismatch, invalid archive, stale write SHA, missing metadata, interrupted/invalid import all fail closed | Automated coverage present; user-device failure smoke test pending |
| Performance | Native startup responsiveness, 5k+ owned rows and 25k+ catalog prints, scrolling/search and memory use stay usable on real hardware | Synthetic timings available; real hardware baselines pending |
| User acceptance | No high-severity regressions in ordinary usage for the native Windows release | Pending user validation |

A v1.0 release candidate must pass **all** gates. Green CI is necessary but does not replace a real Windows device smoke test.

## 2. Read-only acceptance protocol for a real collection

**Before anything destructive:** make a private full backup using the installed app and retain the original file, separate from any test build or version folder. Never upload real card/deck inventory to public GitHub.

1. Record existing app version, current native workspace and catalog paths, plus SHA-256 hashes of both.
2. Perform the upgrade through the native updater. Confirm new `native-version.json` version and source SHA match the GitHub release and that the executable installs in a new version directory.
3. Open the upgraded app. Verify the physical and Arena copy totals, printing counts, decks/revisions and Set Progress totals against the pre-upgrade baseline.
4. Use catalog filters and details on split cards and multiple printings. Verify physical-vs-Arena art scopes, preferred printing, and missing metadata fallback.
5. Create/edit/export a synthetic test deck. Review ownership shortages and analytics. Save/reopen and confirm expected revision history. Do not edit the user's production deck as a first test.
6. Export a full native backup to a *new* file outside both legacy and native workspace folders. Compare logical inventory and historical fields to the backup produced before the test. If there have been no intended edits, the source and exported full backups should be byte-identical.
7. Deliberately test a bad checksum/manifest against a **synthetic** fixture, not the user's production executable. Ensure the active app, catalog and recovery files are unchanged.
8. Record timings and outcomes. Flag any mismatch as release-blocking. Restore only from a verified recovery point using the explicit restore flow.

## 3. Feature inventory to compare with the last WebView release

### Must be present or have an approved alternative
- Card Catalog: search, filters, artwork/card details, legality, sorting and pagination/virtualized performance.
- Physical Collection: exact owned printings, quantities, finish, sorting, collection value and backups.
- Arena Collection: owned quantities, separate scope and Arena-only metadata where supported.
- Set Progress: release-date sorting, official set symbol, artwork-backed tiles, owned printing counts and missing-set filters.
- Deck Library: navigation, deck folders/favorites, create/edit/duplicate/delete, import/export and safe revision history.
- Data & Sync: reliable catalog detail/variant/legalities refresh, observable loading/error states and rate limiting.
- Updates & Recovery: SHA verification, version reporting, non-destructive install, complete backups, explicit fresh start and recovery.

### Post-v1.0 backlog (do not gate stable native release)
- EDH Lab commander/Brawl toolbox, random/color-identity commander browsing and recommendation logic.
- Android companion feature expansion or cross-platform sync changes.
- Optional UI flourishes that do not affect data safety or core navigation.

## 4. Test evidence to attach to release-candidate PR

- Commit SHA and both Windows CI workflow links.
- Exact native release tag, assets and SHA-256 file.
- Synthetic scale canary output (physical/Arena rows, timing and byte-identity).
- Manual Windows acceptance results with pass/fail notes.
- Known issues triaged with severity/owner.
- Explicit approval that no unsupported original-app feature was silently dropped.

**Rule:** never call a native build `v1.0` solely because it compiles. The user's collection, saved decks and recovery options take priority over additional features.
