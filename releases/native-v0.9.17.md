# Arcane Vault Native Windows v0.9.17 (Preview)

Set Progress performance and visual parity update.

## Added
- Persistent, independently scoped Physical and Arena **Set Progress summary caches** in `%LOCALAPPDATA%\ArcaneVaultNext\cache\set-progress\`. Cache entries are keyed by native backup SHA-256 and official catalog metadata, not executable version. A native app update therefore does not force recalculating unchanged set summaries.
- **Full-bleed set artwork banners** using the official Scryfall illustration crop of a representative card from the set, with gradient overlays for readability.
- **Official Scryfall set symbols** in the set tile emblem, rendered as WPF vector paths with a stable local symbol cache and readable set-code fallback.
- Denser but readable set tiles with set names, release dates, completion percentage, counts, and progress bars.

## Changed
- Set Progress tiles use full-width artwork rather than the old small card-art ID box and generic star motif.
- Visible banners use bounded artwork resolution; official set symbols are loaded once, cached outside executable versions, and reused in future sessions.
- Existing set checklist and Physical/Arena scope behavior remain intact. Both modes continue to calculate only the evidence supported by their respective collection data.

## Fixed
- Avoids repeating the expensive all-sets summary aggregation after an application update when the collection backup and Scryfall catalog have not changed.
- Missing/corrupt/outdated cached results safely trigger a verified rebuild instead of losing collection data.
- Cache writes are disposable and independent from the authoritative collection database.

## Removed
- Generic star-like set emblem and small artwork-only ID-box presentation in the Set Progress overview.

## Test and verification
- Confirm official symbols and cropped artwork display for familiar sets and remain present after restarting or updating.
- Inspect the Physical and Arena set scopes independently; they have separate persistent summary caches.
- Confirm a changed collection or refreshed Scryfall catalog invalidates the summary and rebuilds it.
- On first visit after this update, the cache has to initialize. Opening a selected set still loads its checklist on demand.
- The banner uses representative card illustration, **not guaranteed licensed promotional set art**. The symbol and card art are presentation only, not evidence of owned printings.
- This is a native Windows preview, not version 1.0. Existing collection, saved decks and recovery points remain independent of executable updates.
