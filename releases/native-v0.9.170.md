# Arcane Vault Native Windows v0.9.170 (Preview)

## Added
- Verifiable interactive card names for both **Cut** and **Add** columns in the ChatGPT AI Review swap table, including the Priority | Cut | Add | Why layout.
- A clear status showing how many names were verified and made clickable, or an actionable error when the local catalog cannot resolve them.
- Efficient batched verification of proposed card names using one strictly read-only SQLite catalog connection.

## Changed
- AI Review prioritizes proposed cards before scanning a large mainboard, so a 100-card deck no longer crowds recommendation names out of the link budget.
- Card links work with a normal click, not Ctrl+Click; the separate Copy review button still preserves full text copying.
- The existing native full card-detail screen remains the click destination, returning to the same review when closed.

## Fixed
- AI Review could show “Card links unavailable; the review is still readable” and no card links despite successful response rendering.
- Four-column recommendation tables previously omitted the Add column when collecting card names.
- Slow per-card repeated catalog opens caused avoidable lookup delays; an invalid unsaved deck line could also prevent valid recommendation link creation.

## Removed
- Nothing. No changes to saved decks, collection data, printing choices, backups, sync, or ChatGPT credentials.

## Verification
- Native WPF compilation and regression checks passed for PR #239 before merge.
- Release packaging and native installer verification are performed by the main-branch Windows workflow. Live interactive card navigation still requires testing in Arcane Vault.
