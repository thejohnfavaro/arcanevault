# Arcane Vault Native Windows v0.9.160 (Preview)

## Added
- Read-only Observed Gameplay metrics in Match Analytics, including average of each game's highest observed turn and seat-anonymous mulligan observations.
- Regression tests confirming turn observations and mulligan snapshots are deduplicated correctly.

## Changed
- Mulligan reporting aggregates the maximum observed mulligan count per game and seat, rather than double-counting repeated game-state snapshots.
- Analytics explicitly marks observed turn values as lower bounds, not verified finished game lengths. Mulligan aggregation scans a bounded window of the latest 10,000 telemetry messages to avoid unbounded UI reads.

## Fixed
- Gameplay metrics ignore missing game identifiers, malformed historical telemetry, and absent mulligan fields instead of treating them as zero.

## Removed
- Nothing. Existing match results, deck assignments, collection records, proxy ledger, rank tracking and historical analytics remain unchanged.

### Limitations
MTG Arena Detailed Logs must be enabled for new gameplay evidence. Data varies by event type and game mode. No play/draw win rate is shown until a verified player-to-seat mapping can be established. The currently supported data remains partial observations, not a complete replay.