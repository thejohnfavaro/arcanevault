# Arcane Vault Native Windows v0.9.188 (Preview)

## Added
- Local Scryfall-backed autocomplete for commander names in both Windows deck editor surfaces.
- Manual opponent color-identity tagging in Match History (W/U/B/R/G or C; empty means unknown).
- Deck Analytics opponent color-identity win/loss breakdown, scoped by deck, queue, and time range.
- Optional opponent color-identity metadata in Windows-to-Android match analytics snapshots.

## Changed
- Commander suggestions use the front-face name for transforming cards and do not overwrite the user's existing saved decks or collection.
- Matchup statistics count only confirmed win/loss results; unknown opponent identities are counted separately.

## Fixed
- Avoids treating unavailable opponent identity as colorless or a guessed color.
- Advances release version to avoid an already-published version collision.

## Removed
- Nothing. Existing physical ownership, Arena inventory, saved decks and recovery data remain unchanged.

## Verification
- Check both WPF deck editors with Etali, Primal Conqueror; choose a suggested commander and verify the deck still saves.
- In Match History, annotate a known opponent identity, refresh Deck Analytics, and check the matchup breakdown.
- Sync the optional analytics peer snapshot to Android and ensure unknown identities remain unassigned rather than guessed.
