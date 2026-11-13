# Arcane Vault Native Windows v0.9.77 — Proxies, Not Valued Ownership

## Added
- A separate **Proxies · no value** collection tab for user-declared proxy or third-party trading cards. Enter a card name and precise quantity (1–99,999), select to edit, or deliberately remove a proxy record. The UI and full native backup tag these records as **Proxy**.
- The full schema-2 JSON backup now supports an optional **`proxyCards`** array of `{name, qty, kind:"Proxy"}` objects. Historical complete backups without it remain valid. Proxy edits stage a complete replacement SQLite workspace, validate SHA/record contents and retain the prior workspace as an independent rollback.
- Paper Commander/60-card deck coverage recognizes proxies **separately** from real owned copies. Deck analysis, Card Preview and Saved Deck Inspector label `real owned` and `Proxy (not valued)`, while paper shortage checks consider proxy-backed copies. Arena Brawl and the digital inventory never count proxies.
- EDH Lab's paper card browser shows locally legal proxy-backed names in `Available` and recorded-only browsing, while still showing real owned quantities independently and respecting identity/format preflight.
- Explicit preflight notes when a paper deck is covered partly by proxies, including the fact that proxy copies are not owned printings or necessarily legal in sanctioned play.
- Compatibility guard for importing older full backups: if a legacy export omits `proxyCards`, current proxy records are carried forward rather than erased. Only an explicit full fresh start removes them, with a rollback copy.

## Changed
- The physical collection valuation continues to evaluate **only verified real physical printing rows**. Proxies never join `physicalCards`, acquire print IDs, trigger per-proxy Scryfall quotes, affect set-progress, or inflate collection value. A proxy named the same as an owned card still receives **no collection price**.
- The existing physical/Arena edit flows, card-metadata cache, official artwork and deck save/revision handling continue to operate on their original real-owned datasets. Full backup export preserves proxy records unchanged.
- Proxy updates verify the source native workspace checksum and atomically stage a new complete backup with no destructive in-place rewrite.

## Fixed
- Previously a third-party card could only be represented by misclassifying it as physical inventory, which could inflate valuation or verified set progress. The new separate proxy ledger removes that ambiguity.
- Commander paper-deck coverage can now recognize deliberate non-valued proxy copies without describing them as official owned printings.
- Legacy full backups do not erase the proxy ledger when their format predates proxy tracking.

## Removed
- No existing real-owned physical or Arena records, deck revisions, printings, card catalog, artwork, preferences, full backups or recovery points have been deleted.
- No automatic conversion of an owned card to a proxy or vice versa. Proxy updates affect only explicitly selected named proxy records and use the verified rollback process.

## Windows acceptance
1. On a real native collection, open **Collection & Deck Library → Proxies · no value**. Add a card you also genuinely own, e.g. `Sol Ring`, with 2 proxy copies. Confirm the existing physical printing and finish quantities remain unchanged and the proxy appears separately as **Proxy**.
2. Add an original third-party art/card name with no Scryfall printing identifier. Confirm recording works without Scryfall resolution. Edit the count and remove it; verify the other proxy rows and physical/Arena inventories remain intact.
3. Compare **Physical Collection** value before/after recording many proxies of an officially priced card. It must remain exactly the same. Physical Set Progress and Arena counts must also remain unchanged; `Proxy` must never appear as a fake priced printing.
4. Open a **paper Commander** deck with one eligible proxy-backed card but no real owned copy. Confirm EDH Lab shows the Proxy count distinctly and that the missing-copy coverage counts it as a proxy, not owned. Save preflight should explicitly flag proxy usage and tournament caveats.
5. Open **Arena Brawl** with the same proxy. Its digital shortage remains present; proxies cannot satisfy Arena deck inventory.
6. Add a proxy-backed legal paper card through the existing card/Commander Synergy Package quick-add. Ensure only a deliberate double-click/Add inserts one copy, no collection mutation.
7. Full backup export and reimport must preserve `proxyCards`. Import a full **legacy** snapshot without `proxyCards` on a separate test workspace; the current proxy ledger must be preserved.
8. Inspect the native recovery list. Proxy changes must each produce a recoverable checkpoint. Verify normal Update/Restart, existing real-owner quantity edits, decks, saved revisions and prices.
9. Verify the existing `native-v0.9.76` Synergy Package double-click fix has not regressed.

## Planned next milestones
- Choose a specific owned printing or clearly labeled proxy appearance **from a card already in the mainboard**: [backlog #165](https://github.com/thejohnfavaro/arcanevault/issues/165).
- Collection-aware multicolor land suggestions based on commander identity: [backlog #164](https://github.com/thejohnfavaro/arcanevault/issues/164).
- Audit official WUBRG/hybrid/Oracle symbol usage, including `{2/U}`: [backlog #163](https://github.com/thejohnfavaro/arcanevault/issues/163).

**Scope:** Windows native preview v0.9.77. CI success and hands-on Windows collection-preservation tests required before any stable 1.0 claim.
