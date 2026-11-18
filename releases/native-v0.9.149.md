# Arcane Vault Native Windows v0.9.149 (Preview)

## Added
- An ISO-8601 `publishedUtc` timestamp on every Stream Deck / XENEON EDGE compatibility snapshot. Its regularly changing value provides an actual file-content event for new iCUE widgets, even if no match, deck or collection value has changed.

## Changed
- The device snapshot `schemaVersion` and all existing fields remain backward-compatible. A refresh continues approximately every 15 seconds and retains atomic file replacement.

## Fixed
- Newly imported iCUE widget could remain indefinitely on `Waiting for Arcane Vault` because the iCUE File System Provider only guarantees `fileChanged` callbacks after content changes; the old Windows exporter could write exactly identical JSON each tick.
- Pair this Windows build with XENEON EDGE widget v0.7.0, which improves watcher reconnect and diagnostics.

## Removed
- No features or data were removed. Stream Deck, Arena history, collection and deck ownership remain unchanged.

To test, start Arcane Vault, then confirm `%LOCALAPPDATA%\\ArcaneVault\\streamdeck.json` exists and its Windows modified time advances every ~15 seconds. Reinstall the Edge v0.7 widget and accept its existing file-read permission. No data migration, reinstallation of Stream Deck, or installer reinstall is required.
