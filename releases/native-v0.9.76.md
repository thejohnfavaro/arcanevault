# Arcane Vault Native Windows v0.9.76 — Commander Synergy Package Quick Add

## Added
- **Commander Synergy Packages → double-click a real candidate row to add one locally legal copy** to the unsaved mainboard. A single click remains card selection and preview only.
- **Add selected package card** button in the package pane as a clear, accessible alternative to double-clicking. The button enables only for a current, locally eligible, not-yet-drafted candidate.
- The package panel explicitly explains its controls: "Single-click to preview · Double-click an eligible card to add". A missing/unowned suggested card may be intentionally drafted without being marked as owned; the regular save preflight still reviews shortages.

## Changed
- Commander Synergy Packages now uses the **same authoritative verified catalog, copy-limit and unsaved mainboard quick-add code** as the Cards browser. Double-click adds at most one copy; extra basic-land or special allowed copies still use the existing Add (+1) or mainboard manager.
- Selecting an **In deck** package entry never adds a duplicate. A missing/unknown/off-identity/not-yet-verified package entry cannot bypass current commander/format eligibility just because it appears in a recommendation report.
- The Add button disables when an entry has already been added, is stale, is in the deck, is locally ineligible, or the source Text decklist is malformed. Actual add rechecks all constraints before editing the unsaved mainboard.
- Existing v0.9.75 Cards-browser single-click previews/double-click quick-add and v0.9.74 mainboard artwork/search continue unchanged.

## Fixed
- The v0.9.75 interaction improvement was limited to the Cards ListBox. **Commander Synergy Packages was explicitly preview-only**: clicking showed a card preview but there was no direct add action. This now works consistently without forcing a jump to the Cards tab.
- Package previews no longer imply the pane cannot make manual deck edits; double-click or the explicit button is the only package-origin add gesture.

## Removed
- No ownership, collection, card metadata, artwork, printings, saved deck rows, deck backups or revisions were removed.
- No automatic draft edits on single selection, package theme changes, scrolling, or recommendation-refresh events.

## Windows acceptance
1. Open EDH Lab → Improve → Commander synergy packages. Single-click a listed **Owned option**. Confirm card artwork and exact Oracle details update, but the unsaved mainboard **does not** change.
2. Double-click that row. Confirm exactly one copy appears in Mainboard Manage and Text and the package action becomes unavailable for that already-drafted card.
3. Single-click a different **Owned option** and use **Add selected package card**. Confirm one copy enters the unsaved mainboard, preserving color identity and format eligibility.
4. Select an **In deck** entry. Confirm Add is disabled and double-click does not increment any quantity. Basics/multi-copy exceptions still require deliberate mainboard or Cards Add (+1) operations.
5. Inspect a **Missing** suggestion. It can be added once if locally legal, but its owned quantity remains zero and the normal missing-copy review still applies at save.
6. Click empty package-list space and scrollbars, or change mechanic filter. Verify no card is added. An invalid deck Text line must make Add unavailable until repaired.
7. Verify the Cards-tab preview/double-click logic, Manage thumbnails, typed search, save preflight, Deck Library fast return and collection-preservation behavior are unchanged.

**Scope:** Native Windows preview v0.9.76, requires green native/WPF and Windows CI plus interactive regression before stable 1.0.