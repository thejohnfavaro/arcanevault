# Arcane Vault Native Windows v0.9.21 (Preview)

First native Collection and Deck Library visual parity pass, following the approved Set Progress banner redesign.

## Added
- Readable two-line physical-card rows with stronger card names, printing subtitles and highlighted owned-quantity chips.
- Alternating dark surfaces and selected-row contrast in both Physical and Arena collection views.
- Multi-layer deck-box illustration treatment using lightweight native gradients, decorative geometry and visible deck-format labels.

## Changed
- Physical and Arena collection rows use consistent spacing, subtle separators, clearer visual hierarchy and accent-backed owned quantities.
- Deck Library cards use a richer branded gradient surface and upgraded deck-cover treatment, retaining commander, folder, favorite status and deck totals.
- All collection and deck lists retain WPF row virtualization and do not download artwork during scrolling.

## Fixed
- Reduced the dense spreadsheet appearance of native collection rows and generic unbranded deck cover placeholders.
- Improved readability of owned counts and set/printing context without changing the underlying verified printing data.

## Removed
- Generic "DECK" cover-label placeholder in favor of the actual format label.
- No card ownership, deck editing, search, filtering, caching or data-model behavior has been removed.

## Verification
- Open Collection → Physical and Arena: confirm names, quantities, sorting, double-click details and selected-row actions.
- Open Deck Library: test favorites, search, selection, edit/duplicate/delete and large libraries. Deck covers intentionally avoid network art until per-deck artwork metadata is explicitly supported.
- Resize the native app at common Windows display scales. Performance and existing collection data should remain unchanged.
- Preview release, not version 1.0.
