# Arcane Vault Windows v0.9.205 (Preview)

## Added
- Regression coverage for explicit opponent-color annotations when duplicate Arena match IDs exist in native and legacy local history stores.

## Changed
- Match History and Deck Analytics now reconcile opponent-color annotations across duplicate native/legacy match records. The native record remains authoritative for match outcome, deck identity, date, format and W/L tracking; a secondary explicit color annotation is used only if the primary record's annotation is blank.
- Unknown opponent colors remain explicitly unknown. Arena telemetry and the player's own deck identity do not establish an opponent's color identity.

## Fixed
- Existing explicitly saved opponent-color annotations can no longer disappear from the analytics display solely because a duplicate native-history record (read first) lacks the annotation while the legacy-history record retains it.

## Removed
- No match records, annotations, or historical data are deleted or rewritten.

## Verification
- Run the Windows `match-performance` and `native-visual-shell` regression groups.
- Confirm a synthetic duplicate match ID with native blank opponent colors and legacy `WU` displays `WU` while retaining the native W/L outcome and deck attribution; native nonblank colors take priority.
- Compare the Against Opponent Color Identity view before and after installing. If the historical files have no explicit opponent-color annotations, rows correctly remain Unknown.
- Build/package CI and real-user-database validation have not yet been independently confirmed.
