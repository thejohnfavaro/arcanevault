# Arcane Vault Native Windows v0.9.142 (Preview)

## Added
- Embedded Deck Performance dashboard in Match History, available in an expandable panel.
- Per-deck and all-deck performance with 30-day, 90-day and all-time periods.
- Confirmed match win/loss counts, win rate, last-ten form, current streak and rolling 10-match win-rate chart.
- Match performance by Arena format, including unclassified format reporting.
- Regression tests for duplicate match IDs, unknown outcomes, format splits and date ranges.

## Changed
- Match History reads the recorded Arena format for each match to support analytics.
- Dashboard updates after match-history refresh, manual corrections and verified Arena-ID reconciliation.

## Fixed
- Performance stats exclude unassigned and unknown-result games rather than treating them as losses.
- Duplicate match IDs are counted only once in analytics.

## Removed
- No previous behavior was removed.

> Derived from local recorded match history. No estimated wins or synthesized games. Stream Deck and XENEON EDGE continue using their existing compatible native snapshot format.
