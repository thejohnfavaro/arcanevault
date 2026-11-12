# Arcane Vault Native Windows v0.9.39 — Collection-Aware EDH Curve Optimization (Preview)

Builds on the EDH Lab mana curve, casting-consistency and synergy tools in
v0.9.35–38. This update starts turning diagnostics into **reviewable,
collection-aware improvements**, without automatically changing a single
deck card.

## Added

- **Collection-aware deck optimization** expandable panel directly beneath
  the mana curve and casting-consistency panel in EDH Lab Deck Studio.
- A live summary of nonland spells with mana value 0–2 ("early") and mana
  value 6+ ("expensive"), weighted by actual quantities in the draft.
- Conservative curve flags when the current known spell list is sufficiently
  developed (at least 25 nonland copies):
  - **Top-heavy curve** when at least eight spells cost 6+ and those cards
    account for more than 20% of known nonland copies.
  - **Thin early spell coverage** when fewer than 25% of known nonland
    spells cost 0–2.
  These are broad advisory thresholds, not a claim that expensive finishers
  are weak or that a strategy must use an arbitrary ratio.
- Up to five **unused, recorded, legal early-play candidates** ranked from
  the currently selected paper or Arena inventory. Suggestions come
  entirely from the local Scryfall-verified eligible EDH Lab card pool.
- Each suggestion includes card name, mana value, count of unused
  recorded copies, role hints and whether its cached rules text matches
  the selected commander's recognized mechanics.
- **Browse owned early-play candidates** button. Temporarily shows the
  suggestions in the existing card picker; select for the usual card
  preview or return to the full card catalog. This action only changes
  the current editor view.
- Clear handling for full 99-card mainboards (recommend review-only swaps),
  partial drafts (no premature high/low curve judgment), missing metadata,
  ineligible card names and exhausted singleton copy limits.
- Regression coverage for curve threshold boundaries, underfilled drafts,
  copy limits, preview-only candidates, format-separated paper/Arena
  recorded inventory, legacy catalog fallback, full decks and unknown cards.

## Changed

- EDH Lab candidate metadata now carries an explicit indication of whether
  real Scryfall mana values are available. If unavailable, optimization
  pauses instead of guessing cards have zero mana value.
- The new candidate browser updates after unsaved edits, so a singleton
  you already added will not continue appearing as an unused copy.
- Existing card search, role and synergy filters remain available. Changing
  ordinary filters or choosing **Return to full card catalog** exits
  optimization browsing.
- No change to v0.9.36–38 automatic land-balancing behavior, colored-pip
  weighting, curve probabilities, library save flow, or revision backups.

## Fixed

- Future collection-aware optimization cannot recommend a locally banned,
  off-color, unavailable-on-Arena, unowned, or fully committed singleton
  as a candidate merely because its mana value is attractive.

## Removed

- Nothing. No database or collection schema migration, automatic swap,
  automatic card insertion/deletion, inventory spend, crafting, changes
  to native updates, or revision-history rewrites.

## Accuracy and limitations

- Recommendations are **possible options**, never certified deck upgrades.
  They do not understand combo necessities, interactions beyond cached
  rules-text tags, actual card power, budget, printing availability, or
  playgroup preferences.
- "Early spells" describes 0–2 mana-value nonlands, **not** guaranteed
  playable turn-one or turn-two actions. A low-cost spell may still depend
  on targets, triggers, or prior board state.
- Curve flags require a substantial known spell list. Early incomplete
  drafts show their current profile but do not get strong diagnoses.
- The EDH Lab role and synergy classifiers are approximate; missing a tag
  does not mean a card lacks that function.
- Recorded quantities are counted by card name in either paper or Arena
  collection, **not** exact ownership of a printing. Native save/preflight
  remains the final gate.
- No card is changed merely by clicking a recommendation or entering the
  optimization panel. Adding/removing a candidate, if chosen, is still
  manual and the deck remains unsaved until Review and Save.

## Local acceptance

1. Update v0.9.38 to **v0.9.39** via Native Updates; no Full Sync is
   required if the v0.9.37 mana-cost refresh succeeded.
2. Open EDH Lab and choose a Commander (or open a saved Commander/Brawl
   deck using **Edit in EDH Lab**).
3. Expand **Collection-aware deck optimization** beneath the mana curve.
   Verify it displays early/expensive spell counts, advisory warnings
   only when enough of the spell list is known, and appropriate context.
4. Add several expensive cards and remove cheap spells from an unsaved test
   draft; confirm thresholds and owned suggestions change live.
5. Click **Browse owned early-play candidates**. Check the standard picker
   only shows locally eligible unused recorded cards of mana value 0–2;
   card preview, Add (+1) and Remove still use their original controls.
6. Add one suggested singleton, if desired, and confirm it disappears
   from the unused-candidate list while collection quantities stay fixed.
7. Click **Return to full card catalog**, or adjust the role/search
   filters; verify normal catalog browsing is restored.
8. Repeat for Arena Brawl: paper-only and unavailable Arena cards must
   never appear in its recorded optimization suggestions.
9. Enter a missing/unrecognized card, revisit a legacy catalog without
   `mv`, and test an already-full 99-card deck; confirm no silent guess,
   deletion, crafting, or save.
10. Cancel changes; verify the original deck, collection, printings,
    revisions and backup recovery state are unchanged.

Release gates: standard Windows build, native .NET 10 WPF/regression suite,
versioned checksummed native portable bundle, followed by user-device
acceptance before 1.0.
