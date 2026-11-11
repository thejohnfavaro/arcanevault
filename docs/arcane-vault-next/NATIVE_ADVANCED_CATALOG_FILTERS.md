# Native Card Catalog — combined Scryfall filters (AVN-041)

**Windows preview v0.9.7.** This restores several high-value card-browser filters from the shipping WebView application. The local Scryfall catalog is used read-only; filtering never changes ownership.

## Controls

| Filter | Operation |
| --- | --- |
| Search Printings | Indexed name-prefix search, up to 200 printings returned |
| Set Code | Exact, case-insensitive abbreviation, e.g. `CMM` or `MH3` |
| Rarity | Matches that *printing's* rarity: Common, Uncommon, Rare, Mythic, Special, Bonus |
| Type contains | Case-insensitive substring in the cached card type line; e.g. `Creature`, `Wizard`, `Artifact` |
| Arena Only | Only printings marked Arena-available by cached Scryfall, **not necessarily Brawl/Standard-legal** |
| Identity W/U/B/R/G | Combined selected color identity filters |
| Colorless | Finds cards whose cached color identity is empty; mutually exclusive with chosen colors |
| Contains all | Every chosen color must be in the card's identity; W+U finds Azorius plus 3+/4+/5-color identities |
| Exact colors | Match exactly the chosen identity, e.g. W+U finds only WU (not WUB) |
| Contains any | At least one selected color; W+U also finds mono-W, mono-U and multi-color cards with either |

Filters combine with **AND** between dimensions: searching `Sol` with Set CMM and Uncommon shows only matching printings. Color identity is supplied by the Scryfall `color_identity` property, not inferred from printed mana symbols. Every SQL parameter is bound; user-entered `%`, `_` and backslashes are treated as literal search text.

The default no-filter path remains the original bounded indexed name-prefix query to preserve browsing speed. Legacy variant-only catalog snapshots cannot filter by metadata absent from their SQLite schema; they display a helpful message to sync a native official Scryfall catalog instead.

## Test acceptance

CI fixtures use representative colorless, mono, Azorius, Boros and different printing rarities with known Arena flags. Golden checks cover exact/inclusive/any identity, type fragments, set code and rarity, multi-filter combinations, SQL wildcard escaping, 200-result pagination, unsupported legacy metadata, and byte-identical original catalog after read-only queries.

## Outstanding parity

Owned/unowned browsing, multi-printing art gallery, cached pricing/value, Deck Lab/Strategy/Synergy, wishlists and Arena log/match history are separate native vertical slices tracked in [#62](https://github.com/thejohnfavaro/arcanevault/issues/62). They cannot be considered complete on the strength of these filters alone.
