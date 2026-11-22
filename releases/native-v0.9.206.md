# Arcane Vault Windows v0.9.206 (Preview)

## Added
- A single top-level **Deck Scope** selector in Deck Analytics, with **All Decks — aggregate** and one entry per saved native deck.
- Regression checks for scoped GRE telemetry, observed gameplay and mulligan counts, and verified on-play/on-draw attribution.

## Changed
- Choosing a saved deck scopes confirmed match performance, format breakdowns, opponent and own color-identity breakdowns, weekly trends, recent matches, archived telemetry rows and totals, observed gameplay, and verified play/draw statistics.
- Scope uses the exact saved native deck ID, not an ambiguous deck name; historical name-only matches remain excluded from individual deck metrics.
- The nested performance-panel deck selector is removed from the presentation so two competing deck selections cannot disagree. Optional deck comparison remains available in aggregate mode.
- **All Decks — aggregate** is the default when the view is first opened, and is retained as an explicit selector option.

## Fixed
- The earlier Saved Deck Play/Draw selector filtered only play/draw metrics while other analytics continued to display aggregate records. All reports now follow one scope.

## Removed
- The separate Saved Deck Play/Draw-only selector.

## Verification
- Run native Windows CI and the `arena-log-assignment`, `match-performance`, `native-visual-shell` checks.
- In Deck Analytics, switch from All Decks to Mono-White Auras and compare the performance, color identity, weekly performance and telemetry sections. All should change together; switching back should restore aggregate data.
- Confirm ambiguous/name-only historical rows do not silently appear in an exact saved-deck view.
- Ranked season timing and rank reset prediction remain account-level, not per-deck. This is a read-only analytics change and does not edit match attribution, outcomes or collection data.
- Build and on-device verification have not yet been confirmed.
