# Arcane Vault Native Windows v0.9.64 — Full-details double click and Oracle reading surfaces

## Added

- **Double-click for full details and printings:** Double-click the actual card artwork tile, Detailed row or Compact row to open the existing full Card Details window, including printing/artwork browsing and the current shortlist controls. The full-details button in the inspector remains available.
- **Readable Oracle text surfaces:** The catalog's persistent Card Inspector and the full Card Details window now use a non-editable, borderless FlowDocument reading view. Rules text is presented like printed reference material instead of a recessed text input, with more comfortable type size, paragraph spacing and line height.
- Native regression checks that specifically verify both double-click paths, shortlist-star protection, correct hit-tested row activation, preserved selections when switching list modes, borderless Oracle viewers, scrolling, official inline mana symbols and exact-text copy.

## Changed

- **Single click:** Still selects the card for the persistent docked Inspector, without spawning a window. Rapid selections retain the existing async stale-result guard.
- **Double click:** Now opens **full details/printings** for the clicked card rather than automatically widening the inspector. The **Widen inspector** control remains a separate, optional action.
- **All three Card Catalog views** share this behavior. Switching between Card art, Detailed and Compact still preserves the docked inspector and selected printing.
- Oracle text remains selectable and scrollable, with official Scryfall mana symbols inside rules text where the symbol metadata is available. The full-details **Copy exact text** action continues to copy raw Oracle wording and original brace notation.

## Fixed

- Double-clicking previously enlarged only the docked inspector, adding an unnecessary step before reaching full printing details.
- Oracle text had a bordered, filled RichTextBox appearance that suggested an editable form field instead of read-only card rules.

## Removed

- The automatic **Widen inspector** action from double-click.
- The nested Oracle-text form-field bezel and input styling in both card detail surfaces.
- No collection ownership, printing metadata, deck data, shortlist, backups, settings or native recovery points have been removed.

## Windows acceptance

1. In **Card Catalog → Card art**, single-click a tile to update the inspector, then double-click it to open **full Card Details and printings**. Confirm no unexpected inspector expansion.
2. Test both **Detailed list** and **Compact list**. Double-click a real card row to open that exact card, and double-click empty list space to verify no unrelated details window opens.
3. Click or rapidly double-click a shortlist star to confirm it only performs its bookmark action and does not open full details.
4. Switch between Card art, Detailed and Compact with the inspector visible. Selected card and inspector state should remain useful, without losing scroll/paging or changing tile width.
5. Inspect long Oracle rules text and cards with mana/tap/hybrid symbols. Confirm the text reads like a document (no recessed box), the official mana glyphs remain aligned, and scrolling is preserved.
6. In the full window, use **Copy exact text**, paste externally, and verify every original brace token and line break are intact.
7. Confirm the explicit **Widen inspector** and **Open full details & printings** buttons still operate independently.
8. Confirm read-only catalog interaction does not modify owned quantities, deck data, variants or saved printings.

**Scope:** Native Windows preview v0.9.64. Automated compile and regression checks do not replace on-device visual inspection at different scaling levels.
