# Arcane Vault Windows v0.9.227 (Preview)

## Added
- Scrollable, read-only ownership shortages panel in the saved-deck editor and EDH Lab editor.
- Regression checks for shortage scrolling in both editor layouts.

## Changed
- Ownership shortages now use a bounded, wrapped scrolling region so every missing card can be inspected.

## Fixed
- Large shortage lists previously clipped their final entries at the bottom of the Cards panel.

## Removed
- Nothing from collection records, saved decks, ownership, proxies, or backups.

## Verification
- Run native build and regression tests, package the Windows installer and publish via the main release workflow.
- Open a deck with a long shortage report and scroll to the final entry in both editors.
