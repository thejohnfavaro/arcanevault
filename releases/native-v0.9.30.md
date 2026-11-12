# Arcane Vault Native Windows v0.9.30 — EDH Lab Inventory Preview

This release begins the next phase of Arcane Vault Windows after the locally approved v0.9.29 theme refinements: a safe, collection-aware first EDH Lab increment and a focused usability/reliability pass.

## Added

- EDH Lab's Commander Browser can optionally show **In my inventory only**, using recorded physical card names for paper Commander and separately recorded Arena names for Arena Brawl.
- An explicitly labeled recorded copy count for each commander in the current format.
- The inventory filter is disabled if the native collection cannot be safely loaded. Commander discovery from a valid local catalog remains available.
- Regression checks for format-separated inventory counts, repeat-name aggregation, zero-quantity exclusion and source-data preservation.

## Changed

- Random / no-repeat commander choices now respect the same optional inventory-only restriction as the visible results.
- Previous and Next controls are disabled at page boundaries; the active page is clamped to available results when filters change.
- The EDH Lab window now uses the same dynamic theme resources as the rest of native Windows, including the recently approved charcoal Black mana palette.
- Commander inventory is loaded read-only and asynchronously from the native workspace. Browse/sort/random actions never write to saved decks or ownership.

## Fixed

- The EDH Lab preview no longer displays hardcoded dark colors when the user selects a different app theme.
- Page navigation and random commander picks remain consistent when an inventory-only filter narrows the candidate list.
- Paper collection names and Arena collection names cannot silently count toward one another's format.

## Removed

- No existing features, inventories, catalogs, saved decks, recovery points, backups, or theme choices were removed.
- This release does not attempt to validate exact owned physical printing IDs or claim that a name-level match proves a complete Commander-legal deck.

## Manual verification

1. Upgrade through Native Updates from v0.9.29 and confirm v0.9.30 launches and your existing collection/decks still appear.
2. Open EDH Lab from the native sidebar with a synced Scryfall catalog. Browse both Paper Commander and Arena Brawl; verify eligibility and color-identity filters.
3. Enable **In my inventory only** and compare displayed names/counts to the currently selected Physical or Arena collection; counts are name-level inventory records, not verified printings.
4. Try Random with no-repeats on a narrowed result, use Previous and Next, and check empty results. The same set of eligible commanders should be used for listing and random.
5. Change the theme in Options (including Black and Daylight Archive), then open EDH Lab and verify legibility and consistent app colors.
6. Try opening EDH Lab without a native workspace in a separate synthetic environment. Commander browsing should work if the catalog is present; inventory filter should be disabled.
7. Export an independent full backup before testing and compare inventory and deck totals after. No collection changes should occur from EDH browsing.

This is a native Windows preview, **not** full EDH Lab deck construction or a v1.0 signoff. Windows build and .NET 10 native regression CI were green on PR #111; real-device acceptance remains necessary.
