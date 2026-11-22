# Arcane Vault Windows v0.9.213 (Preview)

## Added
- Read-only **Observed Opponent Colors (Partial)** deck analytics derived only from verified public opponent cards in archived Arena GRE telemetry.
- Offline Scryfall `arena_id` -> Arena `grpId` lookup; regression checks for private-zone exclusion, verified opponent ownership, unresolved IDs, catalog import and deck scope.
- A clearly separate observed-colors matchup table. User-recorded opponent color identity is preserved independently.

## Changed
- An existing catalog without `variants.arena_id` is rebuilt by the next normal Scryfall bulk sync even if its source date is unchanged. Neither match history nor collection ownership is modified.
- Unrevealed or ambiguous colors stay unknown. Observing some colors does not establish the opponent's complete color identity.

## Fixed
- v0.9.212 Actions run 45066057430 failed WPF compilation with `CS0103` in `OpponentColorObservation.cs` because `File` lacked the `System.IO` import. The explicit import is now present.

## Removed
- Nothing. All saved match results, manual color annotations, owned printings and prior analytics remain intact.

## Verification
- The new compiler correction is committed; the v0.9.213 Windows CI, native tests and installer must complete before using the build.
- Refresh local Scryfall catalog in Data & Sync before checking previously archived GRE events.
- Compare All Decks vs individual decks. The observed-color row is partial evidence from revealed public objects; the recorded opponent color row is unchanged.
