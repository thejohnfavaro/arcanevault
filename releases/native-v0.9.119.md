# Arcane Vault Native Windows v0.9.119 — Collection and Set Value History

## Added
- An in-app physical collection value-history line chart, alongside the existing priced subtotal on the Physical Collection page.
- A dedicated history chart inside each physical set's Set Progress details, scoped only to owned, verified printings in that set.
- Persistent daily price observations for the overall collection and individual sets in a separate local cache with a 730-day retention limit.
- History regression tests covering set isolation, same-day observation coalescing, collection total history, and collection/catalog byte preservation.

## Changed
- Value-history charts show actual stored observations and their dates. Repricing more than once on the same day replaces that day's sample instead of duplicating it.
- Charts update after the existing verified printing/finish Scryfall valuation is recalculated, and redraw when the window changes size.
- Existing set completion tiles, official symbols, background art, set-value totals, and color-coded lifetime baselines remain intact.

## Fixed
- The Windows app previously compared current set values to a first-tracked baseline without displaying a graph of changes over time.

## Removed
- No card, deck, pricing, catalog, proxy, ownership, or historical-baseline data is removed.

## Safety
- History is cached under the existing per-user Arcane Vault directory, separate from application binaries and authoritative inventory data.
- No zero-dollar samples are fabricated for unpriced holdings; no pre-installation valuation history is guessed.
- Both graphs show daily saved Scryfall USD cached estimates for priced physical holdings only; additions and removals can affect the value independently of market repricing.
- No new quote-fetching services, merchant APIs, inventory writes, or network operations are introduced.
