# Arcane Vault Windows v0.9.230 (Preview)

## Added
- **Complete My Deck · Export shopping list** on the Cards tab in both the saved Deck Editor and embedded EDH Lab draft.
- A read-only, UTF-8 CSV containing each missing card name, uncovered copy count, total needed, owned physical/Arena copies, proxy copies, and selected inventory pool.
- Regression tests covering physical-versus-Arena isolation, proxy accounting, already-covered cards and spreadsheet formula escaping.

## Changed
- Shopping lists always recalculate shortages directly from the current unsaved deck text and selected ownership pool. The current draft and underlying collection are never modified by export.

## Fixed
- Missing-card quantities previously had to be transcribed manually from the shortage panel to plan purchases.

## Removed
- Nothing. No ads, retailer redirections, premium paywalls or affiliate identifiers have been introduced.

## Verification
- Run Windows native compilation, read-only regression suite, ZIP/installer validation, and release publication in GitHub Actions.
- Open a deck with missing physical cards and some proxy-covered copies; export the shopping list and verify its quantities and escaping in Excel.
- Repeat after editing unsaved card counts and switching from Physical to Arena. The exported lists must reflect the draft and the selected pool.
- Cancel the file-save dialog and verify nothing changes; ensure deck and collection data remain unchanged.
- Budget planning, owned substitution ranking, current prices and retailer links will be separate future features requiring reliable pricing, opt-in behavior and review of relevant terms.
