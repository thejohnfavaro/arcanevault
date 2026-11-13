# Arcane Vault Native Windows v0.9.69 — Whole-deck impact previews

## Added

- **Projected whole-deck impact** in EDH Lab → Improve → Resolve missing cards. Selecting a recorded alternative now opens a read-only projection, not just a two-card comparison.
- **Before/after composition:** Tracks each of the existing blueprint's suggested role counts (lands, ramp, draw, removal, wipes, protection) with deltas and whether existing shortfalls shrink or grow.
- **Commander-mechanic signals:** Shows how many copies match the local commander text before versus after, with caution when a recognized match is lost.
- **Honest mana-curve comparison:** Shows current versus projected average mana value from known locally synced entries. Legacy or incomplete mana metadata is explicitly identified; it never invents unknown card values.
- **Ownership implications:** Shows Physical or Arena name-level covered and missing copy counts before and after the candidate swap, without reserving cards or mixing inventories.
- **Contextual ranking:** Alternatives with extra recognized roles now receive additional ranking credit specifically when those roles address a shortfall in the current unsaved deck. This uses a single baseline role analysis per shortlist, not full scans for each eligible card.
- **Regression tests** for shortages, known and unknown mana data, one-for-one mainboard preservation, insufficient available copies, deck-role deltas and ranking gap preference.

## Changed

- The explicit replacement confirmation re-runs the whole-deck projection from the current draft and includes it alongside the existing alternative rationale before any unsaved text edit.
- Recommendations continue to honor user-selected strategy, commander identity, format, inventory pool and actual recorded quantity. The projection is advisory, not a deck-strength or win-rate prediction.

## Fixed

- Previous comparisons couldn't tell whether a replacement gained an ability the *whole deck was actually missing*. Ranking now distinguishes additional tracked shortfall coverage from redundant extra role hints.
- Before-versus-after inventory and mana totals no longer require a user to perform and undo a test edit simply to inspect them.

## Removed

- No saved decks, collection rows, preferred printings, formats, back-ups, revisions, catalog or recovery files were modified or removed by this feature.
- No simulated game outcomes, card reservations, automatic swaps, purchases, or automatic saves were introduced.

## Windows acceptance

1. From a saved Commander or Brawl deck with missing cards, choose **Review shortages in EDH Lab**, select a missing mainboard card and one recorded alternative.
2. Below the two-card comparison, inspect **Projected whole-deck impact**. Verify total mainboard copies stay constant while role counts, tracked shortfalls and commander-mechanic matches show before/after numbers.
3. Where full mana values are available, verify projected average mana value. Where catalog data lacks them, verify the UI labels the curve unavailable rather than claiming it is zero.
4. Compare a candidate that adds a currently short role against one with redundant extra roles; read **Why this ranked here** and verify tracked shortages influence ranking without claiming strategic equivalence.
5. Confirm Physical Commander uses only physical recorded copies and Arena Brawl uses only Arena recorded copies. Coverage must decrease reported shortages only when the replacement is actually recorded.
6. Review the confirmation prompt; cancel and verify the draft remains unchanged. Accept and verify only one copy moves in the unsaved mainboard, then separately Review and save.
7. Test rapid selection changes, missing local catalog metadata, imported unsupported entries and resized Deck Studio panels. Projections must not contaminate selected cards or persisted collection/deck state.

**Scope:** Native Windows preview v0.9.69. Automated CI is a gate; manual on-device Windows acceptance still required before 1.0.
