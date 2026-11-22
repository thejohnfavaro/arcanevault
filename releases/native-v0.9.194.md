# Arcane Vault Native Windows v0.9.194 (Preview)

## Added
- Responsive multi-column Deck Library: saved deck cards now wrap horizontally into as many columns as the available viewport allows.
- Dedicated fixed track alignment for name, commander, format, folder, bracket, AI review and copy/revision statistics.
- Regression checks for responsive tile flow and fixed card baselines.

## Changed
- Deck cards use a consistent 500 × 167 pixel logical layout, with an individual 512-pixel selection container; selected highlighting no longer extends through an empty full-width row.
- Decks flow automatically from two or more columns down to one when the window narrows or the saved-deck inspector takes space.
- Deck Library uses a nonvirtualized wrapping items panel rather than virtualized linear rows. The existing on-screen art cache and bounded-image loading remain unchanged.

## Fixed
- Saved decks with additional bracket badges or AI review metadata could push labels and card counts out of alignment.
- Each saved-deck widget previously occupied a whole row, wasting usable horizontal space.

## Removed
- Nothing. All collection, ownership, deck printing, editor, analytics and sync data remain unchanged.

## Verification
- Open Deck Library with at least four saved decks and a wide window; cards should appear in two or more columns when space permits.
- Open/close the inspector or resize the window; cards should wrap into fewer or more columns.
- Select decks and verify only the selected card has selection highlighting. Verify favorite, bracket and AI review badges never shift the title or count baselines.
