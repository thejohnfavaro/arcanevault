# Arcane Vault Native Windows v0.9.66 — Saved deck collection coverage

## Added

- **Recorded collection coverage beside every saved deck:** The persistent Deck Library inspector now compares the selected deck's saved card requirements with either the **Physical collection** or the **Arena collection** without launching Deck Studio.
- **Physical / Arena comparison selector:** Arena Brawl decks initially select Arena; all other decks start with Physical. The user can switch either way explicitly. Counts from the two inventories are never combined.
- **At-a-glance gaps:** Shows covered copies, total required (including a separate commander for Commander / Arena Brawl), total missing, and the first four shortages by name. An additional-name count appears when more than four different cards are short.
- **Selected card coverage:** A selected mainboard card now shows how many copies are recorded versus needed in the chosen pool. When that card is also the commander, one extra required copy is included.
- Regression assertions for selector wiring and read-only boundaries; synthetic physical/Arena cases covering a commander included in the mainboard and an empty mainboard with a commander.

## Changed

- Saved Deck Library coverage is calculated **on selection or when changing inventory scope**, not for every deck row. Card Catalog Scryfall artwork and Oracle text still load only for the selected card.
- Imported decks flagged as not editable show **coverage unavailable** rather than potentially misleading totals based on only their parseable entries.
- A selected card's artwork and Oracle text remain in place when switching Physical/Arena comparison; the inventory check itself does not need a Scryfall sync or internet.

## Fixed

- Inspecting a saved deck previously provided no name-level ownership gap context, requiring a switch to the deck editor merely to see which cards were missing.
- A commander with the same card name as a mainboard entry can no longer be mistaken for a single required copy in coverage totals (the shared Review helper accounts for both).

## Removed

- No collection rows, preferred printings, card metadata, saved deck entries, revisions, recovery points, backups, or imported fields were removed.
- No card quantities are allocated/reserved across multiple decks. Physical printing identities, Arena format legality and deck legality **are not** verified by this name-only comparison.

## Windows acceptance

1. Open **Collection & Deck Library → Deck Library** and select a saved Commander deck. Confirm the inspector shows a Physical coverage summary and, where applicable, missing card names.
2. Change the coverage selector to **Arena collection**. Counts and missing cards should update immediately, without loading a new Card Details window, changing the mainboard, or altering physical/Arena quantities.
3. Select multiple cards in the mainboard list. The selected card shows recorded vs required quantities in the active pool; cached artwork, Oracle text and full-details double-click behave as in v0.9.65.
4. For a saved **Arena Brawl** deck, verify the default coverage pool is Arena. Confirm switching back to Physical remains possible.
5. For Commander/Arena Brawl, verify an independently stored commander contributes one required copy even if that card name is also present in the mainboard. Non-Commander formats ignore an optional commander field.
6. Try a saved imported deck that requires review. Coverage should show **unavailable** rather than pretending its partial entries are complete.
7. Test missing Scryfall metadata or offline catalog: coverage still works from recorded ownership, with honest artwork/text fallbacks.
8. Hide/reopen the inspector, select a new deck, and resize the workspace. Deck-list scrolling and virtualization, in-place preview, Edit in EDH Lab, backups, and native updater should remain unchanged.

**Scope:** Native Windows preview v0.9.66. Read-only saved-deck inventory visibility. Not a printing verification, legality engine, inventory reservation or an app 1.0 approval. CI and user-device acceptance required.
