# Arcane Vault Windows v0.9.106 — EDH Land Optimization & Mana Filters

## Added
- Optional **Optimize owned nonbasic lands** setting in EDH Lab's reviewable mana-base proposal. It recommends conservative swaps from surplus basic slots into verified, recorded multicolor nonbasics, including appropriate shock lands with an optional life payment.
- Mana-symbol-only toggle buttons for W/U/B/R/G and colorless commander identity discovery, with themed selected and hover feedback in the embedded EDH Lab workspace.

## Changed
- Commander identity filter uses clickable official native mana icons instead of checkbox glyphs and accompanying color names. Colorless remains exclusive of colored filters, and Clear colors still resets all filters.
- Mana-base review binds the selected optimization setting to the draft proposal, so changing the setting invalidates an old proposal before it can be applied.

## Fixed
- Previously completed land targets could not suggest better owned multicolor lands without manually replacing basics.
- Shock-land entry timing is recognized when offering optional upgrades rather than treating the life-payment entry as an unconditional tapped land.

## Removed
- Redundant EDH Lab color-filter checkbox glyphs and text labels. No filters or commander search modes were removed.

## Safety
- All proposed land changes require explicit review/approval, affect only the unsaved mainboard, preserve existing spells and nonbasics, and retain at least one copy of each original basic land type.
- No proxy holdings or physical/Arena records are changed. Arena suggestions never consume paper proxy counts; unowned cards are never fabricated.
- Feature PRs #218 and #219 passed native Windows build and .NET compatibility workflows before merging.
