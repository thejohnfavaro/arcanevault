# Native printing and preferred artwork gallery — AVN-043

## Goal

Recover the shipping WebView application's per-card preferred artwork feature in the native Windows app **without guessing owned editions or modifying inventory**. Native version: 0.9.9 preview. Feature-parity gate: [#62](https://github.com/thejohnfavaro/arcanevault/issues/62).

## Workflow

Open Card Details from Catalog, Physical Collection, Arena Collection or a Set Progress card. Select **Browse printings & artwork**. The new printing gallery reads cached Scryfall `variants` metadata and displays set, collector, paper/digital classification and a full selected card image. Optionally enable **Only verified physically owned printings** to limit the list. Select an edition to preview it; no collection write occurs on selection. Press **Use selected artwork** to commit a display preference, or **Use default artwork** to remove it.

For a printing not confirmed physically owned, the UI explicitly warns that an artwork selection is not an ownership edit. Owned printings require an exact Scryfall UUID and matching known set/number details, with official paper status. Unknown printing IDs and name-only physical inventory are never counted as owned.

## Legacy compatibility

The full schema-2 backup's original `preferredArtwork` object is preserved. Its per-card value retains the WebView format:

```json
{
  "preferredArtwork": {
    "sol ring": {
      "id": "00000000-0000-4000-8000-000000000901",
      "image": "https://cards.scryfall.io/normal/front/1/1/example.jpg",
      "set": "cmm",
      "collectorNumber": "396"
    }
  }
}
```

Existing legacy preferences are read without a schema migration. Saving a new preference changes only the chosen card's four known preference fields, preserving other fields in that preference, all other preferences, owned copies, decks and revisions, and unknown top-level data. The chosen printing ID and its exact card name/image are revalidated against the **current** local catalog immediately before saving.

Native writes are optimistic-SHA guarded and create an independent full SQLite recovery point. Reset removes only the selected card's preference. Incomplete/no-op/stale writes reject without modifying native collection data. No changes occur in the installed WebView application's profile.

## Safety / performance

- The gallery uses local, read-only SQLite variant queries with a maximum of 400 displayed entries and newest-first ordering.
- The selected printing's image alone is loaded on demand through the existing size-bounded HTTPS-only official Scryfall CDN and cache handler. Missing artwork does not break card text or owned inventory.
- Artwork preferences do not change the physical or Arena collection, Set Progress, deck-library state, source Scryfall SQLite database, or legacy WebView data.
- Once the native app refreshes a chosen preference, future Card Details opens use that saved image before the default cached card identity image.
- Full Windows CI tests verify the above; a real Windows collection import, gallery interactions, offline cache display and restore are still required for 1.0 acceptance.

## Remaining WebView parity

Card price/physical collection valuation, more complete deck workflows, shortlists, Deck Lab, Strategy/Synergy tools, Wildcard Planner, Arena logs/match performance, and native data & sync parity remain **incomplete**; see [#62](https://github.com/thejohnfavaro/arcanevault/issues/62).
