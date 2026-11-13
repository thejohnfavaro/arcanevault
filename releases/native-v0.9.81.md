# Arcane Vault Native Windows v0.9.81 — Smarter land coverage

## Added
- The Mana & Roles land advisor now considers which commander colors are underrepresented by directly producing lands already in the unsaved mainboard.
- A read-only W/U/B/R/G source-count summary highlights the lowest-covered commander colors; this is a heuristic and not a mana-probability simulation.
- Land suggestions identify printed tapped-entry clauses, conditional untapped clauses, and restricted mana spending separately.

## Changed
- Suggestions prioritize gaps in the actual draft mana base, then compare color coverage, tapped-entry and restriction penalties. Real owned and paper Proxy status still take precedence over unowned choices.
- Basic land subtypes supply intrinsic mana production even if Oracle text omits the corresponding tap ability.
- Parsing of "any combination of colors" recognizes flexible production; the colors shown remain bounded by commander identity.

## Fixed
- A mana symbol in an activation cost is never counted as a color produced by that land.
- Reflecting Pool's mana remains conditional on other land production rather than being marked as unconditional rainbow mana.
- Fetchlands continue to depend on actual fetch targets and are not treated as direct producers.
- Changing commander identity invalidates cached mana-production hints for the new colors.

## Removed
- Unconditional implication that every land with multiple printed mana symbols is equally useful to the deck's current casting needs.

## Safety
- Read-only advisor; selecting a suggestion does not change deck, collection, prices, or proxy ledger. Adding a land still requires the existing explicit Add or double-click action and legality/copy-limit checks.
- "No tapped-entry clause found" does not guarantee that a land always enters untapped. Dynamic abilities and MDFC restrictions still require reviewing Oracle text.
