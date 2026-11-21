# Arcane Vault Native Windows v0.9.168 (Preview)

## Added
- Interactive gold card-name links in formatted ChatGPT AI Deck Review recommendations.
- Exact local Scryfall catalog validation for deck cards and AI-suggested swap-table or highlighted card names.
- Click a verified card name to open the existing full native card details screen, including art, Oracle rules, mana, type, rarity, color identity and format legality.

## Changed
- The AI Review remains visible behind the deliberately opened full card-details screen. Closing card details returns to the same result and current unsaved deck.
- Candidate matching prefers longer card names to avoid accidental partial-card links; unsupported AI names remain noninteractive.

## Fixed
- Previously, inspecting an AI-recommended card required leaving the review to search Card Catalog manually.
- The interface no longer treats arbitrary AI assertions about card names as verified card records; only exact local catalog hits become links.

## Removed
- Nothing. No deck, ownership, printing, proxy, sync or backup data is modified by displaying card details.

## Verification
- PR #237 passed native Windows WPF compilation and regression checks before merge. The release workflow separately verifies installer packaging.
- Live interactive card-click layout still requires desktop visual testing.
- No new automatic ChatGPT requests or AI-driven deck edits are made when inspecting cards.
