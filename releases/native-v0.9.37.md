# Arcane Vault Native Windows v0.9.37 — Spell-Pip Mana Base Guidance (Preview)

Extends the v0.9.36 Arena-style land planner with the actual color-symbol
requirements of spells already in your EDH Lab deck. All planning is local,
transparent and still affects only the **unsaved** deck draft.

## Added
- **Weight basics by spell mana symbols** option in EDH Lab's land-balancing
  panel. Enabled by default for new drafts; it only takes effect when a
  refreshed catalog contains mana costs and the deck has colored symbols.
- Basic-land mix uses the actual cached Scryfall `mana_cost` of each
  nonland spell, multiplied by deck quantity and smoothed to avoid starving
  colors that currently have few/no spells. White-heavy spells shift toward
  Plains, blue-heavy spells shift toward Islands, and so on.
- Visible per-color demand summary, costed/uncosted spell copy counts and a
  clear statement of whether the live plan is pip-weighted or even-split.
- Hybrid costs contribute fractional demand to both possible colors;
  generic, X and colorless pips do not claim a colored source.
- Native Scryfall bulk catalog now persists `mana_cost` alongside mana
  value. First-face fallback is used for double-faced card layouts; split
  costs are treated as alternatives, **not** combined mana requirements.
- Legacy compatibility: an old native catalog without `mana_cost` remains
  readable and continues the even-split algorithm. The UI explains that a
  **Full Sync** is needed before pip-aware planning is available.
- **Full Sync** automatically rebuilds an old-schema catalog even if the
  Scryfall manifest timestamp is unchanged. The existing transactional
  staging, verification and rollback copy remain mandatory.

## Changed
- Pip weighting is optional; uncheck it to restore v0.9.36 even-color land
  balancing. The target remains adjustable (20–60).
- As spells are added or removed, automatic balancing can redistribute only
  ordinary basics in the unsaved list; existing snow lands, nonbasic duals,
  utility lands and other entries are retained.
- The recommended land total still honors the **99-card mainboard** cap.
- Saved decks continue with Auto-balance OFF until explicitly enabled; the
  new weighting preference does not independently modify any existing deck.

## Fixed
- Changing a draft's colored mana demand now changes the suggested land
  split instead of always staying even across commander colors.
- Same-timestamp older catalogs no longer quietly skip the new mana-cost
  metadata during Full Sync.

## Removed
- Nothing. No ownership, printing, card inventory, Deck Library, existing
  backup, or in-app native updater features removed or migrated.

## Important limits
- This is an approximation, not the proprietary MTG Arena algorithm or a
  full casting-success simulation. The cost model deliberately gives hybrid
  and alternative payment pips fractional demand.
- Some MDFCs, split cards, alternate costs, phyrexian options and X spells
  have multiple casting choices; the first castable/front face is treated
  as the default rather than counting multiple spells at once.
- The plan does not yet model fetchland targets, timed land drops, conditional
  mana, modal face selection, commander recasts or exact per-turn probability.
  Source availability from a land that can make several colors is still
  fractional rather than a guarantee of simultaneous access.
- If a nonland spell has no cached mana cost, its pips are omitted and the
  coverage display reports the missing copies.
- Missing/unrecognized or ineligible card names still block automatic
  changes, and normal protected native deck preflight is unchanged.
- Local legality/printings aren't a live banlist oracle.

## Local acceptance
1. Update via **Native Updates** from v0.9.36 to v0.9.37. Existing decks
   and collection should be unchanged on first launch.
2. Open **Full Sync** once to refresh the independent Scryfall catalog
   with `mana_cost`. The updater alone doesn't rebuild your catalog.
3. Start a new two-color commander draft: with no nonland spells the
   36-land suggestion remains **18/18**.
4. Add several blue spells but few white spells. The land suggestion should
   shift toward Islands, showing the actual counted colored pips.
5. Add a dual land: it still consumes **one** land slot while contributing
   to both colors. Utility/snow lands remain unchanged.
6. Uncheck **Weight basics by spell mana symbols**; confirm it goes back
   to even-color distribution.
7. Reopen a saved EDH deck: it remains untouched until you manually enable
   Auto-balance or click Balance now.
8. Check malformed lines, unrecognized cards, hybrid costs and empty decks:
   no silent deletes; no invented ownership. Confirm existing backup/revision
   safety on explicit save.
9. Repeat on an Arena Brawl deck: eligibility and ownership must remain
   separate from physical paper collection.

Release requires Windows build, native WPF regression checks, checksummed
portable bundle and matching `native-v0.9.37` tag. Local device acceptance
remains necessary before the 1.0 milestone.
