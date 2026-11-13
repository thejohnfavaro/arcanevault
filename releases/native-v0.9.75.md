# Arcane Vault Native Windows v0.9.75 — Click-to-preview and fast mainboard search

## Added
- **Mainboard Manage search** (above the type-grouped list): filter the current **unsaved** deck by case-insensitive card name, local type text, or role hints. Results combine with the existing Lands/Creatures/Spells/etc. type filter and Name/Quantity/Type visual sort.
- Group headings and copy subtotals recalculate to describe the **visible matching rows**. A zero-match search displays a clear "No matching mainboard cards" message instead of implying the deck was emptied.
- Search is a read-only visual lens; cleared/changed filters restore all mainboard rows. Selected card and artwork preview are preserved when the selected name is still visible, while offscreen thumbnails continue to use the existing virtualized cache.
- A visible interaction hint in EDH Lab Cards: **Single-click to preview · Double-click to add one copy**.

## Changed
- **Actual quick-add now requires a second left mouse click on a real card row** (`ClickCount == 2`). The first click is always selection/preview only, including when selecting a different card. Clicking blank space/scrollbars does not add a card. Quick-add still respects commander identity, local format legality, and the single-copy EDH safeguard.
- The explicit **Add (+1)** button remains the intentional method to increase basic lands or locally allowed multi-copy entries. No automatic deck modification occurs from a selection-change event.
- Non-EDH editors retain their original raw Text mainboard and existing add/remove behavior; no change to mainboard provenance or the verified save flow.

## Safety and performance
- No new file writes, collection changes, owned-printing mutations, or saved deck writes occur when previewing/searching.
- Search is bounded to 255 characters and filters a local read-only projection of the already parsed draft; no SQLite rescan or HTTP request is needed to search.
- The existing card art cache remains virtualized and cancellable. Manual mainboard Text edits remain authoritative and deck save still runs full native preflight.
- New regression guards cover first/second click separation, selector event purity, no click on blank rows, category+search combinations, malformed query limits, and unchanged mainboard text.

## Windows acceptance
1. Open EDH Lab → Cards. **Single-click** an eligible card. Confirm the card preview updates but the mainboard copy total does **not** change.
2. **Double-click** the same row. Confirm one copy enters the unsaved deck. Double-click again and confirm the EDH singleton quick-add does not produce two copies. For basics, deliberate **Add (+1)** still increments where allowed.
3. Try single-click on several different cards, click empty space and scrollbars, and change the Cards filter. Only an actual double-click on a card should add a copy.
4. Open EDH Lab → **Manage**. Search for partial names (including case differences), e.g. `sol`; also search by type or role such as `Instant` or `Ramp`. Verify type filters and sorting continue to work together.
5. Search for an absent name, then clear the search. Verify zero-match message appears and all unsaved mainboard entries return unchanged.
6. Select a card and adjust quantity while a search is active. The visible totals and row quantity should update; switch to **Text** to confirm edits are reflected there and no other lines changed.
7. Search while scrolling the illustrated mainboard; thumbnails must correspond to visible rows and remain responsive.
8. Run native Review and save; verify saved deck library return, previous revisions and ownership records are unaffected.

**Scope:** Windows native preview v0.9.75. CI is required for merge; WPF hands-on regression remains a separate test before stable 1.0.
