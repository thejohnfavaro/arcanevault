# AVN-028 — Native Deck Library v1 (safe deck authoring)

## Intent

Replace the old Windows WebView **Deck Library** persistence and basic deck editor with a standalone Windows-native workflow. The user must already have a verified native workspace (fresh or imported), but does not need the old app, a JSON export or a Scryfall catalog just to create/edit decks.

## User flow

1. Click **Open deck library** on the native main window.
2. In the library, select **New draft** or select an existing saved deck and click **Edit selected**. Set a name, a free-form stored format (Commander, Arena Brawl, 60-card or Other), optional commander, and a list such as `2 Sol Ring` or `4 Lightning Bolt`, one card per line.
3. Click **Save deck** and confirm. The new/updated deck is stored in the native workspace. Updates append a revision containing the previous saved deck's content; the entire previous native database is also retained as a unique rollback file.
4. **Duplicate** copies cards and metadata under a new ID without importing revision history or claiming the old Arena deck ID. **Delete** requires confirmation and removes only one selected active deck.
5. Use **Restore native recovery** from the main window to recover a deleted deck, accidental edit, or earlier complete collection. Each restore retains the displaced current database as a new recovery file.

## Data contract

- Legacy `deckLibrary` is an array of JSON objects, each with `name`, `commander`, `entries` and often `id`, `folder`, `tags`, `favorite`, `coverCard`, `arenaDeckId`, and `revisions`. Existing entries and revision history are preserved in the complete backup manifest and typed SQLite deck rows.
- The editor changes only a selected deck (anchored by its original ordinal + complete raw payload and native workspace SHA). All unrelated deck rows, physical printing IDs/finishes, Arena names/quantities, artwork preferences, and unknown top-level JSON fields must compare unchanged before publishing.
- New decks receive unique native IDs. An update creates an explicit before-edit revision without recursively embedding older revision history. Existing revisions are not truncated.
- Existing card-specific unknown annotations are retained for names that remain in an edited deck. Removed cards are removed intentionally. Duplicate names in the submitted card list are rejected; no silent quantity aggregation.
- A delete removes the selected active deck and its revisions, but the previous full SQLite database remains recoverable. Every successful native deck mutation uses `NativeWorkspaceSafety.Refresh` and its verified atomic staging/recovery mechanism.
- Card legality, commander color identity, ownership/availability, collection matching, and Arena imports are **not validated** in this slice. This is an explicit limitation; it is not yet a fully parity-complete 1.0.

## CI and acceptance

Synthetic regression: imported Commander and 60-card deck display, split-name card list parsing, creation, prior revision preservation on update, selected-row annotations, legacy folder/favorite/Arena metadata, duplicate IDs, exact selected-row delete, stale SHA/payload refusal, invalid/no-op input rejection, physical/Arena/unknown preference conservation, full database rollback and restore.

Windows 1.0 gate: actual user-machine database and backup after saves/restores, 100-card Commander deck, legality and ownership calculations, signed installer/updater, restart persistence, performance metrics and clean uninstall/upgrade behavior. Tests in CI do **not** prove user-device correctness.

Android feature work and EDH Lab remain paused until Windows core release acceptance.
