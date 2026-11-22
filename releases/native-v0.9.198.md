# Arcane Vault Windows v0.9.198 (Preview)

## Added
- Regression checks for older explicitly verified Arena Ranked Standard assignments, including generic `Ladder` queue matches.

## Changed
- Deck Library uses the same local official-printing artwork fallback as the XENEON EDGE exporter when an exact card has no canonical catalog image.
- Ranked Standard W/L export accepts verified historical deck assignments preserved in Match History notes when the newer native deck ID field is absent, provided the resolved saved deck is unique and the Arena ID is not blocked.

## Fixed
- Custom saved deck covers with missing default card artwork can now resolve a valid official Scryfall printing image for Deck Library tiles.
- Previously captured, verified Ranked Standard games no longer disappear from exported W/L simply because an older match row preserved its verified attribution in notes instead of the native deck-ID column.

## Removed
- Nothing. No collection, Arena match, ownership, proxy, saved deck, or revision records are deleted or rewritten.

## Verification
- Save a Custom Deck Art choice in a non-Commander deck; reopen the editor and compare the Deck Library artwork and XENEON EDGE artwork after exporter refresh. Ensure saved selection and owned printings are unchanged.
- For a historical generic `Ladder` match with a previously confirmed match assignment, verify the existing win or loss counts exactly once against the matching Arena Standard deck and corresponding widget.
- Verify unknown, ambiguous and unrelated Arena deck IDs, Standard Play and Brawl do not contribute to Ranked Standard totals.
- Run the Windows native regression suite and package/installer identity checks before installing. Artwork downloads still require accessible official Scryfall CDN or an existing local image cache.
