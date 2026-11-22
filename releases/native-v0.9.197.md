# Arcane Vault Windows v0.9.197 (Preview)

## Added
- **Custom Deck Art** in both Windows saved-deck editors: select Auto or Custom, choose any commander/mainboard card in that deck, and optionally choose its exact official Scryfall printing.
- Chosen custom artwork is saved on that deck only, independent of owned printings, Commander printing settings and the global card catalog.
- The Windows Deck Library and the XENEON EDGE/Stream Deck exporter now use the custom card and printing as the deck cover, with a safe automatic fallback when the selected card is removed.
- The Commander field automatically hides when the format is neither Commander nor Arena Brawl, while unsaved Commander text remains available if the format is switched back.
- Arena Standard Ranked deck records recognize additional verified Constructed Ladder queue IDs (such as the generic `Ladder`), **only when the match has a stable confirmed Arena/native saved-deck association**.

## Changed
- With no custom cover, the display still uses the saved Commander art or an ordinary mainboard card for non-Commander formats.
- Changing to a non-Commander format no longer saves a Commander-zone card.
- The native match exporter re-reads existing Match History for W/L counters; verified older Ranked Standard matches can appear without re-import or changing recorded outcomes.
- Match records with unrecognized queue IDs or ambiguous deck associations remain uncounted rather than inventing Ranked Standard results.

## Fixed
- Saved Ranked Standard matches captured under generic ranked-ladder identifiers could be excluded from deck/widget W/L totals despite being present in Match History.
- A removed custom cover card could leave stale imagery. The save confirmation warns and safely resets that preference to Auto.
- Custom cover preferences and selected official printing now propagate beyond the editor.

## Removed
- Nothing. Existing collection ownership, Arena match rows, saved deck IDs, commander printing preferences and revision histories are preserved.

## Verification
- Open a saved Arena Standard deck, choose Custom Deck Art from a mainboard card, and choose an alternate printing. Save/reopen and compare Deck Library and XENEON EDGE imagery.
- Change deck format between Commander, Arena Brawl, Arena Standard and 60-card. Commander input is visible only for the commander formats.
- Remove the selected cover card and save; confirm the Auto fallback warning and no change in collection quantities.
- In Match History, locate an already captured Ranked Standard win. Verify its saved-deck assignment and exact Arena deck ID; use Assign selected match and remember the mapping if necessary. Refresh the Edge/Stream Deck snapshot and ensure that win counts once under the saved Arena Standard deck, not Brawl.
- Standard Play, unrelated ranked queues and unknown/unmapped matches must never be counted as Ranked Standard.
