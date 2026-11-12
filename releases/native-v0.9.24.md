# Arcane Vault Native Windows v0.9.24 (Preview)

Combined Windows native Deck Studio, navigation, cache, and reliability release built from the merged PRs #101, #102 and #103.

## Added

- In-editor, read-only card preview showing type, mana value, color identity, Oracle text, selected ownership pool and draft count.
- Official Scryfall artwork preview with cancellation and short selection debounce; Arena uses an Arena-available representative image rather than potentially paper-only artwork.
- Add (+1), remove (−1), and alphabetical sorting controls for unsaved Deck Studio mainboard drafts.
- Collapsible Card Database, Data & Sync, Native Updates, and Options groups in the sidebar while core collection, deck, set progress and catalog navigation remain directly available.
- Deck Library summary caching keyed to the verified workspace backup SHA, mirroring the existing disposable Set Progress cache approach.
- Commander / Arena Brawl deck covers use cached official commander card artwork when available, with the existing stylized deck box retained as the fallback.
- Synthetic large-collection import/export round-trip regression (4,000 physical rows, 4,500 Arena rows, 64 decks) with recorded performance timings.

## Changed

- Administrative functions now live in categorized sidebar expanders; the same existing event handlers and confirmations are preserved.
- Deck Library images reuse the existing bounded official-art cache and virtualized on-screen loading, avoiding background requests for off-screen deck covers.
- Deck-summary and commander-cover caches are presentation-only, disposable, and invalidated by saved workspace/catalog identity.
- The native release version advances to 0.9.24 to ensure the combined three-PR build has its own immutable GitHub release tag.

## Fixed

- More descriptive failures for malformed native release and manifest JSON, wrong data types and untrusted artwork URLs.
- Reject malformed updates without publishing a version folder or changing user collection, catalog or recovery files.
- Late card image requests do not overwrite more recent selections; library selection persists when art metadata arrives.
- Draft decrement and sorting reject invalid lines rather than silently discarding or changing saved deck data.

## Removed

- No collection, card catalog, Set Progress, deck editing, backup, native updater, recovery, or import/export features were removed.
- No existing inventory quantities, printing identities, preferred artwork or saved-deck revisions are rewritten by these new caches.
- No Android or EDH Lab work is included.

## Manual verification

1. From v0.9.22, use Check Native Updates and verify a checksummed side-by-side v0.9.24 installation without touching the active collection.
2. Verify the core Card Catalog, My Collection, Deck Library and Set Progress sidebar entries remain visible; expand each administrative tools category.
3. Open Deck Library with existing Commander and Arena Brawl decks; check art, fallback, favorite/filter, deck selection, editing, saving, and reopen.
4. Add/remove/sort an unsaved deck, inspect card preview, rapidly change selected card, and verify no stale artwork.
5. Test ownership shortages, mana/color/type analytics, and stale-chart invalidation.
6. Export a full verified backup; compare physical/Arena counts, owned printings, preferred art, saved deck revisions and recovery history.
7. Record or report any regression before further steps toward v1.0.

Release is a **preview**, not evidence that all real-hardware acceptance checks have passed. Preserve a separate full backup before upgrading.
