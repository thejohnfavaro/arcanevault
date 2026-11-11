# Arcane Vault Native Windows v0.9.13 — Integrated Collection & Deck Library

Windows x64 prerelease, still behind the Windows 1.0 feature-parity gate.

## New / changed
- **My Collection** and **Deck Library** are dedicated options on the permanent Windows sidebar, next to **Card Catalog**, without spawning a second window.
- Selecting **My Collection** opens the same native physical/Arena/Set Progress workflow inside the main window, with the Physical Collection tab selected. **Deck Library** opens the saved-deck tab directly.
- **Card Catalog** navigates back to the main catalog. The current collection tab remains selected when switching between workspace sections or after native edit/refresh.
- Reuses the complete existing WPF collection view as a **shared, embedded UserControl**. The existing import/adjust physical printing, Arena inventory editors, saved-deck create/edit/duplicate/delete, Set Progress and collection pricing workflows are preserved, including optimistic-SHA checks and separate full recovery points.
- The existing **open external full backup** action remains an intentionally separate **read-only preview window**, but now uses the exact same shared collection control. No duplicate copies of the collection/editor code remain.
- Navigation is restored after full backup restore, Start Fresh, native catalog change, official Scryfall sync, and shortlist edits. During verification, the workspace shows an explicit loading state; failed verification never resets or overwrites the native collection.
- Selecting a deck-related tab directly updates the active sidebar state. Modal add/edit dialogs are retained until their independent workflow is refactored, but the Collection and Deck Library *browsing screens themselves* are no longer separate windows.

## Reliability and compatibility
- Native profile, legacy WebView data, owned printing IDs, Arena quantities, saved decks/revisions, pricing snapshots and artwork preferences retain their exact existing file formats; this update does **not** migrate or rewrite collection data.
- Shared WPF control is reused for the read-only backup preview and editable native workspace to avoid two drifting data/edit implementations.
- A stale edit invalidates the embedded control, opens a verified fresh native SQLite workspace and returns to the same section rather than reusing old edit SHA checksums.
- Versioned native updater and Windows build/release pipeline are unchanged.

## QA / limitations
- Verify physical CSV/XLSX import, physical adjustment, Arena edit, create/rename/duplicate/delete deck, set progress, valuation and card details from inside the main shell. Verify the edit dialog returns to its previous section and no second Collection window appears.
- Verify no double window opens at startup, sidebar survives switching Catalog ↔ Collection ↔ Deck Library, and native Check Updates still works.
- The legacy exported-backup preview intentionally remains a separate read-only window. Deck creation/editor dialogs and My Shortlist still use their existing dialog windows for now.
- Windows 1.0 remains blocked on real Windows collection acceptance plus additional legacy Deck Studio, Match Performance, Wildcard Planner, Strategy/Synergy and Data & Sync parity (#62).
