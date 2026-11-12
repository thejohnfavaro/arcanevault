# Arcane Vault Native Windows v0.9.43 — Preview Cards Directly from Mainboard

The native Deck Studio/EDH Lab mainboard now supports **direct card preview** without
requiring the card to be visible or selected in the Cards tab. This continues the
v0.9.41–42 screen-space improvements while keeping the mainboard fully editable.

## Added

- **Click a mainboard line to preview** its card in the existing right-side
  CARD PREVIEW area. Arrow/Home/End keyboard caret navigation and initial
  keyboard focus also update the preview. Nothing is added to or removed from
  the deck by a preview.
- The same official cached Scryfall card details, oracle text, and illustrative
  card art as the Cards tab; existing debounce and cancellation avoid wasting
  lookups during rapid line selection.
- Read-only **owned copies** from the selected Physical or Arena pool, plus
  in-mainboard copies, even if the card is unowned or excluded from the
  filtered/limited card browser. Changing ownership pools refreshes the
  displayed owned count and representative art.
- Previewing split names such as `Fire // Ice` without splitting them,
  normalizing spelling, or rewriting the mainboard.
- Regression tests for caret selection, CRLF, blank and malformed lines,
  invalid quantities, out-of-range positions, physical/Arena inventory
  isolation, and both preview entry points.

## Changed

- A click or keyboard selection in the mainboard clears the Cards-tab list
  selection to prevent the old Add (+1)/Remove (−1) buttons from acting on
  a card that is no longer visually being previewed.
- Clearing the Cards-tab list via search/filter no longer blanks an
  intentional mainboard preview.
- The mainboard header now indicates that clicking a card line previews
  its card.

## Fixed

- Previously, the preview was bound exclusively to the Cards-tab list
  selection. Cards already in the deck could not be inspected without
  searching for them separately.
- Preview owned/in-deck counts no longer depend on an active card browser
  selection. No false assumption is made that the selected mainboard
  name is an owned printing.

## Removed

- Nothing. No database schema, collection merge/import behavior,
  deck-save or revision history, EDH legality checks, auto-land-balance
  rules, navigation, layout or updater behavior is removed.

## Limitations

- Only a syntactically valid `quantity Card Name` mainboard line
  triggers a new preview. Blank or incomplete lines leave the last
  card visible so typing is not interrupted.
- An eligible Scryfall catalog entry is **not required** to select a
  name from the mainboard. The preview will disclose when a name has
  no cached catalog details; it will not invent art or format legality.
- Preview art remains illustrative catalog imagery, not confirmation of
  an owned physical printing or exact digital Arena card style.
- This is read-only inspection, not an interactive card drag-and-drop
  grid; normal text editing and explicit saved-deck preflight remain.

## Local acceptance checklist

1. Update native v0.9.42 to **v0.9.43** through Native Updates.
2. Open a saved Commander deck in **Deck Library → Edit in EDH Lab**.
3. Click different mainboard lines, including a split-faced card if
   one is present; verify that the right-side CARD PREVIEW changes.
4. Move the text caret with **Up/Down** and other navigation keys:
   the corresponding card should preview without switching tabs.
5. Switch to **Mana & Roles** or **Improve** and click a mainboard card;
   preview should still update while preserving tab selection.
6. Select a card in the Cards tab, then click a different mainboard line.
   The old Cards-tab selection should clear and its Add/Remove controls
   must not act on that previously selected card.
7. Temporarily type a blank, partial or malformed line. No card is
   silently removed or added; the last valid preview stays visible.
8. Preview an unowned but locally cached card and verify it reports
   zero owned copies rather than failing to preview.
9. Switch the Physical/Arena ownership pool in a normal Deck Studio
   deck (where enabled); verify the preview reflects that pool alone.
10. Cancel editing without saving and confirm collection quantities,
    saved deck, revision history and backup files are unchanged.

Release gates: standard Windows build, native WPF and regression suite,
checksummed portable native ZIP plus user-device acceptance.
