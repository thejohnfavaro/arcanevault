# Arcane Vault for Windows v5.21.17

## Added
- Read-only Stream Deck Companion snapshot export at `%LOCALAPPDATA%\ArcaneVault\streamdeck.json` with saved decks, confirmed Arena win/loss records, physical collection value, recent match, active deck, and locally cached deck artwork.
- Export diagnostics at `%LOCALAPPDATA%\ArcaneVault\Diagnostics\streamdeck-export.log`.

## Changed
- Generate the deck selector snapshot before optionally enriching it with match statistics and artwork, so slower artwork requests cannot block basic data availability.
- Incrementally cache artwork and refresh the companion snapshot while Arcane Vault is running.

## Fixed
- Stream Deck tiles could remain at "Awaiting Arcane Vault Snapshot" when the exporter failed before its first output file was written.
- An unreadable or temporarily unavailable match-history database no longer prevents the baseline snapshot being written.

## Removed
- No features removed.

This update does not migrate, delete, or alter ownership, deck, or match-history data. Stream Deck integration is read-only and local to the Windows device.
