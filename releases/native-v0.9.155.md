# Arcane Vault Native Windows v0.9.155 (Preview)

## Added
- Regression checks that both EDH Lab mainboard thumbnail loaders resolve Arena-style printing suffixes before catalog and Arena-image queries.

## Changed
- EDH Lab Manage-list card art now looks up the official Scryfall artwork by normalized card identity for saved deck entries such as `Assembly Specialist (TRK) 4`, while retaining the original saved name and printing selection.
- The fix applies to the embedded EDH Lab and the standalone deck editor; thumbnail requests still use the bounded artwork cache and remain cancellable when scrolling.

## Fixed
- Previously blank mainboard thumbnail images for saved Arena decklists whose names include `(SET) number` annotations, even after the full-size card preview was fixed.

## Removed
- Nothing. Decks, physical/Arena inventories, proxies, collection values, and card printing preferences are unchanged.

The native Windows release is a preview. Official artwork still requires a locally synced catalog and a permitted official artwork URL. Keep a full private backup before updating.
