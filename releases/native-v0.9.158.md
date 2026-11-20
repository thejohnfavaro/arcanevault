# Arcane Vault Native Windows v0.9.158 (Preview)

## Added
- Opt-in Arena detailed-game telemetry archival through the existing local Player.log tracker. While a match is explicitly active, Arcane Vault extracts bounded gameplay evidence from batched GRE game-state messages.
- A separate, versioned `arena_match_telemetry` SQLite table in Arcane Vault's local match-history database, keyed by explicit Arena match ID, with capture time, game/turn numbers, event type, and normalized JSON.
- An allowlisted, privacy-minimized capture of available turn/phase details, seat-relative life and poison totals, zone types and object IDs, Arena card group IDs, game-state transitions, affected object IDs, and mechanical action annotations. These are observations, not inferred player decisions or a finished replay.
- Idempotent GRE ingestion across repeated scans and rotated logs, with row limits, event-size limits, and exclusion of account identifiers, chat, credentials and arbitrary log data.
- Regression coverage for batched GRE messages, duplicate events, match boundaries, turn numbers and privacy filtering.

## Changed
- Existing Arena match detection also recognizes GRE envelopes where detailed logging is enabled. Game-state observations are written locally only while an Arena match room has been explicitly identified as active.
- The match tracker exposes a local `archivedTelemetryEvents` count in its diagnostic snapshot, without changing the UI, widgets or exported Stream Deck payloads.

## Fixed
- Optional telemetry database errors do not interrupt match-result detection. Existing win/loss and deck-assignment logic remains authoritative and independent.

## Removed
- Nothing. Native inventory, proxy ledger, saved deck text, collection value, match outcomes, and rank tracking remain unchanged.

This is an early data-acquisition foundation, **not** a complete play-by-play replay or verified per-card performance analytics. GRE data availability and shape depend on Arena's Detailed Logs and can vary by game mode/version. Hidden information is not inferred, and historical games cannot be filled in when their detailed logs have rotated. Captured telemetry stays in local SQLite and is not exposed to Xeneon/iCUE widgets in this version.
