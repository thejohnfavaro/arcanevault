# Arcane Vault Native Windows v0.9.10 — Physical Collection Valuation

**Windows x64 native preview**, not a certified 1.0 replacement for the shipping WebView app.

## Added
- Exact-printing USD estimates in **Physical Collection**, including a price for each finish (nonfoil, foil, etched), a unit-price column, line value (quantity × unit), and explicit reasons when a price cannot be calculated.
- A **priced subtotal** with priced and unpriced copy counts. When any row lacks a quote, the subtotal is clearly identified as **not the full collection value**; missing prices never become fabricated $0 values.
- Source date and stale marker for the official Scryfall bulk dataset. Cached bulk prices older than 24 hours or missing a verifiable timestamp are treated as stale estimates, not live quotes.
- Physical inventory sort options: original order, unit price high/low, and line value high/low. Unpriced rows sort last; sorting never changes the original row order or edit identity.
- **Recalculate cached prices** action to re-evaluate the read-only local catalog. For genuinely newer price data, run **Full Scryfall Sync** in the main native window and reopen the Collection view.
- Native regression fixtures for paper printing verification, same-name editions with different prices, finish-specific price selection, missing/invalid prices, nonpaper printings, unspecified finish, old catalogs, aging/timestamp warnings, exact decimal accounting, and unchanged source files.

## Changed
- The native official Scryfall bulk import now retains the per-printing `prices.usd`, `prices.usd_foil` and `prices.usd_etched` strings in isolated catalog columns. No new collection schema or migration is required.
- Catalog sync still writes an independent, verified SQLite replacement and retains its old catalog as a separate rollback.
- Physical Collection calculates valuation asynchronously and does not block editing the owned backup; exact original row ordinal is retained when sorting.

## Fixed
- Previous native Physical Collection had no pricing, valuation, price freshness indicator, or price sort.
- Name-only physical holdings, digital-only printings, unknown foil treatment, and older catalogs cannot be accidentally valued using another printing/finish.

## Removed
- Nothing. No changes to installed WebView data, native collection quantities, Arena holdings, saved decks, preferred artwork or legacy profile.

## Important limitations
- **Scryfall bulk USD estimates only**: these are indicative daily aggregate reference prices, **not live TCGPlayer/marketplace offers, guaranteed resale proceeds, or a merchant-specific buylist**.
- Scryfall cautions that bulk card prices can become dangerously stale after 24 hours. This UI shows the timestamp and explicit stale state; a full catalog sync refreshes prices.
- Already installed older native catalog snapshots **lack the new price columns**; they remain usable for browsing, while valuation requests a new Full Scryfall Sync instead of inventing prices.
- Direct TCGPlayer pricing integration and collection valuation by condition/grading are **not** in this release. Other WebView parity gaps and real-Windows acceptance remain tracked in #62.
