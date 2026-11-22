# Arcane Vault Windows v0.9.211 (Preview)

## Added
- Regression assertion that the unified Deck Analytics selector supplies the same verified match-ID scope to telemetry coverage, observed gameplay, and play/draw statistics.

## Changed
- Updated a stale source-inspection regression expectation from the former `ReadTelemetryOverviewForDeck(scope)` and related calls to the v0.9.210 read-only `ReadTelemetryOverviewForDeck(null, selectedMatchIds)` API pattern.

## Fixed
- Windows v0.9.210 GitHub Actions run 45060057108 compiled the application but failed the `native-visual-shell` test because that assertion still required the obsolete call pattern. The assertion now matches the new safe historical-match reconciliation.

## Removed
- Obsolete regression expectations for the old native-ID-only scoping mechanism.

## Verification
- Confirmed failure from the exact GitHub Actions job log for run 45060057108: `Native visual shell: Match Analytics must use one shared deck selector for all reports.`
- Native workspace, Arena outcomes, opponent annotations, saved decks, and W/L history are unmodified by this change.
- Windows CI and installer status for v0.9.211 must pass before installation; no passing result is claimed yet.
