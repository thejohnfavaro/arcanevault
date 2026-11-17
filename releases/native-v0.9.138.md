# Arcane Vault Native Windows v0.9.138

## Added
- Support for Arena log events where an EventSetDeckV3, CourseDeck, or match-room event header appears on its own line and the JSON message follows on another line.
- Carry event type and header timestamp forward across a short, bounded number of subsequent log lines, without scanning unrelated game/card inventory records as submissions.
- Regression fixture reproducing split-line EventSetDeckV3 request envelopes with an embedded JSON Summary.DeckId, followed by split-line match-room start and finish events.

## Changed
- Arena Player.log scanning now recognizes both existing inline event JSON and split header/payload formats found in the user's actual Arena Event Shapes diagnostic.
- The match detector continues using verified Arena ID-to-saved-deck mappings and a bounded recent queue-submission window, rather than guessing a deck from deck inventory or timestamps alone.

## Fixed
- Deck ID submissions could be skipped when the event label appeared on a separate log line from its JSON body, leading to correctly detected wins and losses with missing Arena deck IDs.
- Match-room JSON following a separate event header could similarly lose event context in the scanner.

## Removed
- Nothing removed.

No existing manually assigned match is overwritten or duplicated. This change is based on structural diagnostics from actual local Arena logs; test a new Arena match to confirm the mapping now resolves in your client.
