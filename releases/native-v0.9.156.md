# Arcane Vault Native Windows v0.9.156 (Preview)

## Added
- Restored Card Catalog presentation modes from the legacy experience: **Big Art**, **Small Art**, and **Text Only**.
- Configurable 25, 50, 100, or 200 catalog cards per page, with current-page count and Previous/Next controls.
- Visual-shell regression coverage for all three views, small-art responsive columns, and variable page-size SQL offsets.

## Changed
- Art views render fixed-size tiles inside virtualized rows. Small Art fits more columns without stretching cards as the window resizes.
- Text Only uses a compact name, ownership, roles, set and collector-number list without creating artwork controls.
- Changing page size safely returns to page one and invalidates cached page results; page size is included in the query-cache key.

## Fixed
- View-specific pagination no longer assumes 200 cards per page when fetching filtered catalog pages.

## Removed
- The old separate Detailed List and Compact List choices have been consolidated into Text Only. The existing full card details and in-place inspector remain available in every view.

This is a preview Windows build. The view and paging choices are presentation-only; no collection holdings, saved decks, proxies or catalog metadata are changed. Keep a private backup before updating.
