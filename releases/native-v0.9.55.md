# Arcane Vault Native Windows v0.9.55 — Safe inventory reset & clean reimport

## Added

- One **Collection & Deck Library → Clear inventory…** control, with independent **Physical only**, **Arena only**, and **Both Physical and Arena** choices.
- Exact, scope-specific typed confirmation phrases (`CLEAR PHYSICAL`, `CLEAR ARENA`, `CLEAR BOTH`) and an additional acknowledgement that the user has checked their private full backup.
- SHA-verified native reset that stages a new database, verifies its schema and surviving inventory, atomically swaps it, and **retains the entire previous native workspace as a separately restorable recovery file**.
- No-op safety: clearing a scope that is already empty does not write to the workspace or create an unnecessary recovery file.
- Regression tests for all three scopes; preserving saved decks, revisions, artwork preferences, shortlist, wildcards, legacy metadata, and the other collection; stale SHA rejection; complete rollback restore; independent Scryfall catalog integrity; and UI confirmation safeguards.

## Changed

- The physical CSV/Excel import tooltip explicitly explains the existing reconciliation semantics: imported printings are compared by quantity, but *local-only rows absent from the import remain owned*. To replace an entire physical collection, **clear Physical first, then import the full replacement CSV/Excel**.
- After a nonempty inventory clear, Collection & Deck Library is reloaded from the newly verified SHA. The recovery file path is displayed to the user.
- Existing **Start Fresh Collection** is unchanged and continues to reset the full workspace, including saved decks and preferences; it is **not** used for the new selective clear.

## Fixed

- Users can now safely discard outdated accumulation from old imports, without deleting Arena ownership or saved decks when clearing Physical.
- A stale confirmation can no longer clear newer ownership changes: the selected scope's reset requires the current verified SHA and refuses a mismatch.

## Removed

- No card metadata, decks, revisions, shortcuts, themes, favorites, saved artwork, Scryfall catalog or export files are removed by selective clearing. Only the chosen physical and/or Arena inventory arrays are cleared.

## Data safety and workflow

1. **Export a full backup** using the existing native export before proceeding. Readiness Checker can parse the exported schema-2 file.
2. Open **Collection & Deck Library → Clear inventory…**.
3. Choose **Physical collection only** if you want to rebuild just your physical collection.
4. Type **CLEAR PHYSICAL**, acknowledge the backup warning, and confirm.
5. Verify Physical is zero, Arena and all saved decks remain present; keep the automatic rollback file.
6. Use **Import CSV / Excel** to import a complete snapshot. Inspect the existing quantity-diff preview before confirming. Reimporting the same snapshot a second time should be a no-op.
7. If desired, test the equivalent Arena and Both options with a throwaway collection first.
8. Check Set Progress, EDH Lab and Card Details with the rebuilt ownership and make sure the existing update process still works.

**Scope:** Native Windows preview v0.9.55, not 1.0. This release does not implement Android reset or automatic cross-platform sync. Android development follows after this data-safety gate and its own signing/backup verification.
