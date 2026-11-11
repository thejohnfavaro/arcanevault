# Arcane Vault Native Windows v0.9.22 (Preview)

Collection-aware native Deck Studio: owned-card selection, live shortages, offline deck metrics and color-coded visual analytics.

## Added
- Search owned cards directly in the native Deck Editor, with separate Physical and Arena inventory pools and per-name owned totals.
- Add a selected card to an unsaved deck without touching collection ownership; duplicate card names increment their counts.
- Live mainboard/commander totals and per-card missing-copy information against the selected inventory.
- Analyze deck using a local, read-only Scryfall catalog: mana curve, average nonland mana value, color identity distribution, and exclusive primary card types.
- An expandable three-column WPF chart panel with value labels and colored, consistently scaled bars.
- Regression checks for chart scales, unknown catalog data, copy-weighted metrics, inventory isolation, and XAML bindings.

## Changed
- Native Deck Editor opens wider and can reveal analytics below the deck editor while keeping editing and save actions available.
- Analytics count deck copies, not only distinct card names, and include the separate commander only in Commander / Arena Brawl.
- Chart calculations now live in a WPF-independent model; WPF brushes are added only in the desktop presentation layer.

## Fixed
- Stale statistics are hidden when card text, commander or format changes, and late analyses are prevented from replacing newer draft results.
- No data is fabricated when offline metadata is absent; unknown copies and catalog errors are shown explicitly.
- Zero-card and zero-metadata charts retain safe, consistent bar scales.

## Removed
- Nothing from the prior native collection, Set Progress, Deck Library, import/export, updater, or recovery features.
- No ownership, printings, preferred artwork, or deck history is modified by analytics or browsing.

## Verification
- From Deck Library, open or create a deck, switch Physical/Arena, search and add an owned card; confirm mainboard counts and shortages.
- Click Analyze deck and expand the charts; check curve excludes lands, type counts are exclusive, and color identity totals reflect known copies.
- Edit the deck while an analysis is pending; confirm old charts are hidden or marked stale.
- Save through the existing review flow, then reopen and verify that original collections and deck revision history were preserved.
- Windows CI and a hands-on smoke test are required before publishing this preview release.
