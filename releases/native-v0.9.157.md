# Arcane Vault Native Windows v0.9.157 (Preview)

## Added
- Regression guard requiring catalog pagination size to be captured on the WPF UI thread before dispatching asynchronous queries.

## Changed
- Catalog search and synthetic paging use an immutable local page-size snapshot during background work.

## Fixed
- Critical Card Catalog regression introduced in v0.9.156: the catalog appeared empty and reported a query failure even when a local catalog with more than 120,000 printings was loaded. Background threads no longer access the cards-per-page WPF dropdown.
- Big Art, Small Art, Text Only, and 25/50/100/200 items per page remain available.

## Removed
- Nothing. Collections, proxies, decks and catalogs are never rewritten by this fix.

This is a native Windows preview. Reopen the catalog after updating to confirm your local results and art; keep a private backup before updating.
