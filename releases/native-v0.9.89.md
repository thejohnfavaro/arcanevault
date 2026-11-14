# Arcane Vault Native Windows v0.9.89 — Persistent workspace navigation

## Added
- Collection, Deck Library and Set Progress now retain their active views while switching through the main sidebar and EDH Lab. Set searches, selected deck/set, filters, open checklist, and in-view scroll positions remain part of the same live WPF control instance.
- After a **real** save/import/reverification, the application captures a transient UI-only snapshot (Collection search, missing-only, Deck Library search/favorites/selected stable deck ID, Set Progress scope/search/empty/unowned toggles/selected set code) and restores only still-valid selections after loading the new verified workspace.
- Regression guards ensure native workspace navigation does not reopen the local SQLite backup merely to switch sidebar sections and a reattached view does not recalculate expensive proxy/deck cover/valuation queries.

## Changed
- Sidebar navigation reuses the verified workspace immediately when already loaded. A selected tab remains selected without rerunning its change handlers needlessly.
- Initial proxy ledger, deck-art and valuation initialization is one-shot per verified Collection workspace instance.
- A new sidebar request made while an authoritative reverify is running wins over the earlier selected section.

## Fixed
- Ordinary WPF Unloaded events no longer permanently mark the live Collection workspace as closed. Explicit invalidation for a changed backup SHA cancels stale set/inspection/valuation work instead.
- Selected Set Progress checklist and Deck Library selection can be restored by verified identity after an actual collection update, rather than resetting to the first page.

## Removed
- Redundant native workspace open/check on already-loaded sidebar transitions and repeated initial expensive data loads on reattachment.

## Safety
- All UI state is transient and presentation-only: it is not written to collection, saved decks, Arena, proxy ledgers, or prices.
- If a selected deck/set disappears during an actual edit/import, the new verified model wins; no stale ownership information is invented.
- WPF automated tests cover the lifecycle and navigation invariants; visually confirm resizing, set detail preservation, and direct EDH-return navigation on Windows.
