# Arcane Vault Windows v0.9.231 (Preview)

## Added
- **Complete My Deck** shopping-list CSV can now include conservative, locally eligible, already-owned alternative card suggestions for Commander and Arena Brawl drafts.
- Additional CSV column identifies possible substitution copies as **Not Applied**, grouped by missing card. The original purchase shortages and all quantities are unchanged.
- Regression coverage for CSV escaping and substitution annotations.

## Changed
- Reuses EDH Lab's existing format- and commander-filtered multi-card substitution planner, only when its ownership pool matches the current export pool.
- Checks suggested copies against the currently selected recorded inventory before displaying them.
- Non-Commander decks, unmatched pools, and unavailable metadata continue to export the accurate missing-card list, without speculative recommendations.

## Fixed
- Completion shopping lists previously required manually cross-referencing EDH Lab's separate owned-alternatives screen.

## Removed
- Nothing. No pricing guesses, automatic replacements, affiliate tracking, purchases, access restrictions, or user data collection.

## Verification
- Build and run Windows native regression tests; package and publish only after successful gates.
- Export a Commander draft with several missing cards and owned role matches. Verify grouped optional alternatives appear without changing shortage quantities or saving the draft.
- Switch physical/Arena ownership pools and verify no cross-inventory alternatives are included.
- Export a non-Commander deck; verify shopping list works and no unsupported legality/ownership suggestion is fabricated.
