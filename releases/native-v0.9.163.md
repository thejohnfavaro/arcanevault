# Arcane Vault Native Windows v0.9.163 (Preview)

## Added
- Optional ChatGPT plan sign-in experiment for the native Windows app using OpenAI's browser-based OAuth with PKCE, local callback, ID-token signature and nonce verification, and explicit plan-usage scope validation.
- AI Deck Review preview, with local prompt review, copy-to-ChatGPT handoff, and explicit in-app streaming analysis after ChatGPT plan authorization.
- EDH Lab **AI Review** action for the current unsaved deck, including commander and format.
- Three AI review focuses: deck health, card substitutions, and combos/synergies.
- Separate unchecked opt-in for sharing deck-scoped physical/Arena ownership-shortage context, not the entire collection.

## Changed
- Native visual-shell regression test now recognizes handlers in partial code-behind files.

## Fixed
- Native XAML visual-shell validation correctly recognizes new event handlers in partial code-behind files.

## Removed
- Nothing. Existing data, deck, sync and backup workflows are preserved.

## Safety and limitations
- AI is optional and disabled until expressly connected and used. Deck and collection databases remain unchanged.
- AI review results are advisory and cannot automatically apply deck edits.
- Account tokens are held in memory only for this preview; restart requires sign-in again. A nonsecret stable host ID is saved under the local ArcaneVaultNext AI folder.
- OpenAI authorization and live subscription inference require interactive testing on an eligible account and have not been verified in CI.
- Account switching, encrypted persistent credentials, refresh, model selection and richer AI-output validation remain follow-up work.

## Verification
- Windows WPF compile and native test groups passed in the pull-request build. The full packaging job was subsequently cancelled by the workflow runner, so end-to-end installation and live OpenAI OAuth are not yet verified.
- Existing card ownership, proxies, backups, match history, and sync data are not intentionally modified.
