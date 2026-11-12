# Arcane Vault Native Windows v0.9.47 — Synergy Chains (Preview)

Introduces conservative, explainable two-card links in EDH Lab, extending the working v0.9.46 synergy package browser without altering deck data.

## Added

- A collapsed **Synergy chains — possible two-card links** section in EDH Lab → Improve, within the same bounded vertical scroll workspace.
- Directional **enabler → payoff** hints for a narrow set of cached Oracle-text relationships: artifact token creation into artifact-entry triggers; creature token creation into creature-entry triggers; +1/+1 counters into counter-placement triggers; gaining life into lifegain triggers; and creature sacrifice into death triggers.
- Three explicit results: **Both in deck**, **Owned missing link**, and **Not recorded**. Existing deck-to-deck pairs are ranked first, then eligible recorded owned options, then unrecorded ideas.
- Selecting a chain previews the relevant card in the existing right-side artwork and Oracle-text preview, with a written explanation of the possible interaction.
- Native regression scenarios for complete pairs, owned/unowned missing links, empty deck, invalid quantity, ban/color restrictions and paper/Arena inventory isolation.

## Changed

- Chain suggestions refresh with unsaved mainboard edits, using the current locally eligible catalog and no network requests.
- Results are bounded to 12 suggestions, and each mechanism's candidate pairing is capped to control work over large catalogs.
- Existing synergy packages, strategy priority, mana base and Swap Lab remain intact.

## Fixed

- No chain is suggested between two catalog candidates when neither is in the deck.
- A recorded-but-exhausted singleton cannot be suggested as a new missing link.
- Off-identity and banned cards remain excluded, even if their rules text appears to form a link.

## Removed

- Nothing. No card, collection, deck, schema, backup, update or save behavior is changed.

## Accuracy and safety

- Links are **possibilities only**, not rules-engine-verified combinations, repeatable loops, wins, or deck-strength grades. They may still involve unsatisfied timing, zone, ownership, target, replacement-effect, or opponent-choice conditions.
- Commander text is used to enable relevant mechanic themes, but the commander itself is not one of the two analyzed cards in this initial slice.
- Recorded ownership is by card name; actual owned printing is not verified. The exact rules text must be inspected before making any deck changes.
- This section has no add/remove/craft/save action; Review and Save remains separate and authoritative.

## Local acceptance

1. Update using Native Updates to v0.9.47, or launch the self-contained Windows portable preview.
2. Open a commander with artifact/token synergies and inspect **Improve → Synergy chains**.
3. Confirm an enabler/payoff pair already in the mainboard displays **Both in deck**.
4. With one component in the deck, confirm a locally eligible recorded companion shows **Owned missing link** rather than **Not recorded**, when available.
5. Select a chain and confirm card details and artwork update without changing the deck.
6. Confirm that card names not recorded in your inventory are marked **Not recorded**.
7. Try a different commander and Arena Brawl: off-identity, banned and non-Arena cards must not appear.
8. Expand several Improve sections and verify scrolling remains accessible; saved deck and collection remain unchanged.

Release gates: native regression suite, standard Windows build, SHA-256 checked portable bundle, and on-device acceptance. Still a preview, not 1.0.
