# AVN-042 — Native Card Catalog Ownership Filters

**Status:** Windows native preview v0.9.8. This restores owned/unowned browsing without guessing physical card printings. Feature parity remains blocked on [#62](https://github.com/thejohnfavaro/arcanevault/issues/62).

## What the new selector means

| View | Meaning |
| --- | --- |
| All printings | All matching Scryfall printings; if a native collection is loaded, display verified owned copies and unresolved name-only holdings |
| Owned physical printing | Only an exact Scryfall printing ID owned in the current **physical** collection, validated against supplied name/set/collector data |
| Physical not verified owned | All printings not confirmed by a matching physical printing ID; **does not prove you own zero copies of the card name** |
| Owned on Arena (card name) | Any printed variant whose card name exists with a positive quantity in the native Arena inventory; the Arena inventory does not identify individual editions |
| Not owned on Arena (name) | Any card name missing from the Arena inventory or with zero recorded copies |

All ownership modes compose with set code, rarity, type/subtype, color identity, Arena availability, and prefix search. Results are bounded to 200 **matching** printings after ownership filtering and show whether more matches are available.

## Exact physical ownership rule

The physical holdings index is built strictly from a **verified native schema-2 workspace**. A positive physical line must have a valid Scryfall print ID. The ID must equal the catalog variant's print ID, the variant must be flagged for **paper** in the official Scryfall metadata (not a digital-only card), and its supplied card name, set and collector number must match. If an older catalog lacks paper/digital metadata, physical ownership filters refuse to guess and ask for an official catalog sync. Multiple physical finishes of the same verified printing contribute to its owned-copy total.

A row with no print UUID remains **unresolved** even when it contains a plausible set/collector hint. It may be mentioned as `Unresolved name N` on candidate card rows, but it is **never** added to an arbitrary set or credited to exact edition progress. A row with a syntactically valid ID but conflicting set/name metadata also cannot mark a catalog printing as verified-owned.

Arena is intentionally different: quantities are names, not physical printing IDs. `Owned on Arena` therefore applies to every variant matching a recorded Arena card name. Combine it with `On Arena` to restrict the visible candidates to variants Scryfall currently marks Arena-available. Neither filter claims that the card is Standard/Brawl-legal.

## Reliability

- Reads only the native collection's verified in-memory snapshot and the independently cached, read-only Scryfall catalog. Queries do not edit the collection, catalog, backup or original WebView profile.
- Refreshes the ownership snapshot whenever the native Collection view is reopened following a verified physical or Arena edit, CSV/Excel import, or restore.
- An unset or unavailable native workspace does **not** silently turn into `everything unowned`; choosing an ownership filter without that workspace fails safely with a useful message.
- Synthetic Windows CI tests cover identical card names across different printings, missing/invalid printing IDs, names and edition mismatches, mixed finishes, zero Arena quantities, combined metadata/ownership filtering, result caps after filtering and byte-identical source files.

## Outstanding parity

Per-printing artwork gallery/preferred art persistence, physical valuation, live Arena match/deck activity, shortlist and advanced Deck/Strategy/Synergy tools still require implementation and user acceptance. The native preview must not replace the current WebView Windows app yet.
