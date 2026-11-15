# Arcane Vault Native Windows v0.9.113 — Integrated EDH Lab Review

## Added
- **Synergy Explorer** inside EDH Lab: commander-informed mechanic packages now have an owned-only view, engine-gap-first sorting, and explanations for potential enabler/payoff gaps. All results retain the existing commander identity/format/collection gates and card-preview workflow.
- **Deck Health redundancy review**: evaluate the number of distinct card names fulfilling ramp, card draw, removal, protection and commander-synergy roles. Suggest separate unused recorded candidates when available, without treating duplicate copies as backup cards.
- **Mana-source audit**: before-and-after proposal summaries now show conservative direct colored source counts and untapped, conditional, tapped and fetch-target-dependent land counts.

## Changed
- Synergy candidates can be prioritized by missing enabler/payoff signals while retaining normal read-only preview and opt-in add actions.
- Mana-base optimization prioritizes recorded untapped and shock-land candidates over slower entries when replacing surplus basics, keeping existing spells/nonbasic lands intact.
- Existing deck-health role coverage is complemented with redundancy explanations rather than a misleading single power score.

## Fixed
- A role could appear covered solely by one card name without Deck Health highlighting the resulting dependence.
- Land proposals previously provided an exact diff but little explanation of whether proposed land choices improve color access or entry speed.

## Removed
- No collection data, deck edits, printings, proxies, artwork or saved preferences removed.

## Safety
- Every advisory analysis is local, deterministic and based on the existing verified commander/format-eligible catalog. Missing metadata is explicitly treated as uncertainty.
- The mana-base proposal does not change a deck until the user confirms the exact diff. Collection ownership, prices and stored decks are never changed by these analyses.
