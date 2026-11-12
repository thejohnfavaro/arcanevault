# Arcane Vault Native Windows v0.9.28 — Theme Identity Visuals (Preview)

This preview addresses theme names not visually matching the palette shown in the Windows interface, building on the locally verified v0.9.27 startup crash hotfix.

## Added
- Actual theme accent swatches and plain-language palette descriptions in Options → Appearance & Themes.
- Original mana color-stop gradients for all 32 Magic color-identity themes. Multicolor names such as Azorius (WU), Rakdos (BR) and Five Color (WUBRG) now visibly show each constituent mana color.
- Live themed accent strips in the main Catalog and sidebar that change immediately when a theme is previewed.

## Changed
- The Catalog's fixed lavender/purple banner background is replaced by a theme-aware panel, title accent and readable adaptive text.
- Light-theme navigation and Catalog hero text now derive their foreground from the selected theme.
- A dedicated WPF `VaultThemeBanner` gradient resource is safely replaced rather than mutated, consistent with the v0.9.27 frozen-brush hotfix.
- Existing theme IDs and saved appearance preferences stay compatible; no re-selection or migration should be necessary.

## Tests
- Windows WPF build and .NET 10 native regression tests for all 37 palettes (five classic themes plus 32 color identities), precise mana-color ordering, dynamic UI accents and prior frozen-brush startup crash guard.

## Not changed
- No changes to native collection, printings, card artwork ownership, saved decks, deck revisions, backups, Scryfall database, or updater protocol.
- No new EDH Lab or Android features.

## Manual verification
1. Install the checksum-validated native v0.9.28 preview from v0.9.27 and verify direct startup works.
2. Open Options → Appearance & Themes and check that swatches match the actual UI for Arcane Noir, Daylight Archive, Emerald Grove, Ember Forge and Deep Ocean.
3. Compare named identity combinations: Azorius should show white/blue, Rakdos black/red, Golgari black/green, and Five Color all five colors in the swatch/gradient.
4. Preview then Cancel; confirm previous saved theme returns. Save and restart; confirm the chosen theme persists.
5. Check contrast of Catalog header, sidebar navigation, card text and panel headings, especially Daylight Archive.
6. Report any named themes that still feel visually misleading. This is a preview requiring local visual acceptance, not a v1.0 certification.
