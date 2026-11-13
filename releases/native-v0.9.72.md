# Arcane Vault Native Windows v0.9.72 — EDH Lab deck-aware card browser

## Added
- **Live In deck ×N indicators** alongside every eligible card in the EDH Lab Cards browser. The count reflects the current UNSAVED mainboard, including existing basic land quantities.
- **Deck status filter** in Cards → Filters & commander matches:
  - **Available · not in deck:** eligible cards with at least one recorded copy and no matching mainboard name.
  - **In deck:** eligible names already in the unsaved mainboard, including unowned/missing names, to make shortages visible.
  - **All eligible:** existing legal, color-identity-filtered browser subject to its normal recorded-only toggle.
- **Live result context** below the search box: selected filter, number shown, count of distinct draft names and an explicit indication if the UI reached its first-200-results cap.
- **Selection preservation** when the draft/filters change, suppressing redundant card artwork reloads. Filters apply to the FULL eligible local catalog before the 200-result browser limit.

## Changed
- After double-click-to-add or Remove (-1), the in-deck badge and filtered results refresh immediately. Hand-edited mainboard text uses the existing 220 ms debounced draft-analysis pass.
- Commander-synergy recommendation browsing respects the selected deck-status view instead of ignoring it.
- A malformed or mid-edit mainboard temporarily suppresses potentially incorrect presence labels and displays a correction message, without changing the source text.
- No changes to v0.9.71 singleton double-click, basic-land Add (+1), ownership pool isolation, or the native preflight and recovery workflow.

## Fixed
- Users could not tell from the eligible-card list which cards were already present, and had to cross-reference the mainboard manually.
- Browsing a large local catalog could bury cards already in the mainboard behind the first 200 matches; status filtering now happens BEFORE the result limit.
- A browser list refresh no longer unnecessarily fetches the same artwork for a restored selection.

## Removed
- No collection rows, card metadata, stored printings, saved decks, backups, revision history, or cache data were removed.
- No automatic card edits or new saved-deck writes. Deck-aware labels are advisory UI state only.

## Windows acceptance
1. Open EDH Lab → Cards. The initial Available view shows locally eligible recorded names not yet in the unsaved mainboard.
2. Double-click a card. Confirm one copy is added; the row disappears from Available. Switch to In deck and see `In deck ×1`.
3. Repeat double-click in In deck: the quantity remains exactly one. Use Add (+1) only for basics or other allowed exceptions, and confirm `In deck ×N` updates.
4. Change mainboard text manually; verify counts update after the normal short draft-analysis debounce. Remove a card and see it move back to Available if recorded.
5. Switch to In deck with the Recorded-only checkbox still enabled. Confirm eligible but unowned names already in the draft remain visible as `missing`; ineligible/off-color candidates never appear.
6. Test search text, role filters, synergy-only, commander focus browsing, and the first 200 results. In deck must not be limited to names in the first unfiltered 200.
7. Enter malformed draft text: labels must not claim inaccurate membership. Restore valid entries and verify the browser recovers without resetting ownership.
8. Verify preview artwork doesn't flash/reload after each ordinary draft status refresh; check that changes stay unsaved until separate Review and save.

**Scope:** Native Windows v0.9.72 preview. CI is the code gate; user Windows UI regression still needed.
