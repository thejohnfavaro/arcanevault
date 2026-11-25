# Arcane Vault Windows v0.9.226 (Preview)

## Added
- Owned physical and Arena quantities in Card Details, including linked cards in AI reviews.
- Unresolved physical and proxy quantities shown separately.

## Changed
- AI review card previews now read verified local collection ownership.

## Fixed
- Linked AI recommendations previously lacked an owned-count summary.

## Removed
- Nothing from collection, decks, or backups.

## Verification
- Verify native compilation, regressions, package and installer via GitHub Actions.
- Check owned, unowned, Arena, and proxy quantities from AI review card links.
- Publish as a tagged `native-v0.9.226` release on main, allowing the application's existing update checker to discover the production-channel preview release; PR artifacts are not the update channel.
