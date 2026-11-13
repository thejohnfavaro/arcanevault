# Arcane Vault Native Windows v0.9.51 — EDH Lab Reliability & Responsiveness

An engineering-focused preview built on v0.9.50. It keeps the proven UI and collection-aware analysis, without adding further clutter to Improve.

## Added

- A 220 ms quiet-period refresh for expensive EDH Lab analysis while typing in the mainboard. This collapses rapid keystrokes into one refresh rather than rescanning the card catalog on every change.
- Per-commander/format **weakly held, immutable predicate indexes** for two-card Synergy Chains and Three-card Engines. Expensive cached Oracle-text/regex pattern classification is computed once per eligible local catalog snapshot; sorting, draft membership, and unused-copy checks remain fresh for each current mainboard.
- Automated regression coverage for multiple edits of the same draft, missing-link status changes, complete pathways, physical/Arena inventory isolation, and the UI's scheduled refresh/stale-action guards.

## Changed

- Card text editing, existing manual deck actions, and the protected save process remain immediate. Only derived EDH Lab recommendations are delayed briefly while typing.
- During the short refresh delay, buttons that apply or navigate recommendations are disabled immediately; card-preview ownership counts continue updating.
- An explicit refresh for strategy, land target, or format replaces any pending typed-refresh pass.
- Closing the deck editor cancels outstanding analysis scheduling and image-loading requests.
- Recommendation panels recompute after a short pause, and the latest draft—not a cached previous status—controls candidate eligibility.

## Fixed

- A malformed custom land target now disables stale Swap Lab, optimization, role-browse, and health-browse controls instead of leaving previous recommendations actionable.
- Following a health card suggestion fails closed if the current custom land target is invalid.
- Cached analyzer predicate matches cannot carry paper inventory, legality, commander, or Arena-availability assumptions across catalog snapshots; all draft-dependent ownership classifications remain live.

## Removed

- Nothing. All previous EDH Lab panels, deck editing, collection imports, backups, and update/release workflows remain available.

## Safety and performance caveats

- Predicate indexes use ConditionalWeakTable so old snapshots can be reclaimed when unused; no database writes or network requests.
- The 220 ms quiet period is a responsiveness optimization, not a guarantee of a particular frame-time or benchmark result on every machine.
- No automated changes to the collection, deck, card inventory, or saved strategy. The separate Review & Save flow remains authoritative.
- All synergy matches remain heuristic possibilities, never rules-engine-proven combos or guaranteed wins.

## Local acceptance

1. Update with **Native Updates → Check for updates** or use the portable build.
2. Open an EDH Lab deck and type several edits in the mainboard quickly. The card text/caret and owned-count preview should react immediately, while heavy Improve panels settle after typing pauses.
3. During rapid editing, ensure stale **Apply Swap**, **Inspect owned candidate**, and missing-link browse controls cannot be used.
4. Test a known two/three-card synergy, add its missing piece, then confirm it changes from **Owned missing link** to **Both/All three in deck**.
5. Test a second commander and Arena Brawl. Suggestions and inventory should not inherit results from a previous format or commander.
6. Temporarily enter an invalid land target (outside 20–60) and verify recommendation buttons are disabled; correct it and confirm analysis resumes.
7. Expand multiple Improve sections and resize the window; verify scrolling, artwork, card preview, and the existing Save flow.
8. Confirm all collections, previously saved decks, and full backup paths are untouched.

Release gates: Windows build, .NET 10 native regression, checksummed portable ZIP, versioned release publication, user on-device acceptance. **Preview, not v1.0.**
