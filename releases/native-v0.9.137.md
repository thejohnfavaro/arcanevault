# Arcane Vault Native Windows v0.9.137

## Added
- Parse MTG Arena's EventSetDeckV2 and EventSetDeckV3 queue-submission RPC lines, including the JSON-encoded request body and nested Summary/params objects.
- Capture submitted deck IDs from actual CourseDeck events before matches, rather than assuming Arena includes a deck ID in the match-room configuration.
- Use a recent explicit queue submission to link the subsequent match to a verified saved deck (20-minute bounded correlation); preserve the submitted Arena ID even if its native mapping is unknown.
- Show a dedicated **Arena ID** column (Captured / Not captured) and total count in Match History, with assignment-diagnostic notes.
- Regression coverage using synthetic Arena Player.log sequences for V2, V3, CourseDeck, unassociated IDs and passive inventory summaries.

## Changed
- Only actual deck-submission events establish the recent active Arena deck. Ordinary deck-list summaries do not change active matching.
- Arena submission records may contain an ID without a name; exact persistent mapping can still identify the saved deck.
- Existing manual assignments, previous W/L statistics and Stream Deck / Xeneon Edge output remain compatible.

## Fixed
- Matches with no deck ID in `matchGameRoomStateChangedEvent` can now identify their deck from Arena's separate queue-submission event, reducing repeated manual deck assignment.
- V3 and double-encoded deck-submission records were previously ignored by the tracker; newly received events can now be recorded.
- When a submitted Arena ID cannot be mapped to a saved deck, the ID is kept for later manual mapping instead of discarded.

## Removed
- No functionality removed.

This build does not infer an Arena ID from an unrelated inventory list or a stale selection. Match outcome verification and manually assigned match rows remain unchanged. Arena must have Detailed Logs enabled and must emit a decodable deck-submission event for automatic deck recognition.
