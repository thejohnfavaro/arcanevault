# Arcane Vault Native Windows v0.9.196 (Preview)

## Added
- **Arena Standard** deck format: 60-card minimum, four-of rule, Arena inventory coverage, cached Standard legality warnings and no separate commander zone.
- Arena Standard Ranked matches can be tracked per saved deck in Match History, Deck Analytics and XENEON EDGE v1.0.4 when a verified queue and deck association are available.
- Verified Arena **Constructed and Limited division steps** now parsed from `constructedStep` and `limitedStep` in `RankGetCombinedRankInfo` responses.
- Optional Mythic percentile and leaderboard position are retained when supplied by Arena.
- Account-level rank progress persists in backward-compatible nullable SQLite columns and is exported to XENEON EDGE.
- Regression checks for rank step parsing/round-trip and Arena Standard validation.

## Changed
- Standard card art uses a representative mainboard card rather than a nonexistent commander.
- The XENEON EDGE exporter counts only explicitly identified **Standard Ranked** events for saved Arena Standard decks; Play and unknown queues do not silently enter ranked win/loss.
- Rank progress is reported as the observed division step, not inferred wins-to-promotion or demotion risk.

## Fixed
- Rank responses previously discarded the available division-step fields.
- Deck formats and ownership hints previously could not distinguish Arena Standard from generic 60-card decks.

## Removed
- Nothing. Existing collection data, match records, saved decks and past rank snapshots remain intact.

## Verification
- Create an Arena Standard deck (at least 60 mainboard cards) with no commander; confirm Arena ownership preflight, Standard legality warnings and save.
- Map a verified Arena deck ID and Ranked Standard match to it in Match History; ensure separate Win/Loss in Deck Analytics and XENEON EDGE v1.0.4.
- With Arena detailed logs enabled, open the client and obtain a rank response containing `constructedStep` or `limitedStep`; confirm the XENEON EDGE rank panel shows the observed division step.
- When step or leaderboard data are missing, show unavailable rather than fabricate an estimate.
