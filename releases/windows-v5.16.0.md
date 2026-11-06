# Arcane Vault for Windows — v5.16.0

## Release overview

See your Magic cards across digital and paper collections, filter the Card Browser by availability, and goldfish with full card artwork rather than text-only hands.

## Added

- **Streamlined sidebar navigation.** All 14 existing pages are organized under collapsible Collection, Decks, Strategy, Play & Analytics, and Application groups. Use **Ctrl+K** to find a page, pin up to five favorite destinations, or switch to a compact icon-only sidebar. Navigation preferences persist between launches.

- **Separate Arena and physical ownership counts.** Each Card Browser card and the Card Details window displays both **Arena owned** and **Physical owned**, including zero counts. Physical quantities are aggregated across recorded printings without modifying Arena ownership.
- **Physical-owned cards in the Card Browser.** Cards found exclusively in your physical inventory now appear when viewing owned cards, even if they are not in your Arena collection.
- **Full-sized Magic card artwork in Goldfishing.** Opening hands and drawn cards display the complete card image. Names and card types remain accessible beneath each image, with text fallback when artwork cannot be loaded.

## Changed

- **Sidebar presentation only.** Page destinations remain intact; selecting a page highlights it and expands its section automatically. No deck, collection, or match-tracking data is changed by the navigation refactor.

- **Availability filter.** Renamed **Arena availability** to **Availability**, with **All Magic Cards**, **Arena Only**, and **Physical Only** choices. Arena Only uses Arena-listed catalog results; Physical Only displays the cards recorded in your paper inventory.
- **Ownership summary.** The Card Browser shows separate Arena and physical copy totals, and list view presents the two quantities side by side.
- **Card details.** Owned printings are labeled by source; Arena-owned set information is distinguished from physical copy counts.
- **Goldfishing layout.** Responsive full-card artwork replaces compact name/type-only tiles.

## Fixed

- **Physical-only card details.** Cards that exist solely in the paper collection can open Card Details instead of requiring an Arena ownership record.
- **Missing physical counts in catalog views.** Browsing Scryfall catalog results now displays physical inventory counts alongside Arena counts.

## Removed

- **Text-only Goldfishing hand tiles.** Replaced by full card images with a text fallback. No saved cards, matches or deck information are removed.

## Important details

- **Existing data is preserved.** This update does not merge Arena and physical inventories, change match statistics or automatically alter deck ownership.
- **Catalog availability is distinct from ownership.** An Arena-listed card may have zero Arena-owned copies; Physical Only refers to your imported paper inventory.
- **Online art fallback.** Card images not cached locally may require internet access from Scryfall.

## Installing

Open **Import & Data → Application updates → Check for updates**, review these notes and choose **Download and install update**. Windows release packages continue to be verified using their SHA-256 checksum.
