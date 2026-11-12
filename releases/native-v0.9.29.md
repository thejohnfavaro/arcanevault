# Arcane Vault Native Windows v0.9.29 — Black Mana Palette Refinement (Preview)

This release improves the native Magic color-identity themes based on feedback that **Black mana looked purple rather than black**. Builds on v0.9.28 and retains the verified v0.9.27 updater and startup protections.

## Added

- A clearer Black (B) theme description in Options: obsidian, charcoal and ash-silver rather than violet.
- Regression coverage for all 16 color identities containing black mana, including mono-black and two-color pairings.

## Changed

- Mono-black uses deep neutral backgrounds and ash-silver accents instead of a bright violet accent.
- Black mana's theme swatch and multicolor gradient stop now use neutral graphite `#79838C` instead of violet `#A66FD4`.
- Other black-containing themes have charcoal foundations and distinct nonblack accents, including Orzhov ivory, Dimir blue, Rakdos red and Golgari green.
- Saved theme IDs, application preference files and live palette switching continue to work without migration.

## Fixed

- Black (B) previously appeared primarily purple instead of black.
- Multicolor themes containing black previously inherited inappropriate purple accents that obscured their named identity.
- Theme swatches now better communicate the actual black-mana palette.

## Removed

- No feature or data was removed.
- No changes to collection ownership, printings, catalog, deck revisions, data imports, backups, or updater behavior.

## Manual verification

1. Update from your working v0.9.28 using Native Updates or install this version's checksummed ZIP.
2. Open Options → Appearance & Themes and preview Black (B): confirm an obsidian/charcoal background and ash-silver accent, with no prominent purple.
3. Preview Orzhov (WB), Dimir (UB), Rakdos (BR), and Golgari (BG): confirm the black foundation remains neutral and the second mana color is recognizable.
4. Save a chosen theme, restart, and verify it persists. Cancel a different preview and ensure the saved theme returns.
5. Check catalog navigation, labels, cards and deck library for acceptable contrast.
6. Confirm physical and Arena collections and saved decks remain unchanged.

Automated native .NET 10 tests and Windows build passed for PR #110 before merge. Final versioned release checks and real Windows visual acceptance are still required.
