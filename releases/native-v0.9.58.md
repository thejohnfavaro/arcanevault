# Arcane Vault Native Windows v0.9.58 — Full catalog pagination

## Added

- **Previous / Next** navigation below the native Card Catalog, with a visible page number, current display range and an explicit *More matches available* / *End of results* indicator.
- Stable page-by-page read-only catalog queries with 200 results per page. Navigate past the former first-200 cap for every printings query, including combined set, rarity, type, color identity, utilization-role, Arena-availability and ownership filters.
- Dedicated paging regression fixtures containing 480 individual physical printings and 240 distinct Arena card names. Tests ensure no skipped/duplicated printing identities, correct page boundaries and intact source catalog bytes.

## Changed

- Every change to a search term or filter returns to page 1; Previous/Next keep exactly the same search and filter settings.
- Query results use stable name, set, collector **and printing-ID** ordering, including when many variants share card information.
- Arena catalog display continues to deduplicate paper/Arena printings into one entry per card name, with the offset applied to the *eligible distinct names* rather than raw variants.
- The existing virtualized detailed, compact and card-art layouts now show the selected page without instantiating all card images or loading the entire catalog into UI memory.
- While a query is pending, page navigation buttons are disabled so late requests cannot silently display stale results.
- Synthetic demonstration data uses the same 200-row paging model instead of allocating a full visible list.

## Fixed

- The Card Catalog previously stopped after 200 matching printings, showing `First 200 matching results` but offering no way to see the rest.
- Pagination for selective role/ownership and Arena-deduplicated queries now skips *matching* results, not arbitrary raw SQL rows.
- Stable ordering across pages prevents records with identical names, sets and collector numbers from repeating/being omitted when scrolling between pages.

## Removed

- The misleading `First 200 matching results` status and an old documentation comment implying the catalog could not browse beyond 200 records.
- No existing catalog data, user collections, deck data, theme settings, shortlist state, or recovery files were modified.

## Acceptance

1. In **Card Catalog**, choose no filters and no search term. Page 1 shows up to 200 catalog printings.
2. Click **Next** to page 2 and **Previous** to page 1. Continue to the end; the final page disables Next, and the first page disables Previous.
3. Filter by color identity, rarity, set, card type and card-utilization role. Confirm the controls return to page 1 and every page retains the chosen filters.
4. Enable **Arena only** and browse past page 1, confirming a single catalog entry per eligible card name (not one per printing).
5. Try Detailed list, Compact list and Card art views on a later page; all should keep the page number and virtualize the visible rows.
6. Confirm your physical/Arena ownership, favorites and decks are unchanged. The catalog search is read-only; no collection write or recovery point should occur.
7. Check that the UI remains responsive during navigation on your real catalog and display scale.

**Scope:** Native Windows preview. This change pages catalog results rather than increasing page memory indefinitely. CI synthetic tests do not replace Windows UI acceptance on the installed device.
