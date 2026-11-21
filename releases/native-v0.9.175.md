# Arcane Vault Native Windows v0.9.175 (Preview)

## Added
- AI Review hyperlinks for brief, distinctive character-name references as short as three letters when exactly one canonical card in the currently reviewed deck matches.
- Regression coverage for the exact phrase “Sai and Anointed Procession”, linking Sai to Sai, Master Thopterist only when that card is in the reviewed deck.

## Changed
- Short-name aliases of three or four characters now use deck-specific disambiguation instead of being categorically excluded from clickable card references.
- Longer full names and unambiguous catalog aliases continue using the existing read-only catalog verification and final WPF review rendering.

## Fixed
- AI Review rendered Sai as plain text beside clickable Anointed Procession even when Sai, Master Thopterist was part of the reviewed deck.
- Prevents short, potentially common words from linking outside a uniquely matching deck context.

## Removed
- Nothing. Collection records, saved decks, proxy information, synchronization, and ChatGPT account sessions remain untouched.

## Verification
- Windows WPF compilation, native regression tests and read-only acceptance checks passed in PR #245.
- The versioned main-branch workflow validates release packaging; live card link click-through should be verified in Arcane Vault on Windows.
