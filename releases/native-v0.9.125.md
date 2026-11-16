# Arcane Vault Native Windows v0.9.125

## Added
- Native WPF Stream Deck Companion exporter started automatically after successful application launch.
- Compatible read-only JSON snapshot for the installed Stream Deck Alpha 0.1 plugin at `%LOCALAPPDATA%\ArcaneVault\streamdeck.json`.
- Saved native deck names and IDs, cached official Scryfall commander artwork, collection-value history, and confirmed Arena match statistics.
- Native MTG Arena Player.log match tracker with deduplicated match IDs and verified outcomes, stored independently at `%LOCALAPPDATA%\\ArcaneVaultNext\\match-history.sqlite3`.
- Diagnostic logs at `%LOCALAPPDATA%\ArcaneVault\Diagnostics\streamdeck-export.log`.

## Changed
- The Stream Deck snapshot is written before expensive deck artwork work; artwork is filled in incrementally.
- Native inventory is read using existing verified workspace APIs; no legacy Windows app is launched or required.
- Deck identity and data remain inside `%LOCALAPPDATA%\ArcaneVaultNext`; the original Stream Deck compatibility file is presentation-only.

## Fixed
- Stream Deck buttons displaying 'Awaiting Arcane Vault Snapshot' when running the native Windows application.

## Removed
- No native functionality removed.

The exporter does not edit physical or Arena ownership, saved decks, or match history. The native Arena match tracker reads the local Arena Player.log when detailed logging is enabled and follows an explicitly logged active deck name. It never scans Arena process memory or changes the game installation. Existing legacy match history is only an optional read-only fallback if the native match database cannot be opened or does not exist. Unknown or ambiguous results are excluded from confirmed W/L counts, and the Current Deck tile requires an observed, uniquely matched Arena deck selection. The collection value reflects the latest recorded native physical-value history, not a new price scrape. Collection value reflects the latest recorded native physical-value history, not a new price scrape.
