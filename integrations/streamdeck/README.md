# Arcane Vault Stream Deck Companion

Version 0.9.2.0 derived from the recovered, hardware-tested 0.9.1.0 from the installed, working Companion on November 17, 2026. This is the plugin source, artwork and Property Inspector, not only an exported installer.

## Data contract

The Windows app owns Arena matching, results, manual corrections and persistent Arena→native deck IDs. The Companion reads `%LOCALAPPDATA%\ArcaneVault\streamdeck.json` and does **not** edit match results. The EDGE widget consumes the same snapshot. Refresh requests go to `%LOCALAPPDATA%\ArcaneVault\streamdeck-refresh.request`.

The Deck W/L action persists the selected **native deck ID** through Stream Deck settings and `streamdeck-selections.json`, and retains a last-valid snapshot across transient file read failures. The key renders `wins`, `losses`, `keyImagePath` from the corresponding saved-deck record. Active deck IDs are not guessed by name when a stable ID is present.

## Test and package

Run `node integrations/streamdeck/test-streamdeck.js` and `node --check integrations/streamdeck/com.rngesus.arcanevault.sdPlugin/bin/plugin.js`.

The GitHub Actions workflow `streamdeck-companion.yml` packages this full folder into `ArcaneVault-Companion-v0.9.2.streamDeckPlugin`. Import the package into Stream Deck; the official host performs installation. Do not mistake a packaged file or test runner for real-hardware verification.
