# AVN-026 — Native ownership editing (Arena + complete physical quantities)

**Status:** Native Windows standalone preview only. The shipping Windows app and Android data are unchanged.

## Physical Collection

Choose **Add physical printing** with a loaded catalog to search exact paper Scryfall print IDs; choose a set, collector number, finish and quantity. A second catalog read verifies the ID at write time; invalid, digital-only, conflicting and absent identities cannot be credited as verified physical printing. To change an existing physical ownership line, select **Edit physical quantity**. Enter 0–99,999; zero removes that specific row after typing **REMOVE**. Increasing a name-only row preserves its unresolved status; the app must never infer a Scryfall ID from its name.

## Arena Collection

Choose **Add Arena card**. Type an Arena card name and how many copies to add. Matching is by case-insensitive name only: if exactly one row already matches, its quantity increases; if none matches, a new row is appended; if multiple imported legacy rows match, the add is rejected to prevent silent combining. No local Scryfall catalog is required or format legality claimed.

Select one Arena row and choose **Edit / remove selected Arena card** to change its name, replace its absolute quantity or delete it by setting 0. Renaming to another existing name is rejected rather than silently merging. A selected imported duplicate-name row remains individually editable by original index. Existing deck references are **not automatically renamed** when an Arena name is edited.

## Reliability and persistence

Both editing paths perform in-memory schema-2 transformations of the preserved full native backup, validate quantities/conservation, and use `NativeWorkspaceSafety.Refresh` for version-checked staged SQLite updates. All successful changes preserve a complete independently verified `native-workspace.rollback-*.sqlite`. Selecting **Restore native recovery** restores an older version and preserves the displaced version as a new recovery copy. Stale workspace SHA and stale row identity refuse edits without publishing changes. Physical printings, Arena cards, decks/revisions, artwork, extra preferences and unknown fields outside the requested edit are conserved.

## Tested synthetically

Arena: add, case-insensitive repeat, new split card, explicit rename + quantity edit, zero/removal, ambiguous legacy names, selected duplicate update, invalid quantities, stale workspace/rows, untouched physical/decks/art/annotations, rollback and restore. Physical: selected-row increases, decreases and removals including unresolved printings; printing identity remains unchanged, recovery preserved.

**User-machine gate remains:** confirm physical/Arena totals and printings from real collection, intentionally edit and restore a small test entry, launch after restart, verify no data change in installed old app. Deck authoring, card scanner, independent Scryfall catalog sync and Android remain beyond this slice.
