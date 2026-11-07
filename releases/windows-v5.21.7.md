# Arcane Vault Windows v5.21.7 — UI/UX Refresh

## Added
- Workspace jump bar for Card Browser, Physical Collection, Set Progress, Deck Studio and Data & Sync.
- Card details arranged into Overview, Printings, Legality and Combos tabs while retaining existing tools and actions.
- Data & Sync overview showing owned card names, metadata coverage, cached printing records and physical printing count, with shortcuts to existing maintenance workflows.
- Physical inventory sorts by total estimated value or per-copy price (ascending/descending), using existing exact-printing price quotes.
- Visual Set Progress tiles with prominent percentage and readable progress bars.

## Changed
- Unified focus rings, spacing, rounded panels, accessible status messages and responsive card-detail layout.
- Clearer progress and empty state text, with unknown prices always last in either price sort direction.
- Sort preference persists; catalog synchronization is not triggered simply by choosing a price sort.

## Fixed
- Better fallback labels for unpriced physical printings and offline catalog statuses.

## Removed
- No working Windows features removed.

## Data safety
- No inventory, selected printing, match record or deck storage format changes.
- No Windows features removed. Pricing is an estimate from previously fetched Scryfall printing quotes.
