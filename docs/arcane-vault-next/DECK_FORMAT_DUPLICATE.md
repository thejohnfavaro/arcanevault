# AVN-029 — Native deck format and safe duplication

This incremental Windows-native slice extends **AVN-028** rather than replacing the saved Deck Library. It does not touch the installed legacy application or Android.

## Features
- Edit deck **Format** independently from deck name, optional Commander and card list. Supports named formats Commander, Arena Brawl, 60-card and custom names. Legacy deck rows without a `format` field show Commander when a commander is saved, else 60-card, without editing the imported source.
- Store selected format on new decks and when a saved deck is changed. No format-specific deck legality, color identity, card count or owned-card validation is claimed.
- **Duplicate selected deck** requires user confirmation and exact saved-row fingerprint plus current workspace SHA. The new deck has a distinct `deck_native_...` ID, copies card data and metadata, but starts with `revisions=[]` and clears linked `arenaDeckId`/`arenaSyncedAt`. The source deck remains byte-semantically unchanged.
- Every write uses complete schema-2 backup validation and a staged SQLite replacement with an independent, verified previous-database rollback. Physical/Arena rows, printing IDs, artwork, unknown top-level data and original legacy profile remain unchanged.

## QA
Regress legacy-format defaults, format-only edit with prior version appended, unchanged-form rejection, duplicate exact copy/new ID, retained custom folder/tags and untouched legacy metadata, Arena link/history reset in copy, stale row/SHA refusal, and restore of previous deck count with newer version retained.

## Still required for Windows 1.0
On-device recovery and performance acceptance, native-format rules and ownership/legality validation, independent release/update workflow, catalog UI refinement, safe edit/backup size budgeting, and native deck imports. EDH Lab remains paused.
