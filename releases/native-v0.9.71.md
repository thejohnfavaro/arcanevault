# Arcane Vault Native Windows v0.9.71 — EDH Lab singleton double-click

## Added
- **Double-click an eligible card in EDH Lab → Cards to add one unsaved mainboard copy.** The same gesture is idempotent: if the card is already present in any quantity, the deck is not changed.
- The on-screen card browser heading and tooltip now explain this gesture, with a separate hint for extra basics and other allowed multi-copy exceptions.
- The action is confined to an actual list row and does not accidentally add the previously selected card when double-clicking blank list space or a scrollbar.

## Changed
- Double-click and manual **Add (+1)** now have separate behavior in EDH Lab. The normal button keeps the supported per-card copy limits and remains available for basic lands and cards with special copy-count text.
- If a card is already in the unsaved mainboard, double-click gives a short informational status instead of incrementing the quantity or opening a modal error.
- EDH Lab quick-add waits for the local format/color-identity-verified catalog before acting; stale or unknown candidate names cannot bypass eligibility checks.

## Fixed
- Previously, **MouseDoubleClick** and **Add (+1)** shared the same increment operation, potentially adding multiple copies to basic lands or other cards with non-singleton copy allowances even when the user intended a quick single addition.
- Repeated or case-variant double-clicks now leave existing quantities and mainboard text exactly as-is.

## Removed
- No deck cards, collections, saved decks, catalog entries, card printings, recovery revisions, caches, theme settings or backups were removed.
- No automatic save, catalog or ownership write, or changes to the special Commander rules for basic lands and permitted multi-copy cards.

## Windows acceptance
1. Open EDH Lab for a verified Commander or Arena Brawl deck. Under **Cards**, double-click one eligible, absent card. Confirm the unsaved mainboard gains exactly `1 Card Name`.
2. Double-click that same card several more times. Confirm the mainboard copy count remains **1**, without opening a new window or modifying collection ownership.
3. Enter `4 Forest` manually, then double-click Forest in the browser. Confirm it remains `4 Forest`. Select Forest and use **Add (+1)** separately; confirm the manual button still allows an additional basic land.
4. Try other legal special multi-copy cards. Double-click must leave any already-present name unchanged even if its format copy limit exceeds 1.
5. Double-click blank list space or scrollbar, or rapidly switch filters. Confirm no stale previously selected card is added.
6. Ensure only the active eligible EDH catalog candidates can be added; wait for initial catalog load before testing. Confirm a missing/unsynced catalog does not allow a silent bypass.
7. Save via the separate review/preflight flow and verify the existing fast Deck Library return and recovery guarantees remain intact.

**Scope:** Native Windows v0.9.71 preview. Automated tests and manual WPF acceptance still required before stable 1.0.
