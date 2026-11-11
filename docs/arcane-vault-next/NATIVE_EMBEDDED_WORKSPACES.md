# AVN-047 — Collection and Deck Library in the Windows sidebar

## Goal

Remove the distracting second **Native Collection** top-level window while preserving all its working inventory, deck and Set Progress behavior. The main native shell should navigate among Card Catalog, My Collection, and Deck Library directly. This is a presentation/navigation refactor, **not** a collection data migration.

## Implementation

- `MainWindow.xaml` exposes **Card Catalog**, **My Collection** and **Deck Library** as distinct permanent sidebar actions. The main content swaps between existing catalog and a host `ContentControl`.
- `NativeCollectionWorkspace.xaml/.cs` is the original collection content and event logic promoted to a shared **UserControl**. The host selects its Physical Collection tab (0) or Deck Library tab (2) from the sidebar; users may still switch among Physical, Arena, Deck Library and Set Progress tabs inside that view.
- `NativeCollectionWindow.xaml/.cs` is now a thin shell for explicitly opened **read-only exported full backup previews**, hosting that exact same UserControl. Therefore no second copy of the collection business logic drifts over time.
- WPF dialogs opened from embedded content use the top-level `Window.GetWindow(this)` as owner. CSV/XLSX file pickers, deck editors, Arena editors, card details, recovery status and confirmation prompts continue functioning with the new parent.
- The main shell retains one embedded workspace view at a time. On a successful edit, it invalidates stale optimistic-SHA state, rereads/verifies the SQLite collection and refreshes ownership, price, decks and Set Progress. The active tab survives the refresh. Navigation back to Card Catalog does not mutate or close the user's inventory.
- A generation guard rejects data produced by an obsolete catalog/collection load. Invalidation during a pending read triggers a fresh read once the earlier task completes. Errors display non-destructively.

## Regress / acceptance checklist

1. Launch with an already migrated native collection. Card Catalog should be the only main screen; no Collection popup opens automatically.
2. Click **My Collection**. Physical collection should appear inside the main window with quantities, valuation, filters, CSV import and Set Progress tabs available.
3. Click **Deck Library**. Confirm the saved-deck tab opens in the *same* main window, retaining all existing decks/revisions.
4. Click Card Catalog and return. Confirm sidebar highlight matches the current section, there is no duplicate collection window, and tab changes retain the correct active route.
5. Create/edit/duplicate/delete a deck; adjust a physical printing and an Arena quantity. Confirm each editor's successful commit reopens the **embedded** section, preserves data and creates a full native recovery file.
6. Open card details and save preferred art or a shortlist bookmark. Confirm the active collection reloads rather than accepting an outdated SHA. Restore a backup; values and deck library must still be correct.
7. Select **Open full backup preview**. The deliberately separate, read-only backup viewer remains available and must not write native inventory.
8. Run Full Scryfall Sync, then navigate to Collection. Confirm official printing ownership and progress refresh; the updater remains functional.
9. Test at 100%, 125%, 150% scaling and while resizing down to the app minimum width; horizontal scrolling is acceptable, but no essential command should become inaccessible.

## Remaining scope

The separate deck *editor* dialog is preserved in this slice. Converting deck editing to an inline workspace, implementing advanced deck card picking, Arena logs and other remaining WebView modules are tracked separately on [Windows parity blocker #62](https://github.com/thejohnfavaro/arcanevault/issues/62). Do not declare Windows 1.0 based solely on source CI.
