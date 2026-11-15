# Arcane Vault Native Windows v0.9.118 — Owned Set Values and Lifetime Change

## Added
- Physical Set Progress cards now show the cached Scryfall-valued subtotal for the specific owned physical printings of that set.
- Each set with a valid quoted subtotal now retains its own first-known value baseline and displays a positive green, negative red, or neutral lifetime change.
- Selected Set Progress details show the set-specific total, absolute and percentage lifetime change, and priced/unpriced printing coverage.
- Regression tests verify that set-specific subtotals never mix printings of different sets, account for the selected finish, reconcile with the overall priced collection total, and do not reset their lifetime baseline on recalculation.

## Changed
- Set pricing reuses the existing Windows verified physical printing valuation and Scryfall cached USD quotes, with no extra Scryfall HTTP requests or duplicate ownership ledger.
- Per-set price baselines are stored as small local cache metadata beneath the existing Arcane Vault native user data directory, not the application installation directory.
- Original Set Progress artwork, official set symbols, completion percentages and layout remain intact.

## Fixed
- Physical Set Progress formerly displayed ownership/completion without the scoped owned collection value and lifetime difference.
- No unknown or unpriced physical printing is silently treated as a zero-priced verified quote.

## Removed
- No saved cards, decks, owned printings, preferences, or set-progress snapshots are removed.

## Safety
- Values are USD Scryfall bulk-cache estimates for verified exact physical printings and finishes, not live merchant offers.
- Baselines begin with the first nonzero priced result after this update, not historical market prices.
- Lifetime change includes changes in owned inventory and repricing, and therefore is not a market-only return.
- Arena name coverage is not treated as physical ownership or valued from physical quotes.
- Existing inventory and catalog are read-only during valuation; baseline metadata cannot change collection ownership.
