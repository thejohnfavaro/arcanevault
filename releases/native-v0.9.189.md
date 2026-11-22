# Arcane Vault Native Windows v0.9.189 (Preview)

## Added
- Independent commander printing selection from the Commander row in the Mainboard Manager.
- Exact Scryfall printing preferences stored on saved decks as commanderDisplayPrintId, separate from mainboard entries and physical ownership.
- Deck Library cover artwork now honors each saved deck's selected commander printing.
- Regression checks for commander printing selection, default reset, unchanged collections, deck history, and transforming Etali front-face artwork.

## Changed
- Reuses the existing owned-first printing gallery for commanders in both embedded and standalone Windows deck editors.
- A commander printing choice is an unsaved cosmetic edit until Review & Save, and is preserved by the normal atomic save and recovery workflow.

## Fixed
- Commander row no longer disables the printing selector.
- Transforming commanders such as Etali, Primal Conqueror resolve Scryfall printing IDs even when the catalog card is named with front and back faces.
- Restoring default commander artwork is a valid standalone deck edit.

## Removed
- Nothing. No commander is inserted into the mainboard; no inventory or ownership change.

## Verification
- In EDH Lab, select Commander in the Mainboard Manager, choose an owned or display-only printing, then Review & Save.
- Reopen the deck and ensure the printing selection and deck cover persist, including a transforming card and a default reset.
- Check that saved deck count, mainboard quantities, snapshots and physical ownership are unchanged.
