# AVN-045 — Native My Shortlist

**Version:** native Windows v0.9.11 preview. **Parity gate:** [#62](https://github.com/thejohnfavaro/arcanevault/issues/62).

## Legacy compatibility

The shipping WebView **My Shortlist** uses the full backup's top-level `favorites` array, containing plain card names, with `Copy as Arena list` writing `1 Card Name` per favorite. The native implementation intentionally reads and writes this **same** structure. No new wishlist database or hidden localStorage migration is introduced. Missing `favorites` means an empty shortlist. Existing order/casing/unknown top-level fields are preserved when adding or removing cards; edits are case-insensitive to avoid accidental duplicate names.

## Native workflow

Use the **My Shortlist** button in the main Windows sidebar. The list shows each saved name alongside quantities from the native Arena inventory and the verified physical collection. The status distinguishes verified physical paper copies from **unresolved** physical records. Search your saved names, or enable **No verified owned copies** to find names without confirmed paper copies or Arena quantities. This filter does not claim there are zero unresolved physical copies.

From the main **Card Catalog**, select an actual result and choose **Save selected to shortlist**. The same bookmark toggle appears inside **Card Details** from catalog, physical, Arena and Set Progress. A card may be bookmarked even if already owned; the shortlist is for planning, not inventory.

To remove a bookmark, select its row in **My Shortlist** and choose **Remove bookmark** (confirmation required), or toggle the bookmark from Card Details. The saved view also offers **Card details**, **Copy as Arena list**, and **Refresh ownership**. Export preserves names even when a particular card isn't in the current local catalog.

## Data safety

- Full legacy schema-2 backup is read and validated. The bookmark transform alters only `favorites`, maintaining owned paper/Arena cards, decks, deck revisions, artwork preferences and unknown fields.
- Every edit validates the caller-observed SHA, creates a separately verifiable replacement native SQLite workspace and retains a complete rollback. Stale, no-op or malformed requests reject without writing.
- Main/Collection/Shortlist windows refresh after bookmark changes so future inventory edits never rely on an outdated SHA.
- No editing of installed WebView profiles, catalog snapshots or Android collection. No ownership is inferred from bookmarks.
- CI covers all of these behaviors with real schema-2 full backup and local Scryfall paper-vs-digital verification fixtures. Real Windows machine checks (existing migrated favorites, clipboard, large shortlist and restore) remain required before 1.0.

## Known gaps

Legacy WebView My Shortlist basic bookmarking is restored. Advanced collection-aware recommendation candidates, Deck Studio card picker, Archetypes/Synergy Lab, Match Performance, Wildcard Planner, Strategy, Data & Sync parity and complete Windows 1.0 acceptance remain open in [#62](https://github.com/thejohnfavaro/arcanevault/issues/62).
