# Arcane Vault Native Windows v0.9.135

## Added
- Persistent, user-confirmed Arena deck ID mappings, stored in the local `match-history.sqlite3` database and associated with the exact Arcane Vault saved deck ID.
- Match History offers an explicit "Remember this Arena deck ID for future matches" checkbox when the selected match contains a usable Arena deck ID.
- On startup and after manual correction, match tracking reloads saved mappings, allowing future results and older unassigned matches with that exact Arena ID to resolve to the right saved deck.
- More useful diagnostics distinguish missing Arena IDs, unknown mapped IDs, and conflicting saved-deck IDs.
- Match History shows detected per-game W–L separately from the overall match result; unknown game details display a dash rather than fabricated game scores.
- Regression checks cover persistent mappings, remapping conflict protection, historical backfill, and duplicate Arena IDs.

## Changed
- Arena log deck-summary events no longer overwrite verified user-confirmed mappings.
- Stream Deck and XENEON EDGE snapshots refresh saved mapping information regularly without restarting the Windows app.
- New Arena result records carry the resolved stable Arcane Vault deck ID when one is verified.

## Fixed
- Repeated manual assignment of matches using the same Arena deck is reduced when Arena exposes its deck ID.
- Previously unidentified matches with an exact known Arena ID can be attributed without inserting duplicate wins or overwriting manually assigned records.

## Removed
- No functionality removed.

A match without a usable Arena deck ID cannot be linked solely from this mapping feature and remains available for manual assignment. Manual match result corrections do not change physical or Arena card ownership, and all recorded data remains local.
