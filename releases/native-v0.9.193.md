# Arcane Vault Native Windows v0.9.193 (Preview)

## Added
- Regression protection for commander-specific printing artwork in the Deck Library tile, saved-deck inspector, and full card-details window.

## Changed
- The saved-deck inspector now uses the deck's exact saved `commanderDisplayPrintId` for the commander preview instead of loading the catalog's generic default art.
- Full card details opened from the saved-deck inspector use the same deck-specific printing preference for their initial artwork.

## Fixed
- The same Etali deck could show two different printings at once: one on its deck tile and another in the saved-deck inspector.
- The selected deck artwork preference now drives both surfaces consistently without changing global preferred artwork or asserting an owned printing.

## Removed
- Nothing. Existing saved decks, collection ownership, Arena inventory, win/loss records, and independent card printing preferences are unchanged.

## Verification
- Open the 'Maybe dinosaurs' saved Arena Brawl deck after choosing its commander printing.
- Select the commander in the saved-deck inspector and compare its artwork to the deck tile; both should show the selected printing.
- Open Full card details & printings and confirm the initial artwork uses the deck's selected printing.
- Change or reset the commander printing in EDH Lab and save; verify the tile and inspector remain consistent.
