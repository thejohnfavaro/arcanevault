# Arcane Vault Windows v0.9.226 (Preview)

## Added
- Independently scrollable, read-only ownership shortage report on the Cards tab in both the saved-deck editor and the embedded EDH Lab draft.
- Regression coverage asserting that both editors retain a bounded, vertically scrollable shortage panel.

## Changed
- Replace the clipped 100-pixel-high shortage TextBlock with a wrapped TextBox that can be scrolled to the final shortage, without changing ownership calculations or the editable card list.

## Fixed
- Long physical or Arena ownership shortage lists were cut off at the bottom and could not be inspected in full.

## Removed
- Nothing. Card counts, proxies, deck drafts, saved collections, and backups are unchanged.

## Verification
- Run the Windows native compile and regression suite, then verify the published installer and checksums.
- In both the Deck Library saved editor and EDH Lab draft, open a deck with many shortages. Scroll the shortage area to the last entry while keeping Add/Remove and the deck summary reachable.
- Confirm ownership pool switching refreshes the visible shortage text and does not edit the saved deck or collection.
