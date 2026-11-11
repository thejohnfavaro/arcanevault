# AVN-044 — Native physical collection valuation

**Version:** native Windows preview v0.9.10. **Release gate:** [#62](https://github.com/thejohnfavaro/arcanevault/issues/62).

## Price source and freshness

The official Scryfall `default_cards` bulk feed contains per-printing USD price fields named `usd`, `usd_foil`, and `usd_etched`. Native Scryfall Sync now saves those original **strings**, not floats, in new `variants` catalog columns, preserving snapshot precision. Prices are sourced from **Scryfall bulk data**, not live retailer scraping or direct TCGPlayer quotes. Full Scryfall Sync updates these fields with the bulk snapshot's declared `updated_at` timestamp.

**Staleness:** Treat these numbers as approximate reference values only. Scryfall explicitly cautions that bulk prices may become dangerously stale after **24 hours**. The Physical Collection UI shows the original bulk timestamp, marks prices stale when older than 24 hours (or undated/implausibly future dated) and still permits read-only comparisons. A local Recalculate action does not fetch new prices; use Full Scryfall Sync for a new snapshot.

## Matching and calculation

For each imported physical row, first use the existing read-only `CollectionCatalogReview.Examine` to verify an **exact paper Scryfall printing UUID** and compatible card name/set/collector information. Never match prices by card name alone. A valid print ID for a digital-only variant is still **not a physical printing**.

| Physical finish | Scryfall bulk quote |
| --- | --- |
| `nonfoil` | `prices.usd` |
| `foil` | `prices.usd_foil` |
| `etched` | `prices.usd_etched` |
| `unspecified` or unsupported | **Unknown** (not a guessed nonfoil price) |

Each valid price is parsed as invariant-culture decimal text; display and arithmetic are in USD with two decimal places. Line value is **quantity × exact printing/finish price**. If a quote is missing, corrupt or contradictory, the row shows **—** and an explicit reason. Partial rows are never assigned $0 or a different edition's quote. The UI calls the sum of complete line values **Priced subtotal**, not the total worth of the collection when even one copy is unpriced.

## Native UI

Open **My Collection → Physical Collection**. The top section displays the priced subtotal, number of priced and unpriced copies, a warning when incomplete, and the cached Scryfall source timestamp. The Detailed view includes unit USD, line USD, price status and printing verification. Compact view adds numeric columns. Price sorting supports unit/line ascending or descending; rows missing prices appear at the bottom of each sort. Original collection row ordinals are preserved for editing, irrespective of visual order.

To obtain updated price data, return to the main native window and use **Full Scryfall Sync**, then reopen the native Collection view. Old catalog snapshots are backward-compatible for ownership and set progress but do **not** magically provide prices. Their valuation status says explicitly to resync.

## Safety and tests

- Scryfall Sync continues to stage/verify/atomically publish a new independent SQLite catalog and keep its own old catalog rollback; the physical owner database is **never edited** by pricing.
- Valuation scans cached printings read-only in one bounded-memory pass, only materializing requested printing IDs; price sorting changes displayed order, not underlying ownership.
- Synthetic native regression tests include multiple copies and multiple finishes of the same printing, same-name editions at different prices, unknown finish, missing quote, digital-only ID, missing UUID, new vs stale snapshot, legacy unpriced catalog, exact subtotal and conservation of physical copies, and hash-identical source catalog/backup.
- Additional bulk-import regression confirms JSON `prices.usd` and `prices.usd_foil` survive to SQLite exactly, with null `usd_etched` retained as missing.

## Incomplete parity

Direct TCGPlayer buyer/seller price integration, condition-aware collection valuation, shortlists, Deck Lab and advanced Strategy/Synergy analysis, Arena logs and Match Performance, full sync and migration acceptance remain open in [#62](https://github.com/thejohnfavaro/arcanevault/issues/62). This remains an offline-first preview rather than feature-complete Windows 1.0.
