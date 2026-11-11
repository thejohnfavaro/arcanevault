# Arcane Vault Native Windows v0.9.6 — Deck Studio Interchange and Organization

**Preview release** — restores a meaningful slice of the former Windows WebView Deck Studio and Deck Library. Not a feature-parity-complete Windows 1.0; see issue #62.

## Added
- **Deck Studio text interchange:** import an MTG Arena-style decklist from a local `.txt` file or clipboard. Recognizes a `Deck`/`Mainboard` section, optional single `Commander` section, `4 Card Name (SET) 123` and ordinary `4 Card Name` lines. Duplicated card names combine under the existing native deck-count limits.
- Copy an Arena-style name/quantity list to the clipboard or export a `.txt` file from the current editor text. The separate full native backup remains the lossless copy with deck revisions and metadata.
- **Deck organization:** choose a folder and mark any saved deck as a favorite directly in the native editor; these settings persist, are included in deck revision history, and support rollback.
- **Deck Library:** search by deck name/commander/folder, filter ★ Favorites, and sort favorites ahead of other decks without modifying authoritative stored order.
- Automated tests for Arena text parsing, round-trip import/export, quoted/multiword names, exact preservation of opaque legacy deck fields, folder/favorite history, stale-edit rejection, and full workspace recovery.

## Changed
- Imported text fills the editor only after an explicit prompt; **Review and save** still runs advisory format/ownership checks and requires separate confirmation before editing SQLite.
- Deck format selection remains explicit: import does not guess Commander, Brawl or tournament legality.

## Fixed
- Previously, all native deck edits kept favorite/folder metadata but the editor could not edit them. This release supports native edits without discarding unknown deck fields, card annotations, prior revisions, or Arena sync links.
- Explicit refusal of sideboard, companion, maybeboard or multi-commander deck exports **if they contain unrepresentable cards**; those sections are not silently discarded.

## Removed
- None. The old WebView app, its saved data and updater remain intact.

## Limitations
- Imported Arena printing annotations are stripped with a warning; the native saved deck interchange currently stores card names and quantities, not per-card edition/finish selections.
- Sideboard cards, Partners/multiple commanders, wishboards and complete deck revisions/tags are not captured by external plain-text interchange. Use the native full backup for a lossless collection/deck export.
- This is **one parity slice**, not the full feature restoration. Remaining collection browsing, card artwork, pricing, shortlists, deck analytics, Arena logs, match history, synergy, wildcard tools and data sync remain tracked in issue #62.
- Windows 1.0 still requires on-device backup/migration parity, safe updates, and performance/UX acceptance.
