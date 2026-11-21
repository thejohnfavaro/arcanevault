# Arcane Vault Native Windows v0.9.177 (Preview)

## Added
- Explicit `[[card:Exact Card Name]]` markup protocol in AI Deck Review prompts to distinguish intentional card references from ordinary words.

## Changed
- AI Review parses and verifies only explicitly marked card references against the local catalog, retaining native Markdown presentation and card detail previews.
- Markers are hidden from the rendered review; verified references become gold links and unverified references remain readable, noninteractive text.

## Fixed
- Stops automatic linking of ordinary format and gameplay terminology, such as Brawl, that happens to match a card name.
- Preserves full-name linking for marked modal double-faced card faces such as Fell the Profane.

## Removed
- Implicit linkification of every matching card-name word in arbitrary AI review prose.

## Verification
- Run Windows CI and test live AI reviews containing ordinary words that are also card names and explicitly marked card references before treating UI behavior as verified.
