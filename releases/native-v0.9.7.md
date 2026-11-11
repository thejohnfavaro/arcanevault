# Arcane Vault Native Windows v0.9.7 — Card Browser Filtering

Native Windows x64 prerelease: restores advanced offline catalog browsing functionality from the existing WebView app. **Not** a complete Windows 1.0 or replacement for the shipping WebView app. See feature parity blocker #62.

## Added
- **Set code filter:** show printings from a chosen set without importing ownership or changing catalog files.
- **Rarity filter:** All/Common/Uncommon/Rare/Mythic/Special/Bonus. Matches the individual printing's rarity, not a different edition of the same name.
- **Type filter:** case-insensitive substring match against the cached card type line, including creature subtypes.
- **Color-identity checkboxes:** W, U, B, R and G can be combined to show matching card identities. Options: **Contains all** (e.g. W+U also finds W/U/black), **Exact colors** (only WU), **Contains any** (cards with either W or U), and a distinct **Colorless** choice.
- **On Arena checkbox:** limits printings to cached Scryfall Arena-available variants. This is an availability filter, not a claim of present-day Brawl or Standard format legality.
- Debounced combined search: all selected filters are evaluated by the same bounded read-only SQLite query, with a maximum of 200 results shown at once.
- Regression suite for combined set/rarity/type/color/Arena filtering, exact identity, printable query literals, unsupported old catalog safety and unchanged official Scryfall catalog bytes.

## Changed
- The original fast prefix-name query remains available when no advanced filters are selected.
- Legacy variant-only catalog snapshots still support ordinary name and set browsing; filters requiring unavailable type/rarity/color metadata show a safe message instead of changing ownership.

## Fixed
- The previous native catalog toolbar exposed only a name-prefix search, leaving set, color, type, rarity and Arena filtering unavailable compared with the shipping app.

## Removed
- Nothing. No ownership, inventory, Scryfall catalog schema, saved decks, backup/recovery, old WebView profile or updater is modified.

## Limitations
- Color identity is **not** mana cost or deck legality; these are offline Scryfall metadata filters.
- The 200-result cap and prefix-name search remain; large unfiltered queries are intentionally bounded for performance.
- Owned/unowned filters and richer artwork cards, pricing, wishlist, Deck Lab/Strategy/Synergy, Arena match tracking and cross-device sync remain open in feature parity issue #62.
- Current 0.9.x previews are not feature-complete or code-signed Windows 1.0. Real-PC performance and collection migration/recovery acceptance are required.
