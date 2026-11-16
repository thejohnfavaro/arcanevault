# Arcane Vault Native Windows v0.9.125

## Added
- Native WPF Stream Deck Companion exporter started automatically after successful application launch.
- Compatible read-only JSON snapshot for the installed Stream Deck Alpha 0.1 plugin at `%LOCALAPPDATA%\ArcaneVault\streamdeck.json`.
- Saved native deck names and IDs, cached official Scryfall commander artwork, collection-value history, and optional confirmed Arena match statistics.
- Diagnostic logs at `%LOCALAPPDATA%\ArcaneVault\Diagnostics\streamdeck-export.log`.

## Changed
- The Stream Deck snapshot is written before expensive deck artwork work; artwork is filled in incrementally.
- Native inventory is read using existing verified workspace APIs; no legacy Windows app is launched or required.
- Deck identity and data remain inside `%LOCALAPPDATA%\ArcaneVaultNext`; the original Stream Deck compatibility file is presentation-only.

## Fixed
- Stream Deck buttons displaying 'Awaiting Arcane Vault Snapshot' when running the native Windows application.

## Removed
- No native functionality removed.

The exporter does not edit physical or Arena ownership, saved decks, or match history. If a native Arena match-history database does not yet exist, it can read the existing old match-history SQLite as an optional read-only source, clearly labeled as such. Without available verified match history, deck statistics remain at zero and the Recent Match tile displays no match. The Current Deck tile cannot reliably identify a live Arena-selected deck until the native Arena tracker is connected. Collection value reflects the latest recorded native physical-value history, not a new price scrape.
