# Arcane Vault Native Windows v0.9.86 — Embedded EDH Lab

## Added
- EDH Lab commander discovery now opens in the main application workspace, using the existing sidebar and central content area rather than a separate commander-browser Window.
- Building a Commander or Arena Brawl draft opens the full EDH Lab deck editor in that same central workspace, with its existing card preview, mana planning, improve tab, and collection-aware tools.
- Deck Library → **Edit in EDH Lab** and **Review shortages** now route their verified saved decks to the same in-app editor.
- **Commander browser** and **Return to draft** controls switch between discovery and a live, unsaved editor. Switching to Card Catalog or Deck Library does not close or discard the current draft.
- Explicit discard confirmation protects an unfinished draft when opening a different commander or saved deck.

## Changed
- The central EDH Lab surface is now a WPF UserControl host like the native Collection/Deck Library navigation. The original separate Window implementations are retained temporarily for unrelated legacy code paths.
- Saving still requires preflight review, exact native expected-SHA checks and an explicit confirmation. On successful save, EDH Lab returns to a refreshed Deck Library.
- EDH browsing keeps the same locally cached commander index and read-only catalog behavior; optional full card details still opens separately.

## Fixed
- EDH Lab no longer traps you inside a separate non-integrated commander window, and its deck editor no longer blocks the main application as a modal dialog during the EDH Lab editing flow.
- Normal sidebar navigation preserves an in-progress EDH draft in memory until explicitly saved or discarded.

## Removed
- Mandatory modal deck editor and independent commander-browser Window from the standard EDH Lab and Deck Library → Edit in EDH Lab paths.

## Safety
- Draft content stays unsaved until **Review and save deck**; moving between workspaces does not write collection or deck records.
- Starting another draft requires affirmative confirmation before discarding the existing draft.
- New editor still uses the established native deck preflight and recovery mechanism; physical and Arena ownership, Proxy ledger, prices, and collection value are unchanged by navigation.
- This is the first integrated workspace implementation; other legacy deck-editing entry points may still use the old standalone editor.
