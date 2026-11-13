# Arcane Vault Native Windows v0.9.65 — Inspect cards without leaving Deck Library or EDH Lab

## Added

- **Saved Deck Library inspector:** Selecting a saved deck now opens a persistent, read-only panel alongside the virtualized deck list. It shows the saved mainboard's quantities and card names, the deck's format/size, and a selectable card preview with cached official artwork, card type, WUBRG/colorless identity icons, and Oracle text with official inline symbols.
- **Saved-mainboard card navigation:** Select different cards inside the deck inspector to update the same preview. Double-click a card or use **Full card details & printings** when the larger view is genuinely needed. Closing the inspector restores the full deck-list width.
- **EDH Lab Commander inspector:** In the Commander Browser, selecting or randomly rolling a commander shows its artwork, cached type, name-level inventory indicator, official color-identity symbols and readable Oracle text in an adjacent panel. The commander list remains virtualized and paginated. Full card details remain an explicit button action.
- **Shared native Oracle reading component** for Deck Library, Commander Browser, and the existing Deck Studio preview. It displays authentic Scryfall mana SVGs through the same cached native renderer as Card Details. Uncached symbols retain their exact `{W}`/hybrid notation for offline readability.
- Regression assertions for inspector wiring, source/selection safety, stale async catalog lookup guards, read-only behavior, and editor Oracle integration.

## Changed

- **Deck Studio's existing inspector** now uses the same borderless, selectable, official-symbol Oracle reading surface. Its ownership-aware card preview and EDH strategy/optimization workflows are unchanged.
- Deck Library card details are loaded **only for the currently selected mainboard card**, never for every saved deck or off-screen list row. Quick deck-to-deck navigation discards out-of-date asynchronous catalog results.
- Commander Browser uses its **already loaded local, read-only commander index** to update selected artwork and Oracle text instantly; it does not fetch every candidate's artwork over the network.
- Open full details from Deck Library or Commander Browser is read-only relative to the user's owned printing inventory. Mainboard quantities describe saved deck entries, **not proven inventory ownership**. Existing Deck Studio edit/save remains the only path for deck edits, including its native recovery protection.
- All existing Deck Library operations (edit, EDH Lab edit, duplicate, delete, favorites, search) and EDH Lab formatting, random selection, paging and filters remain intact.

## Fixed

- Selecting a saved deck previously showed only its cover and summary; inspecting individual included cards required opening the deck editor or another window.
- Commander browsing previously displayed a text-only list with no persistent card details beside the selected candidate.
- Deck Studio's Oracle preview was plain text without the official inline mana symbols already available elsewhere.

## Removed

- No physical/Arena ownership, deck entries, revisions, preferences, backups, card metadata, or file data were removed.
- No redundant second card-preview or extra per-card windows are required during ordinary browsing.

## Windows acceptance

1. Open **Collection & Deck Library → Deck Library** and select one of your saved decks. Verify its inspector appears, listing exact saved mainboard quantities and card names; selecting another saved deck updates the same panel.
2. Choose several mainboard cards in sequence. Artwork, type, color-identity symbols and Oracle text should refresh in place without opening a new window. Select rapidly to confirm late results never overwrite the last selection.
3. Double-click a mainboard card to explicitly open its full details; close the window, continue browsing and hide the inspector to reclaim space.
4. Try an imported saved deck with unsupported records. Its list and warning should remain honest and read-only, not silently claim any unverified printing or quantity.
5. In **EDH Lab → Commander Browser**, select a commander, change selection, use Surprise Me, and page the results. Inspector card/Oracle/icon data should follow the active selection, not create additional windows or modify the commander draft.
6. Open the full details window from the commander inspector, then return to the filtered list and build a deck through the existing explicit draft action.
7. In **Deck Studio**, click a card in the owned pool or mainboard to verify the existing preview renders Oracle symbols in the new borderless reading surface. No automatic deck change should occur.
8. At different window sizes, verify that the saved-deck inspector's internal contents scroll, the full-details button remains visible, and the deck/commander lists continue to virtualize their rows.
9. Confirm the main Card Catalog inspector, shortlist, import, collection backups, native updater and protected deck changes all continue to function.

**Scope:** Native Windows v0.9.65 preview. A read-only browsing enhancement to Deck Library, Commander Browser and existing Deck Studio. CI compilation and regression tests are required; final DPI and cached-artwork behavior remain on-device acceptance items.
