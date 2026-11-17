# Arcane Vault Native Windows v0.9.140 (Preview)

## Added
- Search box for filtering Match History by date, deck, result, status, diagnostic note, and Arena deck ID.
- Confirmed "Resolve verified IDs" action to reconcile previously unassigned matches using exact known Arena deck IDs.

## Changed
- Match History review now offers an explicit, opt-in way to reconcile old records against uniquely linked saved decks.
- Both native and legacy match-history records are checked for safe reconciliation.

## Fixed
- Exact-ID reconciliation honors ambiguous or conflicting saved-deck mappings and never attributes a deck by name alone.
- Existing manual assignments, match results, and game totals remain untouched during bulk reconciliation.

## Removed
- No functionality removed.

> Matches that never captured an Arena deck ID cannot be automatically assigned from this feature and still require manual review.
