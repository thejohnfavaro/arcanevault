# Arcane Vault Native Windows v0.9.122

## Added
- **Smart Mana — Optimize Now** one-click button in Deck Studio / EDH Lab mana-base area.
- Uses the verified, owned and format-eligible land catalog to prioritize shock lands, reliable untapped sources, conditional sources and tapped fallback lands.
- Regression check for owned shock/untapped optimization, preservation of spell counts and constant land total when swapping basics.

## Changed
- Smart Mana generates and applies the exact mana-base diff directly to the UNSAVED draft without requiring a separate proposal/apply click.
- Smart Mana excludes paper proxies and never invents unowned land copies.
- The existing manual, reviewable Build proposed mana base workflow remains available.
- Auto-balance basics is disabled when Smart Mana applies its draft edits, preventing immediate reversal.

## Fixed
- Mana-base suggestions now expose explicit entry-priority ordering for Smart Mana's additive land selection.

## Removed
- None. Smart Mana never automatically saves or changes inventory, proxy records, or collection prices.
