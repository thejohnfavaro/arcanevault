# AVN-023 — Start fresh native Windows collection

## Use cases and behavior

A fresh user can start a blank native Windows collection **without running the legacy app, downloading a prior export, importing a CSV, or selecting a catalog**. A migrated user can deliberately discard the *active native collection contents* without losing the previous version.

Choose **Start fresh collection**. The confirmation window makes the scope clear: the current physical and Arena copy counts, deck/revision counts and preferred-artwork entry counts are shown. The user must type exactly **START FRESH** before proceeding.

- No native database exists: a complete schema-2 empty snapshot is generated in memory and imported via the verified atomic native SQLite import. No backup is fabricated when there was no previous collection.
- A native database exists: the currently verified backup SHA is captured before confirmation. The new empty snapshot is staged through the existing `NativeWorkspaceSafety.Refresh` transaction. It requires matching SHA, uses SQLite's verified row checks, and publishes the blank workspace using `File.Replace` with a uniquely named rollback of the **entire previous SQLite workspace**. Old recovery files are not deleted.
- The fresh snapshot has zero physical and Arena rows, decks, revisions, artwork, favorites, collection history and wildcard values; it does **not** contain the prior `legacyStorage` blob or source-specific preferences. It is not copied from the bundled 25,000 synthetic catalog entries.
- The Scryfall catalog file `%LOCALAPPDATA%\\ArcaneVaultNext\\native-catalog.sqlite` is not affected. The installed app `%LOCALAPPDATA%\\ArcaneVault` and its collection are never modified.
- A repeated reset of an already-empty matching fresh source returns no-op, without rewriting its files or generating an extra recovery point.
- The user may use **Restore native recovery** to restore the previous complete collection. The emptied state is then itself preserved as a rollback, allowing a second restore.

## Reliability gates

- First-run blank DB, zero physical/Arena/decks/art, and no other source requirement.
- Current database corruption or stale expected SHA: reject, never silently overwrite.
- Existing populated DB: keep prior typed physical and Arena rows, complete deck history, artwork/opaque fields and SHA in a recovery file.
- Validate the empty workspace post-publish, compare counts to zero, and preserve the original recovery before surfacing success.
- Catalog and old-app isolation; double reset idempotence; complete previous state restored by the existing verified restore pipeline.
- No real installed Windows profile is read or written during a fresh start.

## Still outstanding

The Windows-native UI is not yet an inventory editor: a newly empty workspace cannot yet be filled by typing cards or scanning cards in this preview. First card add/edit/delete with transaction journaling and safe partial-ownership updates is the next important Windows slice. EDH Lab and Android feature work remain backlogged until core parity.
