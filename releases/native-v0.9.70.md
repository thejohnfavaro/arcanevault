# Arcane Vault Native Windows v0.9.70 — Cached EDH Lab and instant deck-library return

## Added

- **Two bounded, in-memory read-only EDH Lab catalog snapshots** keyed by the actual Scryfall SQLite source fingerprint, commander name, color identity, format and a SHA-256 fingerprint of that format's recorded owned names/quantities. Unchanged repeated deck opens reuse eligibility, synergy, mana and Oracle snapshots without another full table scan.
- **Commander browse/verification caching** keyed by the same local catalog source signature. Opening a saved Commander/Brawl deck doesn't rescan the entire legendary/commander text catalog when nothing changed.
- **Deck-only fast refresh** after Save, Edit, EDH Lab Save, Duplicate or Delete. These flows reuse the already verified native replacement result and refresh only deck rows/selection and the current verified backup SHA, leaving the existing collection browser, physical/Arena inventory lists, catalog, set progress and price views mounted.
- **Source validation and fallback:** a deck-only refresh is allowed only if Physical and Arena rows, totals and artwork count are unchanged, the source path matches and the newly read preserved backup checksum matches the verified SHA. If any check fails, the existing full verified workspace reopen is used instead.
- **Automatic cache invalidation** on selected catalog changes, full sync/replacement, different commander/format/identity, different owned card names/quantities, and SQLite file/WAL metadata changes. No persisted copy of private ownership data is written by the EDH cache.
- **Regression tests** for warm cache reuse, exact pool isolation, same-format inventory changes, updated Oracle text, manual invalidation, fast verified deck-list read, stale-source rejection and UI wiring.

## Changed

- The EDH Lab status on a cold load now indicates that local suggestions are loading and will be reused on subsequent opens. Warm cached catalog access runs immediately, without the previous repeated expensive SQLite scan.
- The selected deck survives edits via its stable saved ID. The deck cover artwork still loads asynchronously; stale cover fetches cannot overwrite a newly refreshed deck list.
- Native workspace views are not disabled/destroyed after successful deck-only changes. Only a real inventory/catalog/recovery operation, or validation failure, requires the original full refresh.

## Fixed

- Substantial pause/unusable library/catalog after closing a saved deck: the full `NativeWorkspace.Open`, `CollectionCatalogReview.Examine`, `NativeSetProgressCache.Physical`, and expensive whole-view re-creation were unnecessarily rerun for every deck-only change.
- Repeated EDH Lab sessions showed "Waiting for the local format/identity-verified card catalog" while rescanning the same source and owned pool.
- Stale artwork work may no longer win a race against new deck rows after a deck-only save.

## Removed

- No saved decks, ownership, printings, set progress, prices, revisions, rollbacks, or original catalog data are changed by caching. No persistent EDH cache is created.
- No shortcuts to integrity or eligibility for actual saves: existing protected native editing, complete rollback, preflight and SHA checks are unchanged.
- Cold first-load verification is still performed; cache reuse applies only when source and recorded inventory match.

## Native Windows acceptance

1. Open a saved Commander/Brawl deck, close without saving, then reopen it. After the first eligibility load, the same commander/format/ownership combination should return to EDH Lab without a visible multi-second scan.
2. Save a deck and return to Deck Library. The existing library should stay mounted and usable immediately instead of showing "Reloading your verified local collection…"; check that the edited deck and inspector update, and that the current expected SHA allows a second edit.
3. Create, duplicate and delete a deck (after taking an ordinary recovery backup), checking that the library updates in place and that native recovery remains complete.
4. Verify the Card Catalog, Physical/Arena collection, Set Progress and stored price values do not blank/restart after a deck-only save. Check that covers refresh asynchronously without changing selected deck to a stale row.
5. Switch between Physical Commander and Arena Brawl in EDH Lab. No recorded quantities, eligible cards or color identity should leak between cache entries. Refresh Scryfall metadata or change inventory and confirm the advisory catalog refreshes.
6. Test with a cold catalog or missing metadata: the app should still show an honest loading/unavailable message, not invent card legality. Full Sync remains the source of truth for missing metadata.
7. Check resize, closing an unsaved editor, repeated quick saves, and update/restart behavior. No persistent app data is written by the in-memory EDH cache.
8. If native source verification fails or an unrelated inventory change is detected, confirm the app falls back to the original full verifier rather than using a stale SHA or cached authorization.

**Scope:** Native Windows v0.9.70 preview. CI plus actual Windows performance/DPI regression are required before any stable 1.0 declaration.
