# Arcane Vault Windows v0.9.103 — Cross-platform Card Catalog & EDH preview

## Added
- Combined **mana value / CMC min and max** filtering; leave one bound empty for one-sided results, use equal values for an exact mana value. Works alongside rarity, utilization tags, set, type, color identity, proxies, and ownership.
- Read-only bounded three-page card query cache and background prefetch of all artwork for each matching 200-row page through the existing limited official Scryfall cache.
- **Next / Previous** catalog pages return to the top; inspecting a card still preserves the current results.
- Bottom-right, shortlist-style **flip card face** controls on double-faced Card Art tiles, persistent inspector, and full Card Details.
- Matching double-faced card artwork flip on the **EDH Lab commander browser's existing right-side card preview**. The commander preview retains name, exact mana cost, color identity and Oracle text.

## Changed
- Face flipping is an independent interaction, not a shortlist or card-selection action.
- Only genuine front/reverse URLs from the same Scryfall printing are used; no reverse face is guessed by name.
- Catalog page cache is invalidated on native workspace refresh. All browsing remains read-only and preserves existing printing/deck data.

## Fixed
- Repeated page queries and fetching artwork only after scrolling into a row are reduced by bounded page prefetching.
- Missing face metadata hides the flip control instead of showing a dead button.

## Removed
- No collection, deck, CSV import, backup, or sync features removed.

## Safety
- Cached artwork remains restricted to official HTTPS Scryfall hosts and existing bounded disk space.
- Edition-level ownership is never inferred from a card name, and these changes do not alter persistent collection records.
- Paired Android v2.1.15 is required for cross-platform parity. Automated Windows native/compatibility checks passed on the feature branch; visual review on users' devices is still required.
