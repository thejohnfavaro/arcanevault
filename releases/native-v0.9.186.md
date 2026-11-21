# Arcane Vault Native Windows v0.9.186 (Preview)

## Added
- Verified incremental catalog-review update for single-row physical quantity adjustments, removals and exact printing additions. Newly introduced paper printing IDs are rechecked against the read-only local Scryfall catalog.
- In-place Arena inventory refresh for saved quantity/name edits and additions; Arena name-based set coverage recalculates in a nonblocking task only when its view is active.
- Regression cases comparing incremental physical review to a full catalog scan and rejecting invalid new printing identities.

## Changed
- Add Printing, Adjust Physical Quantity and Arena Editor now provide the fully verified atomic replacement to the existing Collection workspace. The workspace keeps Catalog, Deck Library, and unaffected set/price models alive rather than closing on every edit.
- Physical set totals and ownership are recomputed using the existing verified catalog denominators. Physical valuation refreshes asynchronously after edits.
- Arena edits preserve physical review, prices and paper set progress and update Catalog's ownership overlay without a full workspace rebuild.

## Fixed
- Unnecessary full collection reloads after common physical and Arena inventory changes.

## Removed
- Mandatory full UI reopen for qualifying single-record physical and Arena changes. No collection, deck, proxy, sync or recovery records were removed.

## Verification
- Run the complete native WPF build, native regression suite and official installer workflow. Manual testing should cover adding a new paper printing, increasing/decreasing and deleting a physical quantity, and editing Arena quantities.
- Every write retains optimistic SHA verification, independent integrity checks, atomic SQLite replacement and a complete recovery point. Any unsupported or mismatched change falls back to the original authoritative full reload.
