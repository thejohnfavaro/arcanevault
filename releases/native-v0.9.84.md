# Arcane Vault Native Windows v0.9.84 — Reviewable mana-base proposals

## Added
- In EDH Lab → Mana & Roles → Lands I own / Proxy, **Build proposed mana base** creates a read-only land plan against the current commander identity, legal local catalog, unsaved draft, and chosen target of 20–60 lands.
- A scrollable diff shows exactly which card names and quantities would be added or removed, with land archetype, entry condition, and real-owned/Proxy explanation.
- **Review and apply proposed changes** displays a separate in-app Yes/No confirmation for the entire diff before touching the unsaved draft.
- Proposals use the current gap-aware land advisor to find legal nonbasic lands first, then available basics, without inventing real ownership or reusing Proxy copies.

## Changed
- Mana-base construction remains separate from the previous automatic basic-land balancing; explicit approved diffs disable auto-balancing so the new changes are not immediately rewritten.
- Editing the mainboard, commander color identity, land target, collection pool, or catalog after generating a plan requires regenerating it before any apply.

## Fixed
- The optimizer will not silently remove nonbasic lands or nonland spells to meet a desired target.
- Missing verified local land inventory, malformed draft rows, overfull mainboards, or a 99-card capacity conflict block an unsafe bulk operation rather than making partial changes.
- Paper proxies never appear in Arena recommendations or masquerade as real owned cards.

## Removed
- Blind, unchecked bulk land edits: proposed changes require a human-readable diff and an explicit application confirmation.

## Safety
- Proposal generation is read-only, offline, and uses the locally legal commander/format catalog.
- Applying a plan changes only the in-memory, unsaved mainboard text, with count verification. Native collection, proxies, prices, ownership, set progress and saved deck history remain untouched until a separate Review and save deck action.
- Initial version conservatively removes only surplus verified basic lands; user-chosen nonbasic lands and spells always require manual cuts. Land-type and mana access classifications are heuristic, not a substitute for printed Oracle restrictions.
