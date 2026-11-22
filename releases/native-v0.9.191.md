# Arcane Vault Native Windows v0.9.191 (Preview)

## Added
- Commander printing preference now reaches the Stream Deck / XENEON EDGE snapshot as deck-specific artwork rather than generic commander art.
- Windows Deck Library displays the saved official commander printing for Arena Brawl as well as tabletop Commander.
- Uniformly sized saved-deck library tiles.

## Changed
- The native exporter reads the exact `commanderDisplayPrintId` saved on each deck and resolves artwork with verified local Scryfall printing metadata.
- Scryfall printing choices remain cosmetic display preferences; they are not evidence of digital MTG Arena style ownership.

## Fixed
- Selecting an alternate commander printing previously changed the in-editor preview while the EDGE widget and Deck Library could continue showing the default printing.
- Refreshing a deck's saved printing now invalidates old deck artwork via the existing workspace-change reload and image URL change paths.
- Double-faced cards with a front-face commander name can resolve their official printing.

## Removed
- Nothing. Collection ownership, deck counts, Arena match history and printing identities are unchanged.

## Verification
- In EDH Lab choose a different official commander printing for an Arena Brawl deck and Review and Save Deck.
- Reopen the deck and confirm the selected printing stays selected.
- Return to Deck Library and verify that deck's cover uses the chosen printing.
- Keep Arcane Vault running to refresh the XENEON EDGE snapshot and verify the Edge tile updates without changing the win/loss record.
