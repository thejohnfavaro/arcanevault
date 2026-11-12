# Arcane Vault Native Windows v0.9.35 — Commander Mechanic Synergy Hints (Preview)

This release adds explainable, collection-aware commander mechanic hints to EDH Lab.
These are offline rules-text similarities, **not** a deck optimizer, combo engine
or competitive power ranking.

## Added

- **Commander mechanic matches first (owned first)** in EDH Lab Deck Studio:
  show locally eligible cards you own before missing cards, ordered within
  each group by the number/strength of explicit mechanic signals that match
  the selected commander's cached Scryfall rules text.
- **Only show commander mechanic matches** optional filter. Ordinary name
  search, existing role filters and the recorded-in-inventory toggle remain.
- Detailed **why this matched** text in the selected card's preview.
- Recognized signals for artifacts, token making/payoffs, +1/+1 counters,
  graveyard recursion, sacrifice, lifegain, spells, enchantments, equipment,
  landfall and discard; typal matches only if the commander also mentions
  one of its own creature subtypes in its rules text.
- A status summary naming recognized commander mechanics and the number of
  locally eligible candidate identities with heuristic matches.
- Automated regression checks for owned-first ordering, evidence explanations,
  bans and Arena availability, false-positive artifact hate, zero-signal
  commanders, typal matching and source-catalog immutability.

## Changed

- Synergy hints are indexed once while opening the eligible EDH Lab candidate
  list; changing the filters uses cached local results rather than a network
  request. The selected card's preview immediately explains its match.
- When no recognized commander signal is available, synergy-only controls
  are disabled rather than inventing synergy or hiding all cards.
- Commander color identity, Paper Commander legality, Arena Brawl availability,
  card copy limits, recorded ownership and protected native deck saves remain
  the same as v0.9.34.

## Fixed

- Explicit positive artifact/token signals are distinguished from text that
  merely destroys artifacts, which should not be recommended as synergy.
- Missing or banned cards never bypass legal-identity filters because of
  a high mechanic score.

## Removed

- Nothing. No collection schema or native database migration, deck copies,
  inventory edits, new price/ownership sources, or changes to the updater.

## Local acceptance checklist

1. From v0.9.34, make an independent native full backup; update to v0.9.35
   through **Native Updates** and confirm the new version opens.
2. In EDH Lab, choose an artifact/token commander (or reopen a saved one
   using **Edit in EDH Lab**). Confirm the status names its detected mechanics.
3. With recorded-only enabled, verify cards in your collection appear first.
   Uncheck it: missing eligible cards appear but never claim to be owned.
4. Toggle **Only show commander mechanic matches** and the existing role filter.
   Select a candidate and read its explanation in the card preview.
5. Switch **Commander mechanic matches first** off; confirm normal
   recorded-first, alphabetical browsing returns.
6. Check an unrelated card, a banned card, and a card outside the commander's
   identity: no invented explanation or bypass of eligibility.
7. Repeat in Arena Brawl; Arena-only availability and Arena ownership must be
   used, not physical collection counts.
8. Save a test deck via existing native preflight, reopen it, and compare
   physical/Arena inventory, deck revisions and backup recovery paths.

**Important limits:** Mechanic tags are conservative, text-based heuristics;
they do not understand all card interactions, special Commander partnerships,
winning lines, mana efficiency or the live banlist. An unrecognized match does
not mean a card is bad, and a recognized match does not guarantee a useful
synergy. No external AI service, collection write or network search is used.
