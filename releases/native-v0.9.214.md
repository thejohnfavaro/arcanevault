# Arcane Vault Windows v0.9.214 (Preview)

## Added
- A lightweight one-second workspace-file-stamp check in the existing Xeneon Edge exporter loop, so a saved deck edit can publish without waiting for the normal 15-second scheduled match/analytics refresh.
- Native visual-shell regression guards covering unchanged artwork reuse, rapid deck-change detection and inventory-only Set Progress invalidation.

## Changed
- Reusing a deck's stable saved ID and unchanged cover selection preserves its existing official artwork URL, cached key path and render state across edits to that or other decks.
- Unchanged existing key images are not downloaded, decoded or rendered again when the deck name and recorded W/L have not changed.
- Physical Set Progress cache invalidation uses a deterministic digest of `physical_lines` plus the Scryfall catalog file stamp. Editing a deck alone no longer triggers collection-wide set progress verification and recalculation.
- Automatic Arena Player.log scanning remains on its scheduled cadence; a deck-only file change doesn't force an expensive log rescan.
- Match result changes continue to invalidate the affected deck's W/L key image when its verified wins/losses differ.

## Fixed
- Slow Xeneon Edge widget updates after small deck edits caused by waiting for a 15-second poll, discarding all deck artwork/render state, and recalculating physical Set Progress for unrelated deck edits.

## Removed
- Redundant full tile recreation and set-progress invalidation after deck-only workspace mutations. No persistent collection, match, deck or proxy records are removed or rewritten.

## Verification
- Run Windows native regression suite including `native-visual-shell`; ensure the standalone and installer pipeline passes.
- After editing a saved deck, its updated name/cover state should appear in the exported Edge snapshot on the next lightweight worker tick without waiting for the periodic Arena log scan.
- Verify physical collection edits still invalidate Set Progress, and existing verified match records continue updating W/L keys.
- Confirm initial artwork creation and manually requested refresh continue to work. Existing cached artwork is cosmetic only; stable deck IDs and verified records remain authoritative.
