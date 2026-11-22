# Arcane Vault Windows v0.9.199 (Preview)

## Added
- A readable artwork load failure tooltip and debug diagnostic when an official Scryfall image cannot be retrieved or decoded.

## Changed
- On-screen deck and catalog artwork retries one failed request while the image is visible.
- A corrupt disposable artwork-cache entry is invalidated before retrying the same official image URL.

## Fixed
- Transient artwork downloads and damaged cached image bytes no longer silently leave deck covers blank without another attempt.

## Removed
- Nothing. Saved custom deck art choices, collection ownership, match history, W/L results, and saved deck identities are unchanged.

## Verification
- Reopen a saved deck with a custom cover, confirm the card and printing remain selected, and verify the corresponding Deck Library artwork after loading.
- Check XENEON EDGE deck imagery after refreshing the snapshot. This release does not alter the already working W/L tracking.
- Test an invalid/offline Scryfall image and a corrupt disposable cached image. A transient failure should retry, and a persistent failure should expose a diagnostic without changing deck metadata.
- Run native Windows build and regression checks before installing.
