# Arcane Vault Native Windows v0.9.174 (Preview)

## Added
- Deck-context-aware links for shortened card references in ChatGPT AI Review (for example, **Tezzeret** or **Dawnsire** rather than their full card names).
- Canonical-name matching against the local read-only Scryfall catalog; an unambiguous card already present in the reviewed deck takes precedence when other catalog cards share the nickname.
- Regression tests for unambiguous Tezzeret and Dawnsire nicknames, globally ambiguous aliases, two matching cards in one deck, and partial-word collisions.

## Changed
- Full-name matches and verified short-name references are both linked while constructing the final native WPF AI Review document. A click on a shortened reference opens its canonical full card details.
- The completion message counts short-name links as well as full card names.

## Fixed
- AI Review could leave abbreviated card references such as **Tezzeret** and **Dawnsire** unlinked even when there was a clearly identifiable matching card in the saved deck or catalog.
- Only distinctive standalone short references can resolve; ambiguous references do not silently open an unrelated card.

## Removed
- Nothing. This release does not change saved decks, collection records, printing choices, proxies, sync data, backups, or the ChatGPT account session.

## Verification
- Windows WPF compilation, local-catalog regression checks, and native acceptance checks passed in PR #244. Main branch release workflow validates installer packaging.
- Interactive card-link navigation requires on-device visual verification. Ambiguous nicknames with multiple equally plausible cards remain unlinked rather than guessing.
