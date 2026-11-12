# Arcane Vault Native Windows v0.9.25 (Preview)

This release restores native appearance themes and improves Card Catalog density, combining merged PRs #104 and #105 on top of locally verified native v0.9.24.

## Added

- A standalone Options window accessible from the collapsible Options sidebar category.
- All five classic Arcane Vault themes: Arcane Noir, Daylight Archive, Emerald Grove, Ember Forge and Deep Ocean.
- All 32 Magic color-identity themes, from Colorless to five-color.
- Immediate theme preview, Save Appearance, Cancel/revert and Restore Default.
- Separate, validated appearance preference storage that never edits ownership, decks or catalogs.
- Regression tests for theme selection, persistence, fallback and Options wiring.

## Changed

- Card Catalog art tiles are 302 px high instead of 363 px, with 132 × 184 px card art instead of 150 × 210 px.
- Responsive Card Catalog rows reserve approximately 225 px per tile and display up to five columns when the window is wide enough.
- Card names, utilization roles, ownership, set/collector numbers, cached artwork and double-click card details remain available.
- Existing bounded image fetches and virtualized scrolling continue to work; no network requests are required to open the Options screen.

## Fixed

- Removed extra empty space around Card Catalog art tiles.
- A malformed appearance setting safely defaults to Arcane Noir instead of blocking startup.
- Theme preferences use separate atomic JSON writes instead of touching native card or deck files.

## Removed

- No native collection, Deck Studio, Set Progress, Deck Library, saved preferences, updater, importer or backup features were removed.
- No inventory quantities, owned-printing identities or deck revisions are modified by themes or compact card tiles.
- No EDH Lab or Android changes are included.

## Manual verification

1. Upgrade from a working v0.9.24 installation using Check Native Updates and verify the checksum-validated side-by-side v0.9.25 installation.
2. Expand Options → Appearance & Themes, preview classic and mono/multicolor palettes, Save, close and reopen to verify persistence.
3. Preview a different palette, click Cancel, and ensure the saved palette returns. Check Restore Default.
4. Verify readable text, dropdowns, button states and focus outlines in bright and dark themes.
5. Open Card Catalog → Card art, resize the application, scroll large search results and open details by double-click.
6. Confirm all ownership labels, utilization tags, set/collector identifiers and library/collection navigation remain correct.
7. Export a full backup and verify no collection or deck data changed as a result of appearance settings.

This is a preview release, not a v1.0 parity certification. Preserve a private full backup before upgrading.
