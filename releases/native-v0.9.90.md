# Arcane Vault Native Windows v0.9.90 — Responsive embedded EDH Lab

## Added
- **Adaptive EDH layout** in the main application workspace: when the editor is narrow, the same live card inspector docks into a **Preview** tab beside Cards, Mana & Roles and Improve. When there is enough room it returns to the existing right-hand preview pane.
- Viewport-aware layout logic with a hysteresis band around the compact/wide breakpoint to avoid repeated reflows during resize and splitter drag.
- Regression checks asserting one card inspector, shared preview state across layout transitions, correct splitter visibility and responsive WPF event handlers.

## Changed
- Three-column minimum widths are reduced to fit the width available inside the main application sidebar at common small window sizes. The preview pane collapses entirely in compact mode so the mainboard and deck tools retain usable width.
- The introductory description is hidden in compact view, prioritizing the current deck and card controls; it returns in wide view.
- Expanded analytics is capped relative to the actual editor height, keeping the mainboard and tools accessible in shorter windows while the existing charts remain scrollable.

## Fixed
- The embedded EDH Lab no longer forces a third 220px preview panel offscreen when the main window becomes narrow.
- The live preview is moved, not rebuilt: card selection, mana symbols, Oracle text and already-loaded artwork are retained during docking.
- The editor's expandable analytics no longer claims a fixed 310px at short heights.

## Removed
- The assumption that the embedded editor always has enough width for three minimum-sized columns.

## Safety
- This is strictly a presentation change. Current draft, owned and Proxy quantities, deck validation, save preflight, card suggestions and local catalog data are unchanged.
- The preview is deliberately still read-only; applying card and mana-base changes uses the existing explicit actions.
