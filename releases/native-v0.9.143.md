# Arcane Vault Native Windows v0.9.143 (Preview)

## Added
- Stream Deck and Xeneon Edge regression checks for explicitly assigned Arena match rows with unmapped Arena IDs.
- Centralized, read-only saved-deck attribution rules for exported W/L totals.

## Changed
- Exported match statistics now respect a unique saved-deck name when the match has an explicitly confirmed manual or Arena-generated deck assignment, even if its Arena deck ID has no independent verified mapping.
- Deduplicate match IDs before assignment, matching the native Match History ordering and preventing stale legacy copies from superseding authoritative rows.

## Fixed
- Match History could show correctly assigned Borg wins and losses while the Stream Deck/Xeneon Edge showed only the smaller subset with independently mapped IDs.
- Conflicting and ambiguous Arena deck IDs remain blocked; valid native deck IDs and exact verified Arena mappings retain precedence.

## Removed
- The blanket rule that discarded every assigned match with an unmapped nonempty Arena ID.

> Exported win/loss totals include only stored confirmed match results. This update does not modify user matches, invent outcomes, or change the compatible `streamdeck.json` contract. Windows-side Match History remains the authority.
