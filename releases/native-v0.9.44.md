# Arcane Vault Native Windows v0.9.44 — Strategy-Aware EDH Lab Advice (Preview)

The next EDH Lab increment builds on v0.9.39–43 by letting a deckbuilder
**choose the goal of the deck** before interpreting owned-card suggestions
or one-for-one swaps. Until now, all recommendations favored a generic
low-cost curve; high-synergy or reactive strategies could be underserved.

## Added

- A **Deck Strategy Priority** selector at the top of **EDH Lab → Improve**.
  Five choices are available:
  - **Balanced:** the familiar v0.9.43 recommendation ordering and role protections.
  - **Faster starts:** favor inexpensive setup, ramp and early answers.
  - **Card advantage:** emphasize inexpensive draw and tutor roles.
  - **Interaction & protection:** emphasize cheap removal and protection.
  - **Commander synergy:** emphasize locally identified interactions with
    the selected commander's cached rules-text mechanics.
- Descriptive text explaining the selected priority and reminding the user
  that it is **analysis only** and not stored with the deck.
- Owned early-play candidate ranking now reflects the selected priority.
  Every suggestion remains restricted to the same locally eligible,
  recorded, still-available Commander or Arena Brawl card pool.
- **Swap Lab** uses the same strategy emphasis to rank alternatives for
  expensive spells. Its per-swap explanation identifies the strategy,
  mana-value difference, protected roles, commander-text score and
  unused recorded quantity.
- Swap proposals are bound to the priority that generated them.
  Switching priorities invalidates any proposal from the former mode.
  An attempted apply recomputes eligibility, strategy focus, role
  coverage, card quantities and legality before requesting confirmation.
- Native regression checks for observable ranking differences,
  synergy-first recommendations, scarce role protections, stale-goal
  refusal, Arena/paper separation and live selector wiring.

## Changed

- **Improve** now shows advice for the explicitly selected purpose of
  the deck instead of applying one generic preference to every strategy.
- **Balanced** remains the default for newly opened EDH Lab drafts and
  saved decks, preserving the previous recommendation algorithm.
- Switching priority refreshes recommendations immediately, without
  altering auto-land settings or recalculating land allocations.
- Existing recommend/preview/click/confirm/review-and-save workflow
  is unchanged; no one-click automatic optimization is introduced.

## Fixed

- Curve-first recommendations no longer have to rank a generic cheap
  spell over a similarly affordable card that advances the deck's
  declared draw, interaction or commander-mechanic strategy.
- A stale swap from a previous strategy preference cannot be approved
  merely because the same in/out card names remain in the new report.

## Removed

- Nothing. No collection, deck schema, old saved deck, copy-limit,
  import-diff behavior, backup/recovery, card preview or native updater
  feature is changed or removed.

## Accuracy and safety notes

- These are **heuristic, local suggestions**, not verified deck strength
  predictions. Role tags are extracted from card text and commander
  synergy matching is approximate; combo necessity, opponent matchup,
  playgroup bracket and complete play patterns are not modeled.
- The strategy selector re-ranks the current early-play pool (mana value
  0–2) and safe swap pool (existing expensive cards versus owned
  replacements at mana value 0–3). It does **not** search the entire
  historical card universe or add higher-cost engine pieces.
- The option is **session-only**. Reopening a saved deck starts at
  Balanced; this release does not persist a strategy attribute.
- Nothing is crafted, purchased, added, cut or saved by changing
  priorities or selecting a recommendation. Applying a swap still
  requires a Yes/No prompt, only changes unsaved editor text, and the
  separate protected Review and Save flow remains mandatory.
- Protected Ramp, Card Draw, Removal, Board Wipe and Protection
  minimums and commander-mechanic constraints continue applying
  regardless of chosen strategy. Existing collection and printings
  are never rewritten by analysis.

## Local acceptance

1. Update from native v0.9.43 to **v0.9.44** in **Native Updates**.
2. Open **EDH Lab → Improve** on a developed saved Commander deck.
   Confirm the new **Deck Strategy Priority** dropdown appears above
   collection-aware optimization and Swap Lab.
3. Select **Faster starts**, **Card advantage**, **Interaction & protection**
   and **Commander synergy** in turn. Observe the explanation and
   owned-card recommendation ordering change without changing the
   mainboard text.
4. In Swap Lab, select a proposal and read its strategy-specific
   rationale. Change priorities: the previous proposal must no longer
   be considered current.
5. Confirm that no proposal removes scarce board-wipe/protection/draw/
   ramp/removal role coverage or casually downgrades recognized
   commander-mechanic matches.
6. Browse suggestions in the Cards tab. Only recorded, locally legal,
   unused cards for the chosen format should appear.
7. Repeat on Arena Brawl; paper-only, banned, off-identity, exhausted
   or non-Arena cards must not appear.
8. Apply an optional proposal to an **unsaved test draft** and decline
   its confirmation. Saved deck, collection, printings, revisions
   and backups must remain unchanged.
9. Reopen the deck; the goal resets to Balanced (not silently stored).
10. Verify Mainboard click-to-preview from v0.9.43 and tab scrolling
    from v0.9.42 continue working.

Release gates: standard Windows build, native .NET 10 WPF/regression suite,
versioned SHA-256 checksummed portable native ZIP, then local user
acceptance. This is still a preview, not the 1.0 release.
