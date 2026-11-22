# Arcane Vault Windows v0.9.212 (Preview)

## Added
- **Observed Opponent Colors (Partial)** in Deck Analytics, separate from manually recorded opponent color identities.
- Read-only GRE observation analysis: only objects with a numeric Arena `grpId`, an explicitly non-local `ownerSeatId`, a verified local match seat, and a matching public battlefield/graveyard/exile/stack zone count as potential opponent card evidence. Hands, libraries, hidden objects, unknown owners, tokens without an explicit card ID, and unrelated matches are excluded.
- Scryfall `arena_id` stored on individual catalog printings, with an index for offline MTGA `grpId` resolution.
- Regression tests for verified opponent ownership, private-zone exclusion, unknown IDs, scoped match IDs, and offline printing-ID persistence.

## Changed
- Scryfall catalog refresh now rebuilds an existing older catalog if it lacks the `variants.arena_id` field, even when the Scryfall bulk dataset timestamp is unchanged. Existing native catalog replacement and recovery safeguards remain in place.
- The opponent observed-color table is scoped by the same selected deck/All Decks, format and date range as confirmed matchup analytics.
- The analysis is intentionally conservative: an observed White/Black card identity proves only that those colors were seen, **not** that the opponent's hidden deck contains exactly White/Black.

## Fixed
- Previously archived, sanitized GRE card IDs could not be mapped to local card colors because the Scryfall catalog lacked Arena printing IDs and Match Analytics had no observed-opponent-card resolver.

## Removed
- Nothing. Manual opponent-color annotations, W/L history, deck attribution, local collections, and existing snapshots remain unchanged.

## Verification
- Refresh the local Scryfall catalog using **Data & Sync** after installing; older catalogs cannot resolve Arena grpIds until the updated bulk import completes. The app will display this prerequisite instead of guessing.
- Enable Arena Detailed Logs to collect future observations. Already archived GRE events are analyzed when they contain a verified local seat plus public opponent card evidence.
- In Deck Analytics, select a specific saved deck and compare **Against Opponent Color Identity** (manual annotations) with **Observed Opponent Colors (Partial)** (public card observations). Unknown evidence stays Unknown.
- Run `native-scryfall-catalog-sync`, `arena-log-assignment`, `match-performance`, and `native-visual-shell` tests, followed by Windows build/publish checks.
- CI and physical-install verification remain pending until the workflow completes.
