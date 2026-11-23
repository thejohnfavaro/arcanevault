# Arcane Vault Windows v0.9.221 (Preview)

## Added
- **Previous card** and **Next card** buttons in both Windows visual deck editors. Navigation skips expandable card-type headers, wraps through the currently visible filtered cards, and scrolls the target into view.
- **Clear search** control for the Manage tab without changing deck contents or the current saved workspace.
- Keyboard shortcuts limited to the visual mainboard: **Ctrl+F** focuses its search box, **F3 / Shift+F3** goes to the next/previous visible card, and **Enter** opens full card details. **Escape** clears a nonempty mainboard search while the search field has focus.
- Headless navigation tests for section headers, wrapping, no-selection behavior, and empty/header-only displays, plus UI regression checks for both saved-deck and EDH Lab editor variants.

## Changed
- The existing selected-card preview, quantity controls, and detailed-card window remain authoritative. Navigation chooses existing rows and triggers the same selection handling as a mouse click.
- Keyboard commands are scoped to the Manage list and search box, not the text decklist, other filters, or global app shortcuts.

## Fixed
- Repeated clicking and scrolling to inspect consecutive card details or move through search results in a type-grouped mainboard.

## Removed
- Nothing. No changes to deck data, unsaved text, ownership, printings, snapshots, analytics, Arena history, or Xeneon Edge export.

## Verification
- Run the Windows native regression suite and installer packaging.
- Open We Are the Borg in the saved editor, switch to Manage, search for a card type, then use F3/Shift+F3 and the navigation buttons. Confirm section headers are skipped and the preview follows selection.
- Use Ctrl+F while the Manage list has focus, Escape to clear the mainboard search, and Enter on a selected card to open details. Verify typing in the Text tab and unrelated search controls is unaffected.
- Check the same behavior in the embedded EDH Lab editor, and verify no deck quantities change until explicitly edited and saved.
