# Arcane Vault Native Windows v0.9.167 (Preview)

## Added
- ChatGPT account connection management under **Options** with **Continue with ChatGPT**, connection status, reauthorization, and **Disconnect ChatGPT**.
- Renewable ChatGPT plan sessions protected by Windows CurrentUser DPAPI in a local per-user credential file, separate from all collection, deck, backup and sync data.
- Automatic OAuth refresh on the next AI request when the saved access token expires, using the OpenAI-issued client registration and rotated refresh tokens.
- Best-effort remote OAuth token revocation when disconnecting, with an explicit warning if remote revocation cannot be confirmed.
- Inherits the v0.9.166 disposable-process crash-recovery regression suite, including interruption immediately before and after the atomic workspace swap.

## Changed
- **AI Review** no longer shows Connect or Disconnect controls. It uses the account authorized in Options and continues to require clicking Analyze before sending any deck contents.
- ChatGPT authorization is retained across normal app restarts and upgrades on the same Windows user profile, rather than requiring a fresh sign-in for every launch.
- Regression runner includes the process interruption checks from v0.9.166.

## Fixed
- Short-lived ChatGPT access tokens previously forced users to sign in after every app restart; renewable protected sessions now allow silent renewal while the account grant remains valid.
- Strengthened verification that workspace refresh interrupted during staging or atomic replacement leaves a usable recovery path.

## Removed
- Removed duplicate ChatGPT sign-in and disconnect buttons from EDH Lab AI Review. No collection, deck or proxy data is removed.

## Verification
- WPF compilation, native regression suite and packaging passed in PR #236 before merge. Release packaging and publication are validated by the main branch native workflow.
- Live persistent-session and refresh behavior still require an interactive Windows check using an eligible ChatGPT account. An upgrade from the older session-only implementation needs one fresh sign-in to create the protected credential record.
- Reauthorization may still be required if the ChatGPT grant is revoked, expires, or changes; this is not permanent unrestricted access.
