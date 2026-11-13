# Arcane Vault Native Windows v0.9.59 — Visual Depth Refresh, Phase 1

## Added

- **Theme-aware atmosphere:** layered gradient fills for the Card Catalog hero, sidebar, elevated surfaces and card tiles, plus accent edges and navigation hover washes.
- Restrained panel elevation/shadow on the **large, static** dashboard surfaces. Card-art tiles receive extra lift only while hovered; scrollable catalog rows remain virtualized with no permanent per-card drop shadows.
- New shared WPF styles for elevated panels and richer inactive navigation, with a clearer active section and visible keyboard focus indication.
- A visual-depth regression check verifying that all new gradients are dynamically updated for the currently selected theme, and that card rows do not carry unconditional expensive effects.

## Changed

- **Card Catalog:** deeper hero, stronger type hierarchy, lifted filters and result surface, more dimensional card-art tiles, and theme-aware left navigation.
- **Collection & Deck Library:** elevated inventory/verification summaries, improved search surface and list-row hover/selected feedback, avoiding hardcoded blue-only physical-row backgrounds.
- **EDH Lab:** accented hero header and more legible, theme-sensitive listing border.
- **Deck Studio:** theme-sensitive window, header, deck input surfaces, mainboard, and EDH workspace tabs instead of a single hardcoded purple palette.
- **Appearance & Themes:** live theme preview now uses the actual layered hero/tile surfaces present in the app.
- Selected rows and tabs use the theme's readable foreground instead of fixed pale lavender; notably, Black stays charcoal/silver and the light theme retains legibility.

## Fixed

- Hard-coded purple selection text could clash with non-purple or light themes.
- The Card Catalog, Collection dashboards and EDH Lab lacked visual separation between search, results, and navigation.

## Removed

- No collection, deck, catalog, shortlist, update, or recovery features were removed. No framework rewrite, new remote artwork requests, or animation dependencies were added.

## Verify on your Windows device

1. Update to native v0.9.59 and check **Arcane Noir**, **Black (B)**, **Emerald Grove** and **Daylight Archive** in Appearance & Themes; preview gradients should match the actual Catalog/Collection surfaces, without reverting to purple when black is chosen.
2. Confirm visible elevation of the Card Catalog hero, filter panel, results and left sidebar, without distracting artwork treatment.
3. Browse multiple Card Catalog pages in **Card art**, **Detailed**, and **Compact** views. Hover a tile and check the outline/lift, then verify scrolling and pagination stay responsive.
4. Test keyboard focus, tab selection and sidebar navigation between Catalog, Collection, Deck Library and Set Progress.
5. Open Collection & Deck Library, EDH Lab and Deck Studio. Confirm typography and inputs remain readable at 100%, 125%, 150% and 200% scaling; no confirmation controls disappear.
6. Ensure ownership quantities, favorites, decks, full backup and recovery behavior are unchanged.

**Scope:** Native Windows preview, phase 1 of the visual-depth work. The next aesthetic iteration may address remaining legacy hardcoded deck-editor surfaces and richer card/collection presentations, after device feedback. CI is an automated regression check, not a screenshot or a manual accessibility audit.
