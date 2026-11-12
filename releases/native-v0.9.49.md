# Arcane Vault Native Windows v0.9.49 — Three-Card Engines (Preview)

Builds on the on-device tested v0.9.48 Synergy Chain Explorer with a limited, read-only first slice of **three-card pathways** in EDH Lab.

## Added

- Collapsed **Three-card engines — inspect possible pathways** section in the existing scrollable EDH Lab → Improve tab.
- Explicit producer → bridge → payoff detection for supported cached Oracle-text sequences:
  - Artifact-token production → artifact-entry life gain → life-gain payoff.
  - Artifact-token production → artifact-entry card draw → card-draw payoff.
  - Creature-token production → creature-entry counter placement → counter-placement payoff.
- **All three in deck**, **Owned missing link**, and **Not recorded** classifications for each eligible pathway.
- At least two distinct pieces must already be in the mainboard before a third card is suggested.
- Separate **Inspect producer**, **Inspect bridge**, and **Inspect payoff** controls to use the established full Oracle-text/artwork card preview.
- **Find owned missing engine link in Cards** navigates to the existing manual card picker. It never adds, crafts, removes, or saves a card.
- Regression fixtures for complete three-card pathways, correctly separated owned and unrecorded missing pieces, Arena/physical pool isolation, singleton limits, and malformed quantity rejection.

## Changed

- Synergy analysis can identify a small set of linked three-card text cues rather than only independent two-card suggestions.
- The analyzer limits candidates per pathway and displays at most ten pathways; fully drafted candidates rank ahead of owned missing links, ahead of unrecorded possibilities.
- Existing two-card Synergy Chain Explorer, commander packages, mana balancing, strategy priorities, Swap Lab and review/save behavior remain unchanged.

## Fixed

- A three-card suggestion cannot consist of three wholly unselected catalog cards or require adding two cards.
- A missing link with exhausted copy allowance cannot be advertised as an eligible new owned addition.
- Off-color, banned, or Arena-unavailable candidates cannot enter the eligible pathway pool.

## Removed

- Nothing. No collection data, deck data, backup, import, schema, or updater behavior is removed or changed.

## Accuracy and safety

- These are *potential pathways based on cached Oracle text*, not verified game-rule interactions, deterministic engines, loops, infinite combos or win conditions.
- Timing, optional effects, replacements, targeting, opponent decisions, and zone transitions are not simulated. Inspect complete card text before relying on any proposed sequence.
- Recorded ownership is by card name; an owned printing or cosmetic treatment is not guaranteed.
- Recommendations are read-only. The existing manual deck editor and protected Review & Save remain authoritative.
- This is a native Windows preview, **not 1.0**.

## Local acceptance

1. Update using Native Updates to v0.9.49 (or launch the native Windows portable preview).
2. Open a commander deck with an artifact-token producer and an artifact-entry draw payoff in **EDH Lab → Improve → Three-card engines**.
3. Confirm an eligible draw-trigger card appears as an owned or unrecorded third link only when two pieces are already in the mainboard.
4. Click **Inspect producer**, **Inspect bridge**, and **Inspect payoff**. Verify each opens the right-side card preview and no deck editing occurs.
5. For an **Owned missing link**, use the Cards navigation button. The normal manually operated card search should open with the suggested name, without automatically adding it.
6. Confirm **Not recorded** pathways do not enable the owned shortcut.
7. Add or remove a card from an unsaved test draft, then confirm that the pathway classification changes with the current mainboard.
8. Repeat with an Arena Brawl commander: physically owned but Arena-unavailable cards must not become owned additions.
9. Expand multiple Improve panels, resize the window, verify scrolling and performance, and confirm existing collection and saved decks remain intact.

Release gates: native .NET 10 Windows/regression workflows, checksummed portable bundle, release publication, and user on-device acceptance.
