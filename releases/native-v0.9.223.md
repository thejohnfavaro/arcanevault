# Arcane Vault Windows v0.9.223 (Preview)

## Added
- **Browse Card Catalog** shortcut in the embedded EDH Lab deck editor header, making it straightforward to inspect specific Scryfall printings while editing a deck.
- Return to the in-progress draft through the existing **EDH Lab** sidebar item after catalog browsing.

## Changed
- Catalog browsing reuses Arcane Vault's existing workspace navigation and preserved embedded editor session rather than opening a modal window or closing the current draft.

## Fixed
- The absence of a direct, discoverable Card Catalog shortcut from the embedded deck editor workflow.

## Removed
- Nothing. No changes to owned cards, decks, printings, persistent collection storage, or saved match history.

## Verification
- Native Windows compile, regression tests, preview archive identity checks, and installer publishing are performed by the native Windows GitHub Actions workflow.
- Manually open a saved deck, make an unsaved change, select **Browse Card Catalog**, inspect an exact printing, then select **EDH Lab** and verify the same unsaved changes remain.
- Save the deck and verify only the intended deck is updated; repeat the navigation before saving to confirm no accidental persistence.
