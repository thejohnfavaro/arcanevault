# Arcane Vault Native Windows v0.9.23 (Preview)

Deck Studio usability: inline read-only card previews, precise copy edits, and safer deck organization.

## Added
- Inline card-preview panel within the native Deck Editor: selected card name, selected inventory's owned quantity, current mainboard quantity, official card type, mana value, color identity, and Oracle text.
- Illustrative official Scryfall artwork is loaded only after a card is selected. It uses the existing trusted-URL, size-limited cache. Arena previews use an Arena-available representative image instead of a paper-only default.
- Remove one selected mainboard copy with a single button, deleting only the final copy's line.
- Alphabetically sort an unsaved mainboard with a single button while preserving card names and quantities.
- Regression tests for split-card names, draft-only removal, malformed inputs, sort ordering, preview control wiring, cancellation and Arena-art scope.

## Changed
- Deck Editor now arranges the editable mainboard, owned-card picker and compact card preview side by side.
- Analyze deck is grouped with import/export actions, retaining the existing expandable mana/color/type analytics.
- Preview images are loaded asynchronously, cancel previous selections, and debounce fast keyboard navigation to avoid unnecessary downloads.
- The preview pane explicitly says displayed Scryfall art is illustrative, not evidence of an owned printing.

## Fixed
- Removing or sorting card quantities never changes physical or Arena ownership.
- Invalid decklist lines are rejected before a sort or removal can rewrite draft text.
- Late card-preview requests are discarded after selection changes or the window closes.

## Removed
- No native collection, catalog, Set Progress, deck revision, import/export, updater or recovery features removed.
- No new data schema, card printing inference, automatic deck saves or background art prefetching.

## Verification
- Open Deck Library → Edit deck, switch between Physical and Arena, and select owned cards; ensure the preview shows correct local text and representative art.
- Rapidly change selection and search while images load; verify stale artwork never appears for another card.
- Add (+1), Remove (−1), Sort A–Z; verify only the unsaved mainboard changes and live stats stay consistent.
- Save after existing preflight, reopen, and verify that collection quantities and deck history remain preserved.
- Run native test and Windows build workflows before publishing this preview.
