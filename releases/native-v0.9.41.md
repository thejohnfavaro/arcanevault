# Arcane Vault Native Windows v0.9.41 — EDH Lab Workspace Redesign + Reconciled CSV/Excel Imports

This release makes EDH Lab workable at ordinary desktop window sizes and
replaces *default additive* physical/Arena collection CSV/XLSX importing
with **explicit reviewed, idempotent quantity reconciliation**.

## Added

- Tabbed EDH Lab workspaces, independent scrolling and resizable deck/card
  panes; a reviewable CSV/XLSX quantity-diff dialog for paper and Arena.
- Confirm-before-apply snapshot reconciliation with unchanged/new/adjusted/
  explicitly removed row types, verification status and full recovery copies.

## Changed

- Visible physical and Arena CSV/XLSX imports now reconcile matching quantities
  to file values instead of adding them unconditionally.
- EDH deck-building tools are separated into Cards, Mana & Roles and Improve.

## Fixed

- Reimporting identical quantities no longer duplicates ownership counts.
- Expanded EDH analysis no longer consumes the card browser's list area.

## Removed

- Nothing. Existing additive internal APIs and all native collection,
  deck-library, backup and updater capabilities remain available.

## EDH Lab layout

- **Cards**, **Mana & Roles**, and **Improve** tabs replace the old vertically
  stacked tool pile in the middle column.
- The mainboard remains visible on the left, with a persistent card preview
  on the right. Resizable splitters let users dedicate space to the deck,
  tools, or artwork.
- Cards gets a full-height picker with collapsible filters. The local
  format, recorded inventory, commander-mechanic and role filters remain.
- Mana & Roles has a scrollable blueprint, Arena-style automatic basic-land
  balancing, colored-mana weighting and curve/casting-consistency analysis.
- Improve has a scrollable collection-aware curve optimization and the
  **Swap Lab** proposal list, explanations and explicit-confirmation action.
- **Find next role** and **Browse owned early-play candidates** switch back
  to Cards automatically so the matching cards are immediately visible.
- For ordinary saved-deck editing (not EDH Lab) the familiar owned-card
  picker remains accessible, while EDH-only tabs stay hidden.
- The title, introductory description and outer margins are tighter to
  maximize editing area. All existing save, text-import/export, favorite,
  folder, card preview and deck analytics actions remain.

## Physical CSV / Excel diff imports

- **Import CSV / Excel** now compares the imported quantity with the
  current quantity of each matching card printing, rather than adding
  the imported quantity every time.
- A resizable, scrollable **Review collection differences** dialog lists
  action (new/adjusted/removed/unchanged), card, printing, current quantity,
  imported quantity, signed difference and verification status.
- Physical printing identity is determined using Scryfall printing ID +
  finish when known, or exact unresolved name/set/collector/finish
  attributes when not verified. Unresolved cards never silently become
  verified printings.
- Duplicate printing rows in the imported file are aggregated safely.
  Existing ambiguous duplicate printings targeted by the import fail
  closed with an error rather than guessing which local row to edit.
- **Unchanged** imported records remain untouched. **New** import rows
  are inserted. An increase applies only the positive delta; a decrease
  applies only the negative delta. An explicit **0** removes exactly that
  matched record; a zero for a nonexistent card is a no-op.
- Local cards **absent from the file remain untouched**. This is a
  non-destructive partial snapshot, not a replace-entire-collection action.
- Reimporting the same file after a successful reconciliation produces
  a genuine **no-op** with no unnecessary backup or collection rewrite.
- The final transaction revalidates the current collection SHA and the
  exact approved result SHA. A changed local collection or changed diff
  requires a fresh review before the original is touched.
- Every applied change creates a new **full native recovery copy** and
  preserves Arena cards, decks, revisions and artwork preferences.

## Arena CSV / Excel diff imports

- The same row-level review and Apply workflow, but Arena quantities
  match on case-insensitive **card names**, not paper printing metadata.
- Duplicate names in the import are aggregated; targeted local duplicate
  names must first be resolved manually.
- Identical, increased, decreased, new, and explicitly zero quantities
  follow the same semantics. Local-only names are left alone.
- Arena import cannot create physical printing ownership or alter paper
  inventory, decks, favorites, revisions, or artwork.

## Important limits

- This is **quantity reconciliation**, not price reconciliation. Pricing
  still comes from the app's dedicated valuation sources.
- Source rows without printing identity remain unresolved, and matching
  them by name alone against a verified specific printing is unsafe;
  these rows are deliberately kept separate.
- CSV and XLSX rows must have names and quantities. The quantity field
  accepts whole numbers from 0 to 99,999. XLSX formulas aren't evaluated.
- No import executes until explicitly approved in the diff dialog.
  If nothing differs, **Apply** is disabled and no recovery point is made.
- Existing additive import APIs remain available internally for backward
  compatibility; the visible collection import buttons now reconcile.
- EDH Lab still uses existing heuristic curve/synergy/swap analysis.
  This release improves access and presentation rather than changing
  recommended deck power or card legality.

## Local acceptance checklist

1. Update v0.9.40 to **v0.9.41** using Native Updates. Open the Borg Queen
   saved deck from Deck Library → Edit in EDH Lab.
2. Verify the middle workspace shows **Cards**, **Mana & Roles**, and
   **Improve**. The card picker should fill Cards vertically, and the
   Mana/Improve panels scroll independently. Mainboard and art preview
   stay visible at ordinary and maximized window sizes.
3. In Mana & Roles, click **Find next role** and confirm the appropriate
   role results open in the Cards tab. In Improve, click **Browse owned
   early-play candidates** and confirm it similarly opens Cards.
4. Select a swap in Improve and confirm its preview is visible and
   only applies after explicit confirmation. Cancel without saving
   and verify the original deck and collection remain unchanged.
5. In Physical Collection choose **Import CSV / Excel**. Confirm the
   diff table lists before/imported/change for exact printing + finish,
   along with unresolved printing warnings.
6. Import a CSV/XLSX containing one unchanged quantity, one increased,
   one decreased, one new printing and one existing card not listed
   in the file. Confirm the final diff and that the local-only card
   stays intact.
7. Import the exact same file again. The diff should show all
   **Unchanged**, disable Apply and not create a new rollback.
8. Test a row whose imported quantity is explicitly **0**: the
   corresponding exact printing should be removed only after
   deliberate review; absent records should be preserved.
9. Repeat in Arena Collection, verifying name-based reconciliation
   and that physical collection numbers are unchanged.
10. Change the local collection after opening the import preview or
    use an older stale checksum: commit must fail, preserving the
    current workspace and previous rollback copies.

Release gate: standard Windows CI, native WPF compile and regression tests,
published checksummed ZIP and updater verification, followed by local
device acceptance. No persistent data-schema migration.
