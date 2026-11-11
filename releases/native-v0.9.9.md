# Arcane Vault Native Windows v0.9.9 — Printing & Artwork Gallery

**Windows x64 prerelease, not a certified 1.0.** This is the next WebView feature-parity slice: view official printings and save a preferred display image without altering ownership or set counts.

## Added
- **Browse printings & artwork** from native Card Details, reached through the Card Catalog, Physical Collection, Arena Collection and Set Progress.
- A searchable-by-card-name, bounded (400 results) **printing gallery** ordered by set release, with set code, collector number, set name, paper/digital classification, and a selected-printing full artwork preview loaded on demand from the official Scryfall CDN or local image cache.
- **Only verified physically owned printings** toggle. Printings are counted owned only when their Scryfall UUID, name/set/collector, and paper status match the current native collection. Other available art may still be chosen explicitly as display art, with a warning.
- **Use selected artwork** saves a per-card preference. **Use default artwork** resets it. Saved preferences appear on subsequent Card Details visits, including after application restarts.
- Read and write the same legacy `preferredArtwork` object shape `{id,image,set,collectorNumber}` keyed by normalized card name. Preserve existing extra preference metadata and other opaque backup fields.
- Windows CI fixtures for official printing validation, bounded gallery behavior, untrusted URL rejection, old WebView preference compatibility, immutable physical/Arena/deck data, safe no-op/stale-SHA handling, reset and full SQLite rollback.

## Changed
- Artwork fetches only occur after opening Card Details/gallery and selecting a printing; no per-row network fetches in the main catalog.
- Preference saving revalidates the Scryfall printing against the current local catalog and atomically updates the native workspace via optimistic SHA protection plus a separate complete recovery copy.
- Card Details reports saved preferred artwork and refreshes its image after a confirmed gallery edit. Native Collection is reopened after a change to avoid stale edit checksums.

## Fixed
- Native Card Details previously displayed only its generic cached Scryfall image and offered no way to browse editions or apply existing legacy preferred artwork selections.

## Removed
- Nothing. The older WebView app, its local data, Android app and the physical printing selection itself remain untouched.

## Limitations
- Preferred art is a **display preference only**; it does not prove ownership of that printing or alter collector progress.
- The gallery loads up to the first **400** release-ordered printings per card; artwork URLs are only accepted from `https://cards.scryfall.io`.
- Gallery browsing requires official Scryfall printing metadata. Saving and resetting preferences require an active, verified native collection.
- Card pricing/collection valuation, all older analysis and Arena-match workflows, and full native 1.0 feature parity remain open on issue #62.
