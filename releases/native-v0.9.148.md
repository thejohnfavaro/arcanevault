# Arcane Vault Native Windows v0.9.148 (Preview)

## Added
- Forward-looking Arena queue detection from explicit queue/pairing and deck submission events, in addition to match-room event IDs.
- Format labeling that keeps Competitive Brawl separate from unranked Brawl; other ranked formats retain their ranked designation where verified.
- Raw, unknown Arena event IDs remain available instead of being misidentified as a common queue.
- Read-only, timestamped account-level Constructed and Limited rank observations from verified Arena log responses. No opponent rankings or speculative per-match ranks are persisted.
- Optional `rank` field in the existing shared `streamdeck.json` presentation snapshot. Both device integrations remain backward compatible.
- Native regression tests for paired rankings, rank persistence, queue classification, escaped request formats and verified W/L stability.

## Changed
- Deck Analytics displays clearer queue names when the underlying match record already contains a supported explicit Arena event ID. Unknown past matches remain unknown rather than being guessed.
- The XENEON EDGE v0.4.0 widget can replace its old Latest Match tile with Current Rank, independently displaying Constructed and Limited tiers and the last observed timestamp.

## Fixed
- Match formats emitted in escaped `EventSetDeckV3` request parameters or explicit pairing submissions can now be captured for future matches.
- Current Arena builds may emit rank snapshots without a named rank event label. The scanner also accepts the paired Constructed/Limited level-field signature.
- Competitive Brawl queue variants are not accidentally grouped with unranked Brawl.

## Removed
- No match results, card records, saved deck assignments, or prior history are removed. The Edge latest-match tile is superseded in its separately packaged v0.4 widget.

> Rank snapshots are account-level and reflect the **last observed** Arena response. They are not guaranteed to reflect real-time server rank and are not retroactively assigned to old matches. Older missing queue identifiers remain Unknown format. Enable Arena detailed logs; running Arcane Vault alongside Arena improves capture of log events.
