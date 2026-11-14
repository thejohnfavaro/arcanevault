# Arcane Vault Native Windows v0.9.88 — Responsive Catalog and Set Progress

## Added
- **Set Progress** is now a dedicated center-panel workspace accessed from its existing sidebar entry. It shows its own title, progress set browser, verified physical/Arena scope and printings checklist, instead of appearing as a fourth Collection tab.
- Responsive Card Catalog filters: a full-width search field, wrapping, consistently labeled filter groups (Set, Rarity, Utilization, Type, Arena scope, Ownership/Proxies), symbol-only WUBRG toggles, and a separate aligned view selector.

## Changed
- Removed the Card Catalog header's fixed 275px right column and inconsistent bottom alignment; controls now wrap as complete groups at smaller window widths.
- The Set Progress sidebar selection reuses the same verified native collection view, cached progress data and event handlers. Navigating between Collection, Deck Library and Set Progress does not perform an extra collection verification solely for switching sections.
- Set Progress hides the Collection inventory summary, search, and destructive clear-inventory control; only its own set filters and progress are shown.

## Fixed
- Header layout no longer leaves Catalog View floating far to the right when the main window is maximized or resized.
- Set Progress is no longer exposed as a redundant Collection tab as well as a sidebar entry.

## Removed
- The visible Set Progress tab from Collection's tab strip (existing index and underlying behavior remain supported internally).

## Safety
- This release changes presentation and navigation only; no changes to physical/Arena ownership, Proxy ledger, pricing, set completion mathematics, import/export, or saved decks.
- Offline printed-card verification and existing Set Progress scope rules remain authoritative.
