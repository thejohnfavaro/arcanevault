# Arcane Vault Native Windows v0.9.60 — Card-first catalog and mana identity

## Added

- **Native mana-color identity symbols** in Card Details for W/U/B/R/G and colorless, with a readable color-name label and accessibility names. Symbols are local vector artwork; there are no additional network calls, downloaded fonts or remote image dependencies. These denote **color identity**, not a card's mana cost.
- **Printing rarity badge** overlaid within Card Catalog art and displayed prominently in Card Details. Common, uncommon, rare and mythic printings have differentiated labels and tones, based on each exact catalog printing (not guessed from the card name).
- Native regression checks for WUBRG parsing, the colorless fallback, unknown identities, missing-data safety, card details artwork space, source rarity and paging/virtualization compatibility.

## Changed

- **Card Catalog:** artworks grow from 132 × 184 to 200 × 278 in a compact 366-pixel tile. The art is the primary element, not a small image lost in a large padded panel. Card name, utilization tags and ownership fit into a tighter footer; rarity/set badges appear directly over the artwork. Shortlist stars stay over the top corner.
- **Removed the noninteractive "YOUR COLLECTION. YOUR RULES." hero banner**, pulling search and results upward to recover meaningful vertical workspace.
- **Arena filter:** renamed misleading *On Arena* checkbox to **Arena catalog (one per card)**. Its existing behavior is preserved: show Arena-available cards once per card name. It is **not** the Arena ownership filter; Collection Ownership still filters your recorded Arena quantities.
- **Card Details:** larger window and 445-pixel artwork pane; data-driven rarity badge and compact icon-based color identity replace the plain WUBRG text.
- **Deck Library:** enlarge deck-art covers from 85 × 109 to 108 × 145, simplify card frames, and theme fallback cover illustrations instead of fixed purple.
- Keep catalog pagination at 200 results per page, row virtualization in card-art view, cached artwork requests, scoped card edits, theme choices and sidebar navigation.

## Fixed

- Cards appeared much smaller than their surrounding catalog tiles, with unused space devoted to decorative padding.
- Card Details displayed raw color identity letters without immediately recognizable colored mana symbols.
- Arena availability and Arena ownership could be mistaken for the same condition due to the prior ambiguous checkbox label.
- Fixed purple fallback deck covers broke the appearance of the black/charcoal, ember and other mana identity themes.

## Removed

- The decorative catalog marketing hero and its unused vertical footprint.
- No card, printing, ownership, deck, theme, recovery, shortlist or catalog data was removed. No changes to the meaning of existing search filters.

## Windows acceptance

1. Check **Card Catalog** at default window size and maximized. Confirm the noninteractive hero is gone, the search panel is higher, art dominates the tile, and names and badges aren't clipped.
2. Browse more than one 200-result catalog page in **Card art**, **Detailed**, and **Compact** views, resizing the window and verifying scrolling responsiveness.
3. Favorite/unfavorite a card directly on its artwork; click a tile and double-click to open details.
4. Compare a common, an uncommon, a rare and a mythic printing; verify displayed rarity matches the exact printing and changes when the set/collector changes.
5. In Card Details check one color, multiple colors and colorless cards: identity mana glyphs display in canonical WUBRG order, with accessible names; unknown metadata remains honest.
6. Toggle **Arena catalog (one per card)** and compare with **Collection Ownership → Owned on Arena**; availability is not an ownership claim.
7. Open the Deck Library, including decks without cached art; confirm enlarged covers, responsive themes and working editing.
8. Regress **Arcane Noir**, **Black**, **Daylight Archive**, and other chosen themes. Verify no hardcoded purple and no stale brush/update failures.
9. Verify ownership, decks, backups, preferred printings, wishlist/shortlist and catalog data are unchanged by browsing.

**Scope:** Native Windows v0.9.60 preview. Automated compile and regression tests do not replace on-device visual inspection and scaling checks.
