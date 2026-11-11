# AVN-028 — Windows native Deck Library editing

## User path

1. Open the independent Arcane Vault Next Windows executable. If necessary, use **Start fresh collection**.
2. Choose **Open native collection → Deck Library → New deck**. Enter deck name, optional commander, and plain `quantity Card Name` mainboard lines.
3. Confirm the deck save and reopen the Deck Library. The new saved deck, its unique persistent ID and quantity totals are available without any installed-app export or WebView2.
4. Select a deck and **Edit selected deck** to change name, commander or mainboard list. Edits retain deck ID, folder/tags/cover, unknown fields and append a complete snapshot of the previous deck to revisions.
5. Select **Delete selected deck** to remove that exact deck from the active library after confirmation. The prior complete workspace and deleted revisions remain in an automatic recovery file. Use the main window's **Restore native recovery** to recover it.

## Format and data boundaries

- Source of truth remains schema-2 `deckLibrary` within the preserved full native workspace, plus a typed `decks` projection. New records use `id, name, commander, entries:[{name,qty}], updated, favorite, folder, tags, coverCard, revisions`. Existing records retain any additional unknown fields, Arena link or metadata.
- No automatic ownership deductions for cards added to a deck, and no printing-ID or legality claims. `physicalCards`, `cards` (Arena ownership), `preferredArtwork`, wildcards, favorites and opaque source fields must remain unchanged.
- Exact saved-deck selected ordinal + fingerprint and the complete native-workspace SHA must match before update/delete. The file is atomically replaced only after the whole resulting schema-2 backup and typed row projection are verified; a unique rollback preserves the complete previous native state.
- A deck edit appends a revision with the previous name/commander/entries plus timestamp, label and supported cover/folder/tags. **Existing older revisions are not truncated**. Deleting a deck removes revisions from the active deck only after explicit confirmation; the full prior native database retains them.
- Mainboard input accepts one count-and-name per line, max 500 nonempty lines, quantity 1–250 each, up to 1,000 total copies. Duplicate names combine case-insensitively only within the explicitly provided new mainboard list. Split card names are not rewritten. Legacy deck rows with unsupported/ambiguous entries remain visible, but cannot be edited destructively.

## Regression coverage

Synthetic full legacy deck library with deck IDs, 2 revision histories, favorite/folder/tags/cover, unknown future fields and per-card role metadata; save edited mainboard, append revision, preserve prior and future fields, keep ownership/selected artwork unchanged. Create a new commander-optional deck, explicit delete, stale workspace/row refusal, no-op rejection, invalid card quantities, and complete restore from verified SQLite rollback. Legacy malformed entries are listed but fail closed on edit. Both .NET 10 WPF / native tests and shipping Windows .NET 8 checks must pass before merge.

**Still open:** user-machine collection and catalog acceptance, deck format legality/color checks, linked card details, deck import/export UX, scanner, pricing and Windows signed installer/update path. Android and EDH Lab remain backlogged.
