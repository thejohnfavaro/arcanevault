# Arcane Vault Native Windows v0.9.121

## Added
- EDH Lab whole-deck owned-card substitution planner with read-only proposal list and unresolved shortages.
- Explicit approval to apply multiple one-for-one swaps to the current unsaved mainboard.
- Regression checks for per-copy inventory reservation, zero-shortage plans, and isolated Arena ownership.

## Changed
- Replacement candidates reuse the already format- and commander-filtered local card catalog, rank by strategy and whole-deck roles, and reevaluate the plan immediately before applying it.
- Applying a plan updates only the unsaved draft; final Review & Save remains a separate action.

## Fixed
- Multiple missing-card recommendations no longer compete for the same recorded candidate copy in a batch plan.

## Removed
- None. Single-card preview and manual selection remain available.
