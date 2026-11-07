# Arcane Vault for Windows — v5.18.0

## Release overview

Physical Collection now shows an estimated USD collection value and a dedicated **Refresh Values** button. This release also repairs missing card details on newly imported physical cards and expands board-wipe usage detection.

## Added

- **Physical Collection → Collection Value.** Estimated collection total, priced-copy count, unpriced-copy count and date of the last price refresh.
- **Refresh Values** retrieves current Scryfall USD price estimates for confirmed physical printings, using Scryfall UUIDs or set codes and collector numbers. Foil, etched and nonfoil editions use separate pricing fields.
- **Estimated value per inventory row.** Multiples are calculated as the quote for the exact finish times the owned copy count; rows with no confirmed quote read "Unpriced."
- **Repair missing physical card details & tags.** Requests Oracle text, typing, color identity and reported format legalities for physical cards with incomplete metadata and recalculates tags.
- Physical-only cards can use the existing **Fetch card details** button, rather than being told metadata is already current.

## Changed

- Price refresh preserves a previous snapshot if network calls fail. Cached values remain visible offline, with the last refreshed date.
- The existing local Scryfall batch endpoint accepts validated UUID and set/collector-number identifiers in addition to card names.
- Physical valuation is isolated from Arena ownership. Arena-exclusive and digital-only cards never receive a paper price.

## Fixed

- Physical cards absent from Arena collection can now be enriched with official card metadata.
- Broader Oracle text detection covers additional mass-destruction and mass-exile effects for the **Board Wipe** role tag.
- Unpriced cards and unmatched finishes are excluded from the collection total rather than counted as having zero market value.

## Removed

- Nothing. Existing collections, deck lists, and match history remain intact.

## Pricing notes and limitations

- Prices shown in this version are **Scryfall USD reference estimates**, not live retailer offers, certified TCGplayer Market prices, or Card Kingdom retail/buylist prices. Direct TCGplayer/Card Kingdom quotations remain future integrations.
- A precise price requires a valid Scryfall printing ID or set code plus collector number; name-only inventory rows are deliberately unpriced to avoid assigning the wrong card printing.
- Quotes are not condition-adjusted and do not represent guaranteed sale proceeds. Some recent or specialty treatments may be unpriced even after a successful refresh.
- The price cache is stored locally; a CSV reimport preserves valid printing quotes where the printing key is unchanged. Prices change when the user explicitly refreshes.
- Card detail repairs are user-initiated; imported cards are not silently sent to a third-party service.

## Installing

Use **Import & Data → Application updates → Check for updates**, or download the verified Windows ZIP from the corresponding GitHub Release. No collection database reset is required.
