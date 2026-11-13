# Arcane Vault Native Windows v0.9.73 — Visual EDH mainboard manager

## Added
- **Manage / Text** tabs in the EDH Lab mainboard pane. Manage is the initial EDH view, while the original editable **Text** tab remains fully available. Non-EDH saved deck editing still defaults to Text.
- **Organized card rows** showing names, precise current unsaved quantities, locally verified card types and role hints. Under **By card type**, cards are grouped with section headings and group subtotals. **Name A–Z** and **Quantity descending** change display order only.
- **Type filters** for All, Lands, Creatures, Artifacts, Enchantments, Instants & Sorceries, and Other / Unverified. Unverified/off-identity names already present in the source are shown for review or removal, not silently hidden or relabeled as legal.
- **Card selection preview:** selecting a visual mainboard entry uses the existing persistent official artwork/Oracle inspector. Selection is restored across live deck updates where the name is still present, without repeatedly reloading the same preview.
- **Exact quantity controls:** −1, +1 and Remove all operate only on the selected current mainboard entry. The current authoritative deck text is reparsed before any action; +1 is permitted only for locally eligible names within the catalog's suggested copy limit. Unverified or ineligible cards can be reduced or removed but never incremented.
- Visual mainboard changes refresh the EDH Cards browser, deck counts and existing health analysis without writing ownership or saved decks. All changes remain unsaved until the separate **Review and save** action.

## Changed
- EDH Lab defaults to visual Manage view on new and existing EDH decks. Previous Text mode is one click away for import/paste, quick correction and full card line editing.
- Mainboard visual refresh follows the existing 220 ms draft-analysis debounce for manual Text edits; explicit +/-/remove actions refresh immediately.
- Buttons reflect the selected card and disable +1 at its locally supported copy limit. The current quantity is resolved from the Text tab at click time, rejecting stale or malformed edits rather than trusting stale visual rows.
- The previous v0.9.71 double-click singleton quick-add, v0.9.72 Available/In deck/All browser filter and v0.9.70 deck-save fast refresh remain in place.

## Fixed
- Managing a mainboard previously required editing text lines, navigating the caret or using the Cards browser to remove copies. Most common quantity changes and browsing are now available from a persistent visual list.
- Grouping/sorting doesn't change the persisted ordering or count of the underlying mainboard.
- Unknown/missing catalog metadata does not get silently treated as eligible during the +1 action.

## Removed
- Nothing is deleted from collection, owned printings, local catalog, card caches, saved decks, or recovery revisions. Visual filters and sorting do not change data; explicit Remove all only edits the UNSAVED mainboard.
- No new collection or save workflow, and no second synchronized persisted deck representation.

## Windows acceptance
1. Open EDH Lab. The mainboard left pane starts on **Manage** with type-grouped card rows. Use Name A–Z and Quantity sorting; verify counts and names stay unchanged in the Text tab.
2. Click a card row. The existing card inspector shows the corresponding card's official metadata; changing the filter/ordering preserves the selection when the card is still shown.
3. Use **−1** on a multi-copy basic land, then **+1**. Confirm the Text tab reflects the exact current quantity. Use Remove all and confirm that name disappears from the unsaved mainboard and visual rows.
4. Try +1 on a regular singleton: it should be disabled at one copy. On a basic land it should allow more copies within valid local limits. For an unverified/off-color card in a loaded legacy deck, allow removal but never increase its count.
5. Add a new card from the existing EDH Cards browser using double-click. The Manage list refreshes; selecting it previews its card and the quantity remains one on repeated double-clicks.
6. Edit several mainboard lines directly under Text, including temporary invalid text. Manage must report malformed edits instead of silently dropping entries, then recover after the Text tab is corrected.
7. Verify role/mana/health analysis, deck coverage, native Save preflight, rollback revisions, and v0.9.70 in-place Deck Library return still work.
8. Confirm regular non-EDH deck editing continues to show its existing Text editor by default.

**Scope:** Native Windows preview; requires successful CI and user Windows interaction regression before stable 1.0.
