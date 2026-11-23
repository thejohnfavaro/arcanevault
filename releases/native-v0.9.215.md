# Arcane Vault Windows v0.9.215 (Preview)

## Added
- Android-style **Collapse All / Expand All** actions in the Windows mainboard Cards/Manage tab.
- Clickable card-type headers that collapse or expand only their own category, with visible arrow indicators and a count of hidden entries.
- Regression checks covering card-type controls in both saved-deck and EDH Lab editor variants, plus exact set-number card-art lookup.

## Changed
- The mainboard groups remain a **read-only projection** of the existing editable Text tab. Collapsing groups never rewrites the card list, changes quantities, or modifies ownership.
- Both editor variants now prefer the user's explicitly chosen artwork printing, then the exact official Scryfall printing hinted by `Card Name (SET) collector` (for example, `Island (FIN) 297`), then representative Arena/base card artwork.
- The exact set/collector lookup queries the printings table directly rather than truncating a gallery at 300 candidates, which particularly matters for frequently reprinted basic lands.

## Fixed
- The deck editor card preview could show placeholder artwork while a valid Arena-style set-number printing existed in the offline catalog. The preview now uses the same explicit printing identity rules used elsewhere in Arcane Vault.

## Removed
- No existing deck editing, saved printing selections, match history, card counts, collection data, or Xeneon Edge functionality is removed.

## Verification
- Run the Windows build, the `native-printing-artwork` suite, and `native-visual-shell` checks.
- Open the saved **We Are the Borg** Commander deck, preview `Island (FIN) 297`, then switch to Manage/Cards to collapse and expand individual card-type groups. Verify text and deck counts remain unchanged.
- Verify an Arena Standard deck with `(SET) collector` notation previews exact art, and that a missing printing falls back to official base art.
- Validate saved-deck card previews in both the native editor and EDH Lab. On-device image rendering is not confirmed until user testing.
