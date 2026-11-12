# Arcane Vault Native Windows v0.9.45 — Commander Synergy Packages (Preview)

EDH Lab now groups the commander's cached Oracle-text mechanic matches into small, explainable, collection-aware packages. This is a read-only first slice of the larger synergy-package roadmap.

## Added

- A collapsed **Commander synergy packages — preview only** section inside **EDH Lab → Improve** (within the existing scrollable workspace).
- Separate groups for cards already in the draft, unused owned candidate cards, and eligible cards not recorded in the selected collection.
- Package labels based on recognized commander mechanic signals such as Artifacts, Tokens, counters, Graveyard and typal support. Each match is scored **for that specific theme** rather than borrowing the card's total match score.
- Conservative hints identifying potential enablers, payoffs and supporting matches, explicitly without combo or power guarantees.
- Select any listed card to view its local card text, illustrative artwork and ownership in the existing right-side preview.
- Synthetic regressions for per-theme matches, singleton exhaustion, ownership separation, Commander versus Arena Brawl eligibility, commander exclusion, color identity and banned/unavailable cards.

## Changed

- EDH Lab now refreshes package suggestions from unsaved mainboard edits using the already-loaded format-filtered catalog snapshot. The complete eligible card snapshot is analyzed rather than truncating to the first 200 browser results.
- The new panel remains collapsed by default so it does not crowd the working Cards, Mana & Roles and Improve layout.

## Fixed

- Package-level suggestions do not inherit a strong match to a different commander mechanic.
- Recorded-but-exhausted cards are not advertised as unused owned candidates; cards with no recorded copies are presented separately as missing.

## Removed

- Nothing. Existing saved decks, imported collection entries, update handling, mana balance, strategy priorities and Swap Lab behaviors remain unchanged.

## Accuracy and safety notes

- These are cached Oracle-text heuristics, not verified combos, synergies, deck grades, power rankings, printing identities or purchase recommendations.
- The analyzer does not request network data, edit the catalog, change collection quantities or alter unsaved or saved deck cards.
- Existing copy limits, commander color identity, format legality and Arena card availability are inherited from the current eligible catalog snapshot. Separate saved-deck preflight remains authoritative.
- The in-deck and suggested-card lists show at most five examples per category and theme; counts are not a claim of exhaustive recommendations.
- This is a native Windows **preview**, not a production 1.0 release.

## Local acceptance

1. Update using **Native Updates** to v0.9.45.
2. Open EDH Lab and a commander with recognized artifact and token text, such as a local test deck.
3. Open **Improve → Commander synergy packages**. Confirm at least two mechanic groups where applicable, with cards distinguished as **In deck**, **Owned option**, or **Missing**.
4. Click an owned and a missing suggested card. Verify that right-hand card preview shows the selected card rather than an old Cards-tab selection and that nothing is added to the draft.
5. Verify scrolling and expanding other Improve sections remain usable when the window is resized.
6. Repeat with Arena Brawl; confirm physical-only owned counts and unavailable/banned cards never appear.
7. Add a card manually or remove a card from the draft, then confirm the categories update without auto-saving.
8. Confirm that collection quantities, deck revisions, and stored files remain untouched by browsing packages.

Release gates: successful standard Windows and .NET 10 native regression workflows, checksummed native portable ZIP from the merged main-branch SHA, then on-device acceptance.
