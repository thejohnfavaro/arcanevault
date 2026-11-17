# Arcane Vault Native Windows v0.9.139 (Preview)

## Added
- Regression checks for Arena inventory versus active deck submissions and 24-hour log timestamps.
- Regression checks for filling missing deck IDs on existing unassigned match records.

## Changed
- Extend the freshness window for a verified Arena deck submission when associating it with a later match.
- Recognize additional timestamp formats emitted by Arena's player logs.

## Fixed
- Prevent multi-deck CourseDeck inventory responses from being treated as the selected matchmaking deck.
- When the same Arena match is seen again with an exact deck ID, fill in the previously missing ID without changing the recorded win/loss or game counts.
- Preserve manually confirmed deck assignments when later automatic evidence is encountered.

## Removed
- Unsafe first-deck attribution from multi-deck CourseDeck inventory responses.

> This is a preview build. Automatic association depends on Arena logging an explicit deck selection and may remain unassigned when the log has insufficient trustworthy evidence. Existing manual choices are protected.
