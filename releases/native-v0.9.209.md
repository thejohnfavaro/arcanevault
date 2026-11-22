# Arcane Vault Windows v0.9.209 (Preview)

## Added
- Regression guard that Deck Analytics presents the opponent-color matchup table and never reintroduces a "By Your Deck Color Identity" panel.

## Changed
- Deck Analytics color-identity breakdown refers **only to the opponent's recorded color identity**, scoped by the selected deck or All Decks.
- The revised wide-screen layout pairs opponent color matchups with the format table, while weekly performance remains a separate readable section.
- Removing the irrelevant local commander-color identity catalog query also eliminates an extra native workspace verification from analytics refresh.

## Fixed
- Incorrect "By Your Deck Color Identity" visualization: it had categorized the player's deck by its commander colors when the desired metric is which opponent colors the player wins or loses against.
- The opponent matchup table continues to read explicit opponent color annotations from Match History. Missing opponent color annotations remain Unknown; own deck colors are never substituted or guessed.

## Removed
- The own-deck color-identity chart and saved-commander color lookup in Deck Analytics. No match records, color annotations, saved decks, collection or W/L histories are edited.

## Verification
- Confirmed Windows v0.9.208 workflow run 45057944890 succeeded.
- Check native Windows CI, especially the `native-visual-shell` and `match-performance` suites, for v0.9.209.
- In a deck-scoped view, opponent-color row totals must include only the selected deck's confirmed matches. In All Decks they must aggregate all assigned confirmed matches; unknown opponent colors remain Unknown.
- Compilation and installer status for v0.9.209 have not yet been independently confirmed.
