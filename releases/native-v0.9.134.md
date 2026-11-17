# Arcane Vault Native Windows v0.9.134

## Added
- Full-size **Match History** sidebar workspace in the native Windows app. View recorded Arena match results, detection notes, saved deck picker and a **Needs assignment / review** filter.
- Manual correction for an **existing** match, with explicit confirmation. The result is left unchanged unless the user deliberately chooses another result. Unknown results require explicit review.
- Stable native deck ID on manually assigned match rows. A backward-compatible migration adds an optional identity column to the separate local match-history database; no ownership or deck-library data is touched.
- Match tracker consumes verified Arena deck IDs already linked to saved Arcane Vault decks and can safely backfill previously unassigned matches with that exact Arena ID.

## Changed
- Stream Deck and Xeneon Edge W/L attribution prioritizes exact saved native deck identity, then unique Arena deck ID, and finally unambiguous saved deck name.
- Deck mapping is refreshed only when the saved workspace changes instead of rescanning assignments every 15 seconds.
- Manual matching explains the detection note rather than silently assigning results.

## Fixed
- Previously unassigned Arena matches with a matching saved Arena deck ID can be linked without speculative deck-name guesses.
- Manually associated results remain assigned to the correct saved deck even if multiple decks share a display name.
- Match correction updates an existing match row by match ID without creating an extra win or altering collection ownership.

## Removed
- No functionality removed.

Matches remain local in `%LOCALAPPDATA%\ArcaneVaultNext\match-history.sqlite3`; legacy records are supported in the existing compatibility database. This build does not guess a winner or assign an unidentified deck automatically without a verified ID. The Stream Deck Companion and EDGE widgets remain compatible.
