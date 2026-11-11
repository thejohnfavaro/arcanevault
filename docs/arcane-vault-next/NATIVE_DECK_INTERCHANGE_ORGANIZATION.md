# Native Deck Studio — Decklist interchange and organization (AVN-040)

**Scope:** native Windows preview v0.9.6. This restores daily deck-import/export and basic organization; it does not replace all of the shipping WebView Deck Studio/Library workflows. Feature-parity issue: [#62](https://github.com/thejohnfavaro/arcanevault/issues/62).

## Import text into a new or existing deck

Open **My Collection → Deck Library → New deck / Edit selected deck**. Use **Paste Arena list** or **Import .txt**. The importer recognizes either plain count/name lines or these common MTG Arena sections:

```text
Commander
1 Etali, Primal Conqueror (MOM) 137

Deck
1 Sol Ring
4 Llanowar Elves (DAR) 168
36 Forest
```

Names are preserved (including split cards). Repeated names are combined. Card set code/collector number annotations in plain-text exports are discarded with an explicit warning because the current native deck store tracks card names and quantities, not each decklist card's chosen printing. The editor does **not write** anything until the user independently clicks **Review and save deck** and confirms the normal deck-size/legality/ownership review.

**Fail-closed:** lists containing sideboard, companion, maybeboard or wishboard entries, or more than one Commander, are explicitly rejected. Saving a partial import must never silently drop these cards. Full separate native JSON backups preserve all existing deck metadata and ownership.

## Export

**Copy Arena list** copies the current name/quantity decklist to the clipboard. **Export .txt** writes a text file, useful for manual transfer to Arena or other deck builders accepting text. This exchange does not contain saved revisions, print IDs, foil treatments, artwork, tags, favorites, folders or Arena match connections. For lossless portability, use **Export Full Backup**.

## Library organization

Edit a saved deck and change **Folder** (1–64 characters) or select **★ Favorite**, then use **Review and save deck**. Those changes create a new revision and full workspace rollback just like card edits. The library supports search across name, commander and folder and filters favorites. Favorites appear ahead of other decks; no underlying deck ID or ordinal changes.

## QA and acceptance

CI fixtures validate supported Arena-style and plain lists, duplicate counts, unsupported section rejection, import/export round-trip, non-destructive edit/no-op and stale-SHA checks, preserved legacy unknown fields, deck revision metadata, unchanged physical/Arena ownership and complete rollback. Real Windows acceptance must also test clipboard permission/failure states, large deck library filtering, sample actual Arena Brawl exports and the installed native update from v0.9.5 to v0.9.6.

## Known parity backlog

My Shortlist, full deck recommendations/goldfish/synergy/wildcards, detailed card printing/artwork preferences, price/valuation, match telemetry and advanced Data & Sync are separate native implementation slices. Do not mark Windows 1.0 complete until the user's accepted legacy feature parity scope and data-integrity gate have been met.
