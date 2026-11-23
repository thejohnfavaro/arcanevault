# Arcane Vault Windows v0.9.218 (Preview)

## Added
- Source regression checks ensuring neither saved-deck nor EDH Lab preview accesses WPF controls or mutable printing selections from a background task.
- A layout regression check requiring observed opponent colors to share a responsive row with the format and explicitly tagged opponent-color tables.

## Changed
- Deck editor preview captures the commander name, requested card identity and selected printing on the UI dispatcher thread before running any SQLite/catalog work in Task.Run.
- Deck Analytics groups the format breakdown, explicitly tagged opponent color identity, and partial observed opponent-color statistics in one left-aligned wrapping row; tables wrap together on narrower screens.
- The preview now reports unexpected lookup errors instead of leaving "Loading local card details…" indefinitely.

## Fixed
- Deck editor artwork and card rules could remain on a permanent placeholder because the background task read CommanderBox.Text from a non-UI thread. This raised an unhandled WPF cross-thread exception. Both saved-deck and EDH Lab editors now use captured values.
- The observed opponent-color table was inserted as a separate 500px panel after the first analytics row and appeared awkwardly detached or centered. It now occupies a consistent third column with the adjacent breakdowns.

## Removed
- No collection records, user-selected printings, saved deck quantities, match history or confirmed/observed opponent-color data are changed.

## Verification
- Run Windows native workflow and its native-visual-shell regression checks.
- Preview Dawnsire, Sunstar Dreadnought (EOE) 238 and Island (FIN) 297 from the saved Borg deck. Artwork or a clear fallback message and catalog details should appear without getting stuck on "Loading local card details…".
- Verify format, tagged opponent colors, and partial observed opponent colors align across wide windows and wrap predictably at narrower widths.
- Build and installed behavior must be checked; source changes alone do not establish successful device acceptance.
