# Arcane Vault Native Windows v0.9.26 (Preview)

This combined preview includes the previously merged native v0.9.25 work (appearance themes and compact Catalog tiles), plus improvements to utilization filtering and inline shortlist bookmarking. It builds upon the locally verified v0.9.24 release.

## Added

- Standalone Options screen with live theme previews, Save, Cancel, and Restore Default; five classic Arcane Vault themes and all 32 Magic color-identity palettes.
- An overlayed ☆/★ bookmark control on each Card Catalog artwork tile, saving the card name through the existing native shortlist workflow without changing ownership.
- Matching shortlist status across multiple printings of the same card name.
- A repeated-printing utilization-role regression fixture containing 1,800 synthetic printings and read-only catalog checks.

## Changed

- Card Catalog artwork tiles use a more compact 302-pixel height, 132 × 184 artwork, tighter padding, and up to five columns depending on available space.
- The large Catalog Status area, standalone View selected card action, and standalone Save selected to shortlist button are replaced by inline stars and a smaller status message in the bottom workspace footer.
- Double-clicking a card outside its star continues opening card details.
- Utilization role regexes are compiled and cached instead of reconstructed for every printing.
- Role labels are derived once per card name per catalog snapshot and reused when filtering repeated printings or switching tags.
- Completed dropdown and checkbox filters skip the text-entry debounce; typing retains debounce.
- Appearance settings remain separate from native collection, deck, catalog, and recovery data.

## Fixed

- Reduced delays when selecting Card Catalog utilization tags, particularly when the catalog contains many printings per card name.
- Avoided materializing card rows and resolving ownership for printings that do not match the selected utilization tag.
- Invalid or missing appearance preferences safely use the default Arcane Noir theme without modifying the collection.
- Star clicks do not trigger the card-details action, and saving a name already bookmarked does not create duplicates.
- Catalog and native-collection source data remain unmodified by browsing, role filters, artwork display, and theme selections.

## Removed

- The redundant Catalog Status heading and large View selected card / Save selected to shortlist controls from the main Catalog filters area.
- No collection ownership, printings, saved-deck history, imports, recovery, Set Progress, Deck Studio, or native updater functionality was removed.

## Manual verification

1. Upgrade from v0.9.24 using Native Updates. Confirm the version is v0.9.26 and review cards, deck library, and backups after upgrade.
2. In Options → Appearance & Themes, switch between dark/light and mono/multicolor palettes; check preview, Cancel, Save, and restart persistence.
3. Switch Card Catalog to Card art. Confirm compact tile rows reflow as the window resizes; scroll and double-click a card to open details.
4. Click ☆ on a card art tile; confirm ★ appears on all displayed printings of that name, and the card appears once in My Shortlist. Confirm ownership remains unchanged.
5. Switch Utilization Tag among Ramp, Board Wipe, Tutor and Card Draw; compare responsiveness, result accuracy, and combinations with color/owned/set filters on the actual catalog.
6. Ensure catalog progress and error messages remain readable in the footer, including during full Scryfall sync and native updates.
7. Keep a full private native backup; do not use the preview as proof of v1.0 parity.

Automated Windows and .NET 10 regression checks are required, as is real-device smoke testing. This release is an unsigned native Windows preview.
