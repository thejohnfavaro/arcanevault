# Arcane Vault Windows v0.9.229 (Preview)

## Added
- Offline hypothetical paper Commander bracket analysis for Arena Brawl, Arena Standard, and other saved deck formats.
- Prominent context warning for non-Commander decks that a paper bracket is only an indicator screen, not an official result.
- Visual regression check preserving Arena deck access to the offline bracket analyzer.

## Changed
- The Compute bracket action can inspect the current unsaved Arena deck without copying it or changing its saved format.
- Reports continue to use the existing Game Changer, tutor, fast-mana, lock and potential combo indicator analysis.

## Fixed
- Arena Brawl deck reviews previously rejected the bracket analyzer even when the user wanted a hypothetical paper conversion estimate.

## Removed
- The blanket Arena/Brawl bracket-analysis restriction. No cards, decks, Arena match records or collection data removed.

## Verification
- Run native Windows compile and regressions through the official release workflow.
- Open an Arena Brawl deck and compute the paper bracket estimate; verify its saved format and decklist are unchanged.
- Open an Arena Standard deck, run the estimate, and confirm it warns about missing commander and paper legality.
- Confirm the assessment cannot be saved as an AI review and does not write an official Arena bracket.
