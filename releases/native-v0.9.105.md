# Arcane Vault Windows v0.9.105 — Integrated Scrollbars

## Added
- Theme-aware, compact rounded vertical and horizontal scrollbars across all Windows workspaces, including Card Catalog, EDH Lab, My Shortlist, Set Progress, and Data & Sync.
- Distinct hover and drag feedback using the active Arcane Vault theme accent.

## Changed
- Scrollbar tracks and thumbs now match the existing panel, border, and accent colors instead of displaying generic Windows chrome.
- Native WPF track commands retain page clicks, wheel scrolling, keyboard navigation, and mouse/thumb dragging. Track width remains accessible.

## Fixed
- Visual inconsistency between scrollbars and the rest of Arcane Vault's themed UI.

## Removed
- Default-looking scrollbar chrome in scrollable Windows workspaces; no navigation or scrolling features removed.

## Safety
- Presentation-only change. No impact on collection ownership, saved decks, printings, proxies, backups, sync baselines or update recovery.
- Windows build and theme regression checks passed in PR #216. Visual review in the installed application is recommended.
