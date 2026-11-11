# Arcane Vault Native Windows v0.9.15 (Preview)

Windows native catalog and navigation correctness update. No owned-card or saved-deck data migration is required.

## Added

- **Set Progress** as its own destination in the main-window sidebar. The existing Set Progress checklist remains part of the native collection workspace; clicking the new shortcut navigates directly there without opening another window.
- Arena-available **Card Styles** scope in the Printings & Artwork gallery when opened from an Arena card.

## Changed

- **Owned on Arena** and **Not owned on Arena** catalog searches now restrict results to printings reported as Arena-available by the local Scryfall catalog; name-based Arena ownership does not make paper-only versions available.
- Arena Card Details use the selected Arena-available printing's artwork rather than generic paper-card artwork.
- Arena Collection details prefer Arena-available digital-style artwork when present, and the gallery exposes Arena-available styles only.
- Artwork preferences for paper-only editions are not applied when viewing an Arena-scoped card.

## Fixed

- Removed the remaining numbered **Arena 1** / **Arena N** catalog ownership badge while preserving Arena inventory quantities.
- Prevented paper-only printings of an Arena-owned card name from appearing in Arena ownership searches.
- Prevented a physical-only artwork preference from masquerading as an available Arena card style.

## Removed

- Redundant numbered Arena ownership notification on catalog cards; quantities are retained in the Arena Collection inventory and details.

## Testing notes

- Verify **Set Progress** sidebar navigation, **Owned on Arena** filtering, Arena Card Details art, and **Printings & Artwork** styles with a card having both physical-only and Arena-available printings.
- The Arena collection currently records card names and quantities, not the exact Arena printing purchased. Eligible artwork in this release means Scryfall-listed Arena-available printings, not an assertion of ownership of every cosmetic style.
- Full Set Progress visual and Physical/Arena tracking parity remains separately tracked as issue #84. Wider legacy-to-native parity is tracked in issue #82.
- This is a native preview release, not Windows 1.0. Existing collection, decks and backup/recovery files remain separate from executable updates.
