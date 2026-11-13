# Arcane Vault Native Windows v0.9.80 — Deck Library and EDH Lab

## Added
- Deck Library: explicit selectable Commander entry above the mainboard for Commander and Arena Brawl. The separate leader is included in total card display and ownership/proxy coverage without rewriting saved mainboard entries.
- Mainboard Manage: choose an official Scryfall printing per saved deck card entry, prioritize verified owned printing IDs, and preview selected artwork without changing global preferred artwork, prices, collection inventory or Proxy ledger. Persist and restore through saved deck entries and revision backups.
- EDH Lab Cards: collection-aware land recommendations filtered by commander legal identity, availability (owned, paper Proxy, unowned), and local catalog hints. This preserves existing click-to-preview and double-click-to-add workflows.

## Fixed
- Deck Library inspector no longer shows an unlabeled second row of color-identity pips immediately under the exact mana cost. Oracle symbols and casting costs remain intact.
- EDH Lab Cards / Mana & Roles / Improve tabs use explicit selected, hover and focus colors to avoid white-on-white text on Windows light themes.

## Safety and compatibility
- Existing native collection, printing ownership, proxy valuation, saved deck quantities and prior backups are preserved.
- External plaintext deck list formats do not carry per-deck display printing IDs.
- Exact owned printing identity is based on matching physical Scryfall IDs; catalog-only selections are cosmetic display preferences, not owned copies.
- This is a portable Windows build, not a signed installer.

## Validation
- .NET 10 WPF compilation and native regression test suite, plus Windows x64 portable workflow, must pass before publication.
