# Arcane Vault Native Windows v0.9.4 (Preview)

Native Windows x64 CSV collection import release. This is a prerelease, not a certified Windows 1.0 or an automatic replacement for the installed WebView application.

## Added
- **Import CSV / Excel** in Native Collection → Physical Collection. Select a .csv or .xlsx file, review the exact and unresolved printing counts, then explicitly confirm an additive import.
- CSV supports UTF-8 and UTF-8 BOM, embedded commas/newlines in quoted fields, escaped `""` quotation marks, semicolon-separated exports, and common collection-site column names such as `Name`, `Card Name`, `Quantity`, `Count`, `Set`, `Set Code`, `Collector Number`, `Foil` and `Scryfall ID`.
- CSV-specific regression fixtures test BOMs, semicolon exports, quoted names/notes, rejected malformed and fractional quantity rows, verified printing identities and unchanged Arena/deck/artwork data.

## Changed
- Excel and CSV use the same row validation, Scryfall catalog verification, preview, exact owned-printing matching, and native one-transaction recovery path. There is no separate unverified CSV bulk-load shortcut.
- CSV imports are **additive**, never replacements or two-way synchronization. Importing the same export twice adds its quantities twice. Keep an independent full native backup and verify totals before approval.

## Fixed
- Recognize `Normal`, `Regular` and `Standard` finish labels as nonfoil across both CSV and XLSX inputs, when those values are explicitly present in a finish column.
- Reject unclosed/incorrectly quoted CSV fields, invalid quantities, oversized files and conflicting duplicate column mappings before changes.

## Removed
- Nothing. The legacy Windows app, Android feature backlog and EDH Lab work are unchanged.

## Notes
- Required CSV headers: a card name (`Name` / `Card Name` / `Card`) and a positive integer quantity (`Quantity` / `Qty` / `Count` / `Copies` / `Owned`). Optional exact printing columns: `Scryfall ID`, `Set Code`, `Collector Number`, `Finish`/`Foil`.
- When print ID or exact set + collector number is absent, imported cards remain **unresolved**, never automatically credited to a set or a random printing.
- CSV import scans up to 60,000 card lines and requires a file under 35 MB. UTF-8 is preferred; formulas are not interpreted.
- Windows 1.0 real-device data integrity, full migration, import/recovery and performance acceptance remain open in issue #51.
