# Arcane Vault Native Windows v0.9.85 — Catalog proxies and symbol toggles

## Added
- **Card Catalog → Collection Ownership → Owned physical printing → Include Proxies** offers an optional union of exactly verified physical printings plus name-level paper proxies recorded in the verified native workspace. Proxy-only catalog matches appear once per name, as a representative *paper* printing; **Proxy ×N · not owned** remains clearly distinguished from real owned copies.
- Icon-only color identity toggle buttons for W/U/B/R/G and colorless, with accessible names, tooltips, explicit selected styling, hover and keyboard-focus feedback.
- Regression checks for proxy union, exact physical IDs, source isolation, representative rows, Arena exclusions, and toggle semantics.

## Changed
- Catalog ownership lookup now indexes the existing proxy ledger when loading the native collection; changing catalog filters does not repeat the full collection reload.
- Color matching continues to support Contains all, Exact colors, and Contains any. Selecting colorless clears colored selections, and selecting a colored identity clears colorless.
- Include Proxies is available only alongside Owned physical printing. Other ownership modes preserve their original meaning.

## Fixed
- Names represented only by paper proxies no longer disappear from the verified-owned filter when Include Proxies is checked, provided an official Scryfall catalog paper printing exists.
- Proxy presence cannot fabricate an owned printing, price, set completion or Arena inventory entry.

## Removed
- Word-and-checkbox controls for individual Card Catalog color identity choices; symbol-only toggle buttons replace them.

## Safety
- The filter is strictly read-only. Proxy counts remain excluded from verified physical ownership, collection valuation, price scraping, and set progress.
- Third-party cards with no matching official Scryfall catalog card or paper printing remain in the Proxies inventory; the catalog cannot supply a genuine printing for those names.
