# Arcane Vault Native Windows v0.9.5 — Readable Scryfall Legalities

This Windows x64 native preview fixes a Card Details formatting bug. It does **not** modify your collection, official Scryfall catalog, or saved legality data.

## Added
- A dedicated **Format Legalities** section in Card Details, with individual entries for Standard, Modern, Commander, Brawl, Historic, Timeless, Pioneer, and the other formats supplied by the cached Scryfall catalog.
- Clear status labels with accessible text and distinct styling: **Legal**, **Not legal**, **Banned**, **Restricted**, or **Unknown**.
- A compact summary with the number of cached formats and legal/banned/restricted counts.

## Changed
- Card Details now displays type, rarity, mana value, and color identity on readable lines rather than appending serialized metadata to a single field.
- Format status entries live in a bounded scrolling panel; Oracle text and card art remain prominent.
- Missing or corrupt legality JSON produces an explanatory message rather than a raw JSON object or an unhandled exception.

## Fixed
- Raw Scryfall snippets such as `"standard":"not_legal","future":"not_legal"` are no longer displayed directly. They appear as **Standard — Not legal** and **Future Standard — Not legal**.
- Regression coverage for all Scryfall status types, multiword format names, unexpected statuses, missing metadata, and byte-identical read-only catalog access.

## Removed
- Raw legality JSON from the Card Details presentation only. The Scryfall database still stores complete original JSON for compatibility, offline deck validation, and future refreshes.

## Notes
- No reimport or Scryfall re-sync is needed when the existing local catalog has data: this is a pure display change.
- Legality is **cached Scryfall data**, not a promise of up-to-the-minute format bannings. Consult current official rules if entering an event.
- This is a self-contained Windows preview, not a certified production 1.0. Real-device data/recovery acceptance in issue #51 remains open.
