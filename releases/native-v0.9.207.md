# Arcane Vault Windows v0.9.207 (Preview)

## Added
- Two-column summary layout for gameplay telemetry and verified play/draw on wide screens.
- Side-by-side format/own-color and opponent-color/weekly performance tables that wrap at narrower window widths.
- Visual shell regression coverage for the unified scroll container and new paired analytics panels.

## Changed
- Deck Analytics uses one page-wide vertical scroll instead of a non-scrolling fixed analytics header that squeezed the performance dashboard into a small remaining viewport.
- Readable 13pt telemetry and gameplay summary text, a compact left-aligned Deck Scope selector, and clearly labeled recent observations.
- Existing exact-native-ID deck filtering and All Decks aggregate mode from v0.9.206 remain intact.

## Fixed
- Wide empty regions alongside sparse analytics tables and excessively small cramped summary paragraphs on large displays.
- The redundant nested scrolling between the fixed page header and the performance dashboard.

## Removed
- No analytics, match records, historical tags, collection records or saved deck definitions are removed.

## Verification
- Run native Windows CI and the `native-visual-shell`, `arena-log-assignment`, and `match-performance` regression suites.
- Confirm that selecting a saved deck filters performance, color identity, weekly totals, GRE coverage, mulligan and play/draw metrics, and that All Decks shows the aggregate.
- Confirm side-by-side tables at 1440px-plus widths and wrapping on narrower window sizes.
- **Build status**: the user reported v0.9.206 failed; its failed GitHub Actions job log has not been retrieved through the available connection, so the exact CI failure and successful compilation of v0.9.207 remain unverified. Inspect the first compiler/test error from the failed run if this attempt also fails.
