# Arcane Vault Native Windows v0.9.161 (Preview)

## Added
- Confidence-gated on-play and on-draw win/loss records and win percentages in Match Analytics.
- A separate match-to-local-seat SQLite table that records only explicit seat IDs associated with an exact authenticated Arena user ID in match room data. No account identity, user name or credentials are persisted.
- Regression test for authenticated seat and turn-one active-seat attribution.

## Changed
- Play/draw analytics accepts only matches with one observed numbered game, one verified local seat, and a consistent turn-one activePlayerSeatId. Matches lacking evidence remain excluded from the play/draw rates.

## Fixed
- Contradictory local seat evidence invalidates attribution rather than silently selecting one seat.
- Multi-game matches and missing/ambiguous seat data never enter single-game play/draw aggregates.

## Removed
- Nothing. Existing match history, verified W/L results, deck attribution, collection ownership, proxies, rank statistics and prior telemetry are preserved.

### Limitations
Play/draw reporting is available only when the Arena Detailed Log includes an exact authenticated reserved-player seat and a turn-one GRE activePlayerSeatId. Unsupported game modes or old records will remain unattributed. Game-level outcomes for best-of-three require separate evidence and are not inferred from match results.