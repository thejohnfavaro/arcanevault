# AVN-033 — Native saved-deck preflight (read-only)

## Purpose
Before a user saves a native deck, the Windows deck editor now examines the **proposed** mainboard, separate commander, selected format, native physical/Arena inventory and—when available—the independent local Scryfall catalog. Every finding is advisory. The user explicitly chooses **Save anyway** or **Cancel**, and a successful save still goes through the existing exact-SHA verification and full SQLite rollback path.

### Confirmed checks
- **Commander:** 100 cards including the separate commander, singleton nonbasic rule (respecting catalog card-text exceptions), commander/card color-identity subsets, cached Scryfall `commander` not-legal/banned statuses, and **physical** owned quantities.
- **Arena Brawl:** 100 cards including the separate commander, singleton/color identity, presence of an Arena printing in the local Scryfall snapshot, and **Arena** owned quantities.
- **60-card:** minimum 60 cards, four-of nonbasic rule (respecting text exceptions), and physical/Arena shortages **shown separately**, never pooled across platforms.
- **Other/custom:** no invented deck-size or legality rules; metadata and quantities are informative only.
- For all formats, missing card records and unreadable/absent catalog data are marked **unverified**, not falsely legal or illegal. All name lookups use SQLite parameters and read-only connections.

### Deliberate limitations
- **Brawl legality is not guaranteed**: official Arena Brawl, Competitive Brawl, Standard Brawl and commander-specific ban lists can change independently. A Scryfall `brawl` status is *not* treated as Arena Brawl legality. The preflight explicitly calls out this limitation.
- Scryfall color identity, Oracle text and Commander legality are only as recent as the **last local catalog sync**. Supplemental commanders (partner/background/other rules), complex deckbuilding text, format-restricted cards, some alternative naming rules and incomplete imports still need human review. A singleton warning is advisory, never blocks a user's deck.
- The native editor stores **mainboard + one optional commander**; sideboards, partners and companions aren't modeled here. A deck can be saved even with unknown/unowned cards.
- No changes to ownership, catalog, legacy installed profile, Android, or the EDH Lab backlog. Validation never silently adds owned cards.

### CI
Synthetic fixture includes a Golgari leader, an off-color spell, a banned Commander card, an Arena-unavailable printing, 99+1 counts, basic lands, unlimited Relentless Rats, Seven Dwarves' seven-copy exception, missing catalog, SQL-injection-like card name and SHA check confirming the SQLite catalog remained byte-identical. Existing native persistence and shipping Windows CI remain in force.

### 1.0 acceptance
Test a real Commander and Arena Brawl saved deck (especially known cards with ambiguous or rebalanced Arena availability) against the current catalog, confirm warnings are understandable, and verify both **Cancel** and **Save anyway** preserve ownership and recovery guarantees. This preflight does not substitute for a live Arena import/queue validation.
