# Arcane Vault Native Windows v0.9.79 — Commander-aware land discovery

## Added
- **EDH Lab → Mana & Roles → “Lands I own / Proxy — commander identity.”** An expandable, scrollable, read-only browser for locally legal Commander/Arena Brawl lands, filtered by the currently selected commander's color identity.
- Source candidates come from the existing **cached verified local EDH card catalog**, not a new Scryfall query or unsaved global search. Real owned/recorded and **paper Proxy** counts are clearly separate. Paper proxies never count as Arena-owned cards, real physical printings, set progress, or collection value.
- Availability filters: **Owned + Proxy** (default), **Real owned only**, **Proxy only (paper)**, and **All locally eligible**. Nonbasic lands shown by default; optional **Show basics**. Live bounded search by land name, type or mana-access label; first 120 matching results after filters with explicit truncation notice.
- Official mana-symbol display in a new **Production** mode, separate from both casting **Cost** and **Color Identity**. Shows colors a land can actually produce via printed mana abilities or basic-land types; it may include **{C}** alongside colored pips. Unclassified/unknown production shows no invented glyph, particularly not {C}.
- **Single-click previews** the exact card through the existing official card inspector; **double-click a real land row** or **Add selected land once** intentionally adds one eligible name to the UNSAVED deck using the normal verified singleton/add safety gate. Already-in-deck lands are not quick-added again; basics use the existing Cards Add (+1) pathway for further copies.
- New offline, immutable **EdhLabLandAdvisor** with production and conditional/fetchland classification, local-color filtering, first-results cap, and weakly cached per-commander mana-analysis so typing/scrolling doesn't rescan all catalog Oracle text.
- Dedicated tests for dual typed lands, Command Tower, Cavern of Souls, conditional Reflecting Pool, fetchlands, {C}, Wastes, ownership/proxy separation, Arena scope, default/nonbasic filters, search, limits, read-only behavior, and actual WPF control/action wiring.

## Changed
- Mana & Roles now lets you find color-fitting nonbasics you already own without manually remembering names. Search is instantaneous against the cached catalog once loaded.
- Source labels are intentionally conservative: e.g. a fetchland **fetches another land** and does not directly produce its target's colors; Reflecting Pool may produce mana only if other lands can; rainbow mana and limited-spend abilities are labeled conditional.
- The new production icon mode reuses the exact official Scryfall SVG cache and never treats color identity, source access, and casting cost as equivalent.
- Existing automatic basic-land balance behavior, weight-by-pips, saved deck review, owner/Proxy counts and card-printing valuations are unaffected.

## Fixed
- EDH Lab previously helped distribute basic lands but offered no explicit discovery of multicolor or utility lands recorded in the collection for a commander's identity. It now surfaces names, quantity/proxy status, recognized actual mana sources, and restrictions without adding on a mere selection.
- Colorless mana production is no longer conflated with no detected source: `{C}` means verified colorless production; a fetchland or uncertain conditional source displays no production glyph rather than an incorrect colorless icon.

## Removed
- No inventory copies, Proxy rows, chosen printings, collection valuation records, saved deck entries, recovery points, catalog data, or Scryfall images were removed.
- No default automatic nonbasic swaps, speculative purchase recommendations, network calls per row, direct ownership edits or inferred fetchland colors were introduced.

## Windows acceptance
1. Update to 0.9.79. Open EDH Lab with a two- or three-color **paper Commander** and synced local catalog. Expand **Mana & Roles → Lands I own / Proxy**. Verify the shown commander identity pips are official symbols.
2. Find a recorded dual such as a Plains/Island land: its **mana production** shows both appropriate icons. Confirm real owned quantity is distinct from Proxy quantity and in-deck count.
3. Test **Owned + Proxy**, **Owned only**, **Proxy only** and **All eligible**. Verify a proxy-only legal land is visible without adding physical ownership or value. Test Brawl: **paper proxies must not appear** as Arena availability.
4. Inspect a land with “add any color”; confirm only the commander's colors are visually suggested and any restricted spending is called out. Inspect a fetchland: it must **not** pretend to directly tap for a chosen color. Reflecting Pool should show conditional mana access, not invented colored icons.
5. Toggle **Show basics**: normal basics should be hidden by default, while Wastes is available for colorless commanders. Search a partial mixed-case name and clear the filter.
6. **Single-click** land rows. Confirm only the official preview updates. **Double-click** a real eligible row or use **Add selected land once**. Confirm exactly one copy goes into the unsaved mainboard; adding a second singleton is blocked. Check the Text tab, Mana plan, and Save preflight remain authoritative.
7. Rapidly filter/scroll 100+ lands; no UI freeze, per-row SQLite rescans or artwork storms. Offline access should continue using the local catalog.
8. Review and save the deck, verify native rollback and unchanged owned/proxy counts and physical collection subtotal.

**Next milestone:** per-deck Mainboard printing/appearance choice (#169), which must not mutate global preferred printing or real/proxy ownership.
