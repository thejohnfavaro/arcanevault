# Arcane Vault Native Windows v0.9.153 (Preview)

## Added
- **Move selected to Proxy** in the Physical Collection workspace. Ctrl/Shift-select up to 500 catalog-confirmed physical printing rows and enter how many copies to transfer from each.
- A full preview and confirmation listing transferred card names and quantities.
- Regression tests for multiple different printings of the same card, partial and full printing removal, stale workspace checksums, quantity validation, and rollback.

## Changed
- Physical-to-proxy conversion now subtracts the exact selected copies from verified physical printing ownership and increases the independently unpriced Proxy ledger by the matching card name.
- The entire transfer is verified and committed as one native workspace update, with one complete pre-change recovery point.

## Fixed
- EDH Lab mainboard now recognizes saved cards with MTG Arena printing annotations (such as `Arcane Signet (ANB) 117`) by catalog card name, instead of classifying an entire imported deck as Unverified. Original saved labels and quantities are preserved.
- Physical set progress and collection value no longer mistakenly include cards deliberately transferred to Proxy. Saved decks, Arena inventory, match history and original printing metadata of remaining owned copies are preserved.

## Removed
- No other collection data, existing proxy entries or features are removed.

This is a Windows native preview. The operation intentionally changes recorded physical ownership and therefore may change valuation and paper set completion. Only locally catalog-confirmed physical printing rows are eligible; unresolved legacy printing entries remain unchanged. Keep a full private backup before updating.
