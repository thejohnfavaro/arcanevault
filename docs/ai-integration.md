# AI Integration — first slice (November 2026)

## Current prototype

Windows **Options → AI Deck Review (Preview)** opens a read-only prompt composer.
Users paste an Arena-style deck list, choose **health**, **owned-card substitutions**, or **synergies**, inspect exactly what would be shared, copy the prompt and optionally open ChatGPT in their browser. No credentials, API keys, server calls, automatic uploads, or writes to inventory/decks/sync are involved. This is **not** an in-app ChatGPT account connection.

## Verified path to a real connection

OpenAI's **Sign in with ChatGPT** supports optional *plan usage* for eligible Plus/Pro members. Identity sign-in alone is not plan-usage permission. The OSS local-client flow supports dynamic registration and delegated OAuth for eligible Responses API requests, with user-managed limits. Review the live docs and applicable terms before implementing:

- https://developers.openai.com/siwc/quickstart
- https://developers.openai.com/siwc/token-sharing-open-source
- https://developers.openai.com/siwc/token-sharing-open-source/sign-in
- https://developers.openai.com/siwc/token-sharing-open-source/profiles-and-sessions
- https://openai.com/policies/sign-in-with-chatgpt-terms/

**Eligibility note:** open-source local apps are supported; commercial or hosted distribution may require partner approval. Do not confuse account identity with access to the user's ChatGPT chats/memory.

## Next small increments

1. Build a *separate* native WPF account/connection service, with an opt-in authorization screen, host-id registration, PKCE or approved sign-in flow, token protection using Windows credential protection, refresh, and disconnect. Never expose tokens to view layers or logs.
2. Show explicit **Connected / Plan usage authorized / Limited / Unavailable** states. Do not enable model requests until the actual plan-usage permission is granted. Display the app's usage limits and link to ChatGPT usage settings.
3. Replace the manual paste path with **Review current deck** using a read-only export from saved decks. Show a data-sharing preview with granular selection (deck list first; ownership only on separate opt-in). No raw collection databases or local files leave the device.
4. Add a provider abstraction and typed JSON suggestions for deck health, with validation against the local Scryfall catalog, card legality and ownership. Applying any recommendation requires an explicit user-approved deck-edit transaction.
5. Add deterministic tests for prompt generation, redaction, zero automatic requests, authorization gating, rate-limits and disconnect, then Windows CI. Reuse provider contract later on Android without syncing credentials.

## Security and product guardrails

- Default disabled, opt-in only. Never require AI for local features.
- Never read or transmit whole collection files, diagnostics, match logs, or sync credentials.
- Treat AI output as suggestions, not canonical rules text. Verify names, printings, color identity, and formats locally.
- Do not hard-code client secrets, call undocumented ChatGPT web endpoints, or use user session cookies.
- No invisible background requests; disclose all data sent and let the user cancel.

## Experimental native ChatGPT session flow (PR #233)

Windows now has a direct **Continue with ChatGPT** button in AI Deck Review, following the OSS authorization-code + PKCE + dynamic registration flow. It validates ID-token RSA signatures against OpenAI JWKS (issuer, client audience, nonce, expiry), checks the separately granted ChatGPT plan-use scope, gets the account's visible model list, then performs an explicitly initiated streaming Responses request with `store:false` and `stream:true`.

- Tokens and issued client ID are **in-memory only**, for the current app process; restart requires reauthorization. The stable host ID is stored under `%LOCALAPPDATA%\\ArcaneVaultNext\\AI\\host-id.txt`. Refresh tokens are not used yet and never persisted.
- This is a **preview**. It requires interactive validation with an eligible account; CI cannot test real OpenAI authorization. Do not label account sign-in or actual response delivery as production-verified.
- EDH Lab's AI Review button uses the unsaved draft. Optional physical/Arena shortage coverage starts unchecked; only a scoped summary for cards in the deck may be included.
- No automated deck edits, whole-inventory exports, sync credential sharing or silent inference.
- Next: persist per-account issued registration and encrypted refresh tokens securely, add account/workspace switch, model picker, cancellation and stream progress, robust recoveries, explicit revocation, and offline/mock tests.
