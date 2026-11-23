# Arcane Vault Windows v0.9.225 (Preview)

## Added
- A separate taskbar entry for the saved-deck editor, so the editor and Card Catalog can be switched independently with Alt+Tab or the Windows taskbar.
- A single-active-editor guard that preserves the current unsaved draft when the user attempts to open another saved-deck editor.

## Changed
- **Deck Library → Edit selected deck** and **Add deck** now open a non-modal editor. The main Arcane Vault window stays interactive for browsing the Card Catalog, collection, and printings while the unsaved draft remains open.
- Successful deck saves close the independent editor and continue through the existing verified Deck Library refresh path.

## Fixed
- Clicking the main window while editing a saved deck previously played the Windows warning sound and flashed the title bar because the editor used `ShowDialog()`.
- The modal-only close result has been replaced with an explicit close after a verified save; cancel also closes the independent editor safely.

## Removed
- Nothing from saved decks, recorded printings, ownership, backups, Arena match data, or application navigation.

## Verification
- Run native Windows compilation, regressions, standalone packaging, checksum verification, and installer publishing via GitHub Actions.
- Open a saved deck and change its text without saving. Switch to the Card Catalog using the taskbar or Alt+Tab, inspect several exact printings, return to the same editor, and verify the unsaved change persists.
- Attempt to open another deck while the first editor is active; ensure the original remains accessible and unchanged.
- Save a deck and verify the correct Deck Library entry refreshes without a full inventory reload.
- Confirm Cancel or the window close button discards only the unsaved draft, without changing persisted collection data.
