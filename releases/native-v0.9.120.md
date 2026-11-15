# Arcane Vault Native Windows v0.9.120 — Collection Intelligence and Safer Device Reviews

## Added
- A Collection Analytics tab inside the native collection workspace with verified priced subtotal, printing-price coverage, distinct owned card names, catalog verification status, and recent value-history chart.
- Ranked most valuable owned physical printings and owned physical sets, including exact printing/finish quantities, scoped Scryfall cached values, and unpriced printing coverage.
- A near-buildable saved deck ranking based on actual recorded Physical or Arena ownership, with missing card-copy breakdowns. This is card-count coverage, not a format legality or competitive-power claim.
- Peer sync comparison summaries grouped by physical cards, Arena cards, saved decks, and artwork, plus incoming/absent copy differences and explicit conflict warnings.
- Regression checks for analytics price and set identity accuracy, nonzero quote coverage, and reviewed sync summaries.

## Changed
- Analytics recomputes only from the already verified local valuation and saved deck collection, without new catalog-wide queries or extra price scraping.
- Peer summaries remain strictly read-only. No simultaneous edits or peer deletions are silently resolved.
- Unverified printings and price gaps are called out rather than counted as fully valued inventory.

## Fixed
- Set completion, portfolio value, pricing coverage, and deck readiness can now be reviewed together without manually comparing separate inventory and deck screens.
- Peer preview now exposes affected data domains and quantity changes rather than an undifferentiated list of differences.

## Removed
- No saved cards, decks, ownership ledger, prices, history, or existing sync checkpoints were removed.

## Safety
- Existing approved peer-sync apply and recovery behavior is unchanged; verified shared baselines and manual approval remain mandatory for conflicting edits.
- Value rankings count only verified exact physical Scryfall printing and finish quotes. Proxies, unknown printings and missing price quotes are excluded from priced subtotals, not assumed to be worth zero.
- Near-buildable deck suggestions use Physical or Arena ownership as appropriate and do not claim a legal/strategic substitute unless verified by other systems.
- Total collection value history includes changes in owned inventory; the app does not present that total movement as a market-only gain.
- No additional background web requests or authoritative database mutations are introduced for the analytics dashboard.
