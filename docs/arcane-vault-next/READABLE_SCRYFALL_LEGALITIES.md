# AVN-039 — Readable Scryfall Card Details metadata

## Problem

The official Scryfall bulk catalog stores format legalities as a compact JSON object. Native Card Details previously interpolated that raw JSON directly into the metadata text, rendering confusing strings such as `"standard":"not_legal","future":"not_legal"`.

## Solution

Native Card Details now separates normal card metadata from **FORMAT LEGALITIES**. A read-only presentation adapter, `NativeFormatLegality`, safely parses the cached JSON, labels each format, and displays color- and text-coded **Legal / Not legal / Banned / Restricted / Unknown** statuses. The panel can expand/collapse and scroll independently, without pushing Oracle text out of view.

Names such as `future`, `standardbrawl`, `paupercommander` and `oathbreaker` have readable labels. Unrecognized future formats display a human-readable fallback; unexpected status strings display **Unknown** and are never misrepresented as legal. Missing or malformed JSON shows a safe explanatory message, not raw data.

**No data migration is required.** Scryfall's JSON is stored exactly as before. Deck preflight, format legality checks, independent catalog files, inventory, ownership and all recovery files remain untouched.

## Example

| Scryfall field | Card Details display |
| --- | --- |
| `"standard":"not_legal"` | Standard — Not legal |
| `"future":"not_legal"` | Future Standard — Not legal |
| `"modern":"banned"` | Modern — Banned |
| `"commander":"legal"` | Commander — Legal |
| `"vintage":"restricted"` | Vintage — Restricted |

Format status comes from the last local Scryfall sync, not a live banned-list query. The UI states this limitation.

## Acceptance

Open Card Details for several cards with differing cached format legality. Confirm meaningful labels/statuses, long-format list scrolling, readable Oracle text, unavailable metadata behavior and safe ownership counts. CI also verifies the parser's logic and an unchanged Scryfall SQLite catalog hash after reads.

## Card Details activation

**Double-click a real card row** in the native Card Catalog, Physical Collection, Arena Collection or Set Progress collector-slot list to open Card Details. The row is hit-tested before opening so blank space/headers cannot unintentionally open a previously selected card. The existing Card Details buttons continue to work. This is a WPF interaction change, not a data migration.

**Parity limitation:** Readable legalities and double-click are targeted usability fixes, not proof of full replacement for the older WebView app. Track missing older modules at [#62](https://github.com/thejohnfavaro/arcanevault/issues/62).
