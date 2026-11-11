# Arcane Vault Native Windows v0.9.11 — My Shortlist

Native Windows x64 preview; restores the shipping WebView **My Shortlist** card-planning workflow. This is NOT a full 1.0 feature parity release.

## Added
- **My Shortlist** in the native sidebar: list saved card bookmarks, search by name, review verified physical/possible unresolved physical/Arena ownership, and filter to card names with **no verified owned copies**.
- Bookmark a selected name from the native Card Catalog; add or remove bookmarks directly in Card Details opened from the Card Catalog, Physical Collection, Arena Collection, or Set Progress.
- Remove a saved bookmark with confirmation; every edit is atomic and creates a full independent native SQLite rollback. Saving a bookmark does **not** add physical or Arena copies.
- **Copy as Arena list** reproduces legacy behavior: one copy of each bookmarked name, in saved order, even when the name is absent from the current Scryfall catalog.
- Refresh ownership separately from bookmarks; distinguishes physically verified Scryfall printing IDs from unresolved physical holdings, and Arena inventory counted by card name.
- Native regression fixtures for existing WebView favorites, legacy name/order preserving round trips, copy/export, missing/invalid favorites, case-insensitive duplicates, verified/unresolved/Arena status, no-op and stale-SHA rejection, full recovery and unknown-field preservation.

## Changed
- Saved shortlist entries continue using the **existing full-backup `favorites` array**—no second wishlist store, no lossy migration, and no changes to the installed legacy WebView profile.
- A bookmark change from Card Details invalidates stale native Collection edit checksums; affected collection windows are refreshed from newly verified SQLite.

## Fixed
- Native Windows previously had no My Shortlist screen or reusable bookmark control; saved legacy favorite card names could be present in the backup but were invisible.

## Removed
- Nothing. The old WebView Windows app, physical collection, Arena inventory, deck library, artwork preferences, official catalog and recovery files remain intact.

## Known limitations
- Bookmarking is **card-name based** as in the shipping app, not per-printing/finish. Saved names without local metadata are preserved and can still be removed/exported, but Card Details may show no cached metadata.
- 'No verified owned copies' means no confirmed physical paper printing and no recorded Arena copies; unresolved physical records are clearly marked as **possibly owned**, not silently treated as zero.
- Full catalog selection and Scryfall cards-only name search are independent of shortlist. There is no new candidate auto-recommendation or deck synergy scoring here.
- Native Windows 1.0 remains gated on outstanding Deck Lab, Match Performance, Wildcard Planner, Strategy, Synergy, Archetypes, Data & Sync, set progress parity and real device collection acceptance in issue #62.
