# Arcane Vault Native Windows v0.9.159 (Preview)

## Added
- Gameplay Telemetry Coverage section in Match Analytics showing the number of matches with explicitly linked GRE observations, total archived events, and the latest 20 matches with gameplay telemetry.
- Read-only indicators of observed game numbers and highest observed turn, carefully labeled as partial observations rather than complete match metrics.
- Optional bounded ingestion of mulligan count, hand count, cards drawn this turn, and land plays remaining when supplied by MTG Arena detailed logs.
- Regression checks for allowlisted gameplay counters, event deduplication and telemetry coverage aggregation.

## Changed
- Match Analytics now reads gameplay telemetry coverage from the existing local match-history SQLite database without creating an independent ledger or altering match results.

## Fixed
- Native release version advanced to prevent overwriting the previously published v0.9.158 release.

## Removed
- Nothing. Existing match results, deck attribution, physical inventory, proxy records, rank tracking, and historical records remain unchanged.

### Limitations
Telemetry is opt-in and depends on Arena's Detailed Logs. Unknown data and missing turns are never inferred. This release reports coverage and observed fields, not validated play/draw win rates or mulligan performance; future changes can build on the stored observations.