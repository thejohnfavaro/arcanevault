# Arcane Vault Native Windows v0.9.8 — Printing-Safe Ownership Filters

Native Windows x64 prerelease: restores physical/Arena owned/unowned browsing in the Card Catalog. This remains a feature-parity **preview**, not a production 1.0 or replacement for the shipping WebView app.

## Added
- Native Card Catalog **Collection Ownership** selector: All printings, Owned physical printing, Physical not verified owned, Owned on Arena (card name), and Not owned on Arena (name).
- Per-printing ownership labels in Detailed, Compact and Cards/Tiles views showing verified physical copies, Arena name-based copies, and unresolved physical holdings by name.
- Instant ownership refresh whenever a native physical or Arena edit, CSV/Excel import, or full recovery is loaded again into the main native collection.
- Regression fixtures for reprint false positives, foil/nonfoil of the same physical printing, unresolved/no-ID physical rows, mismatched name/set/collector metadata, name-based Arena ownership, combined Scryfall filters and immutable SQLite files.

## Changed
- Physical owned requires a matching, valid **Scryfall printing UUID** plus agreement with supplied card name/set/collector data. A different edition sharing the same name is **not** automatically marked physically owned.
- Arena ownership remains **card-name based** in the native schema. The On Arena metadata filter is independent and can be combined with Arena name-owned status.
- The label **Physical not verified owned** is intentional: unresolved physical holdings may exist for that name, but the app will not guess which specific printing they represent.
- The existing 200-result cap applies **after** ownership matching, so rare owned printings are not silently excluded by the first 200 unowned rows.

## Fixed
- Previous Card Catalog filtering did not account for the native collection at all and could not distinguish owned and unowned printings.
- Unresolved physical imports no longer appear to claim particular sets just because card names match.

## Removed
- Nothing. The separate legacy Windows app, original collection, native backups, Scryfall catalog database, deck history and Android work are unchanged.

## Limitations
- Physical rows without validated UUIDs remain unresolved; filters do not magically determine owned printings from their names. Use existing exact-printing selection/reconciliation workflows before counting them as verified ownership.
- Filtering is read-only and scoped to the currently opened native collection. If no native collection is loaded, ownership filters display an explanation rather than treating all catalog cards as unowned.
- The physical ownership display currently summarizes copies across all finishes of one confirmed printing. Detailed preferred artwork/variant selection, TCGPlayer valuation and pricing sorting remain tracked at parity blocker #62.
- Scanning large catalogs to find uncommon owned results requires performance validation on a real Windows machine; other missing WebView modules and full migration/recovery acceptance also remain open in #62.
