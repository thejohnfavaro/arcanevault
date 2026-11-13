# Arcane Vault Native Windows v0.9.74 — EDH Mainboard Card-Art Thumbnails

## Added
- **Compact official card-art thumbnails directly in the EDH Lab → Manage mainboard rows** beside the card name, current quantity and local type/role hints.
- Illustrated rows keep the existing **type group headers, visual sorting, filters, card preview, +1 / −1 / Remove all**, and the separate editable Text tab.
- **Styled thumbnail placeholder** for cards with absent/unmatched artwork or offline connections. No blank pane or blocked deck action.
- **On-demand, visible-row-only artwork fetches.** The existing WPF virtualized/recycled card list owns row lifetime. Artwork begins when a visual row is realized and cancels immediately if its image is unloaded, reused for another card, or the editor closes.
- **Bounded image resources:** at most three concurrent metadata/artwork fetches per editor, 128 in-memory decoded thumbnail entries, frozen 64-pixel-wide bitmap decoding and existing 160MB Scryfall artwork disk cache. On repeat views and sorting, warmed art stays in memory; card list refreshes do not reload full-size preview artwork.
- Only trusted HTTPS `cards.scryfall.io` assets are considered. Arena Brawl thumbnails resolve the representative Arena-eligible printing rather than silently borrowing a potentially physical-only card image.

## Changed
- The Manage row uses a small fixed-size image tile (40×56) at the left, keeping card names, copy counts and role hints compact. Type group headings don't load thumbnail artwork.
- Thumbnail requests are presentation-only and do not affect background draft analysis, local format/commander eligibility checks, or the native deck-save gate.
- An artwork failure only leaves the placeholder; it cannot modify card ownership, printings, draft quantities, or saved revisions.

## Fixed
- Visual mainboard management previously showed only names and numeric quantities. This adds immediate visual recognition of cards and an image cache designed not to introduce the previous UI-blocking catalog reload behavior.
- Recycled or rapidly filtered rows no longer retain a previously realized card image. A stale async request can't write to a row reused for a different card.

## Removed
- No card data, ownership, deck rows, printing identifiers, prior artwork cache, recovery versions or saved deck history were deleted.
- No new network dependency for deck editing or separate persistent mainboard representation.

## Windows acceptance
1. Update to native-v0.9.74 and open an existing Commander or Arena Brawl deck in EDH Lab → Manage.
2. Verify visible mainboard cards gain small card-art images (or placeholder for missing art), while category headers remain text-only. New image requests must not block scrolling or quantity buttons.
3. Scroll rapidly through a large deck, change card-type filters, sort by Name/Quantity/Type, and switch between Manage and Text. No row should show another card's art.
4. Add cards from the Cards browser and remove or adjust mainboard entries. The new/updated rows get the correct art; selecting them still drives the existing full card inspector.
5. Close EDH Lab during thumbnail loading, or disconnect the internet and reopen. The app should remain responsive and usable with safe placeholders.
6. Open the same deck a second time. Previously fetched official images should reuse the on-disk artwork cache. The initial startup should not eagerly fetch artwork for all 100 cards.
7. Confirm Arena Brawl only uses Arena-eligible artwork, no card-art metadata is interpreted as ownership of that printing, and no copy-limit/preflight behavior changed.
8. Verify Native Updates, deck-save verification, backups, and the fast return to Deck Library still work.

**Scope:** Native Windows 0.9.74 preview; automated native/Windows CI and hands-on WPF regression required before stable 1.0.
