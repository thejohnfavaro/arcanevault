# Arcane Vault Windows v0.9.201 (Preview)

## Added
- **Retry selected artwork** in Deck Library. Select a saved deck and click it to re-resolve its cover from the local catalog, invalidate only its disposable downloaded image, and reload the visible tile.
- Deck Library status now distinguishes a missing official image URL from a resolved URL whose image still fails to render. Hover a blank cover to see the download/decode failure.

## Changed
- Artwork retry preserves the saved card, chosen official printing, match history, deck identity and collection. The update does not change Ranked Standard W/L tracking.

## Fixed
- A failed deck cover can now be refreshed directly without resaving the deck or restarting Arcane Vault. The explicit status helps distinguish catalog metadata problems from image download problems, rather than silently displaying an unexplained placeholder.

## Removed
- Nothing.

## Verification
- Select the saved Mono-White Auras deck with Custom artwork set to Optimistic Scavenger. Click Retry selected artwork; confirm the status identifies the selected card and whether an official URL was resolved.
- If a URL resolves but no image appears, hover over the blank deck cover and capture its diagnostic tooltip.
- Confirm the saved Custom selection remains after reopening the editor. Check XENEON EDGE separately after its next snapshot.
- Run Windows native CI and only install after the installer job passes. This release instruments and enables recovery of unresolved artwork; it is not yet an independently verified end-to-end artwork fix for this user's installation.
