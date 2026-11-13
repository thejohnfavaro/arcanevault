# Arcane Vault Native Windows v0.9.68 — Explainable missing-card comparisons

## Added

- **Before/after panels:** EDH Lab → Improve → Resolve missing cards shows a read-only comparison of the missing card and its recorded replacement candidate. The panels wrap into a vertical stack on narrow windows rather than overflowing the workspace.
- **Card references:** Both panels show name, type, recorded copies versus draft copies, role hints, mana cost, mana value, and scrollable Oracle text. Mana symbols render via the existing native official-Scryfall renderer (with exact brace notation as offline fallback).
- **Explainable rankings:** Preserving all recognized source roles remains mandatory. The ordering then considers matching primary card type, new role hints, commander-mechanic signal, synced mana-value distance, available recorded copies, and the current user-selected EDH strategy.
- **Ranking evidence:** Each selected alternative describes the comparison of type, roles, curve, commander mechanic and current strategy. The ranking is advisory, not a power-level rating or rules-equivalence guarantee.
- Regression coverage for strategy weighting, rank ordering, known/unknown mana, retained Oracle/cost source data, and WPF presentation.

## Changed

- Alternatives display ordinal ranks rather than uncalibrated internal scores.
- The existing strategy focus selection refreshes shortage rankings. Where mana-value metadata is absent, recommendations explicitly say that the curve comparison is unavailable rather than treating the value as zero.
- Applying a confirmed one-copy swap now also checks that the chosen ranking evidence remains current, in addition to the existing shortage and recorded-copy validation.

## Fixed

- Replacements previously displayed a short role-based reason without a direct card-text or casting-cost comparison.
- Suggestions now use more informative deterministic ordering than raw role count, while never treating matched tags as proof of an equivalent effect.

## Removed

- No inventory, deck rows, commander, printing choices, preferences, revisions, backups, recovery files or catalog data were removed.
- No automatic swap or save; physical/Arena inventory remains separate, and no copy is reserved across multiple decks.

## Windows acceptance

1. Open a saved Commander or Arena Brawl deck with shortages and choose Review shortages in EDH Lab. In Improve, select a missing mainboard card and an alternative.
2. Inspect both cards' names, type, roles, recorded/draft counts, mana cost/value and Oracle text; confirm official symbols where cached and literal notation when not.
3. Resize Deck Studio. The two panels should sit side-by-side when wide and wrap when narrow, retaining usable vertical scrolling.
4. Read the ranking evidence for shared roles, type, mana delta, commander-synergy signal and current strategy. It must never claim guaranteed strength.
5. Change the strategy priority and verify the ranked list updates. Try a legacy catalog with absent mana metadata; its cost/curve comparison must say unavailable.
6. Verify Physical-only versus Arena-only suggestions cannot borrow one another's recorded cards or recommend ineligible/banned color-identity candidates.
7. Cancel the replacement prompt and verify no edit. Confirm one substitution and verify only the current unsaved draft changes; the existing Review and save process stays separate.

**Scope:** Native Windows v0.9.68 preview. CI checks and manual Windows/DPI acceptance required for stable 1.0.
