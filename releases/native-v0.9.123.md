# Arcane Vault Native Windows v0.9.123

## Added
- EDH Lab live ownership rebase after a verified Arena/Physical collection diff or peer refresh, without discarding the current unsaved deck.
- Status message when the open draft's ownership is synchronized to the current native collection.

## Changed
- After the native collection is reloaded, the embedded EDH editor refreshes its read-only ownership counts and source SHA rather than retaining stale quantities.
- The locally verified EDH candidate snapshot and deck-shortage summary are rebuilt using the updated inventory, while the mainboard text and commander are preserved.

## Fixed
- Cards present in an updated Arena collection could still appear missing in an already-open EDH Lab draft because the editor retained ownership counts from before the import.
- In-flight old catalog analyses no longer override the refreshed ownership snapshot.

## Removed
- None. No collection quantities, saved decks, or import results are modified by this refresh.
