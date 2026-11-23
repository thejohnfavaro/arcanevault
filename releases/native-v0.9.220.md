# Arcane Vault Windows v0.9.220 (Preview)

## Added
- Mainboard card-type composition in both saved-deck and EDH Lab editors, showing unsaved card copy counts and copy-share percentages by type.
- A **Full card details** action on selected visual mainboard cards, including commander reference rows, plus double-click to inspect full Oracle text, format legalities, and offline printing metadata.
- Tests for full-mainboard composition and visual-shell navigation, including empty decks.

## Changed
- Composition always reflects the authoritative unsaved Text mainboard regardless of visual search, sorting, card-type filtering, or collapsed categories; commander reference isn't counted as a mainboard copy.
- Card details open from an offline read-only lookup and don't supply the native workspace write context.

## Fixed
- v0.9.219 run 45189772008 failed WPF compilation with CS0103 on ArenaDeckCardName in both new editor handlers. Imported the missing ArcaneVault.Next.Domain namespace in each file.

## Removed
- Nothing. Deck text, saved decks, card ownership, printing preferences, artwork, match history and Xeneon Edge records are unchanged.

## Verification
- Run the Windows desktop compilation, EDH Lab draft and native visual-shell regressions, and installer packaging.
- Confirm the composition totals remain unchanged when the user searches or collapses groups. Check double-click and Full card details for Island (FIN) 297 and an unverified card.
- The release is not accepted until CI and local interactive review pass.
