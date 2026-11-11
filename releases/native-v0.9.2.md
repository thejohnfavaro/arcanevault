# Arcane Vault Native Windows 0.9.2 (Preview)

This is an **unsigned Windows x64 preview**, not a certified 1.0 or replacement for the existing WebView-based Windows app.

## Added
- Native Card Catalog **Detailed list**, **Compact list** and **Cards/Tiles** display modes. Selected cards can still be opened for full details.
- Physical Collection **Detailed** and **Compact** tabular views.
- **Import Excel (.xlsx)** for physical cards. The first visible worksheet can contain Name/Card Name, Quantity/Count/Copies, Set/Set Code, Collector Number, Finish/Foil and Scryfall ID columns.
- Pre-import summary of total and catalog-verified/unresolved copies; explicit confirmation before changes. Missing editions remain unresolved rather than credited to arbitrary sets.
- One verified native SQLite transaction and one recoverable full collection backup per successful workbook import.

## Changed
- Excel imports are strictly **additive**—not replacement or two-way synchronization. Reimporting the same workbook adds quantities again; the confirmation explains this.
- Exact supplied paper Scryfall IDs or unique exact name + set + collector matches may be catalog-verified. Name-only rows remain unresolved. Missing finish stays `unspecified` instead of silently becoming foil/nonfoil.
- Tile view is bounded to the catalog's 200-result search limit. No background card-art fetches on scrolling; full art is loaded only after opening Card Details.

## Fixed
- Regression coverage for Excel parsing, invalid quantities, verified/unresolved printing handling, conservation of Arena/deck/artwork data and stale-SHA rejection.

## Removed
- Nothing. The original Windows program and its updater remain unchanged; Android and EDH Lab feature work remain deferred.

## Limitations
- Excel `.xlsx` supported; legacy binary `.xls`, spreadsheet formulas without cached results, non-tabular sheets and arbitrary proprietary collection exports may require cleanup into a table first.
- Only the first **visible** worksheet is imported. Header fields `Name` and `Quantity` are required. Unresolved records remain visible but do not count toward verified Set Progress.
- This is a side-by-side preview. Validate import and quantity counts on your Windows device with an independent native full backup before treating it as final.
