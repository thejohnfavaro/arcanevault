# Arcane Vault Native Windows v0.9.82 — Land archetype labels

## Added
- Mana & Roles → Land Discovery identifies **Shock**, **Tap**, **Conditional tap**, **Fast**, **Slow**, **Check**, **Reveal**, **Bond**, **Pain**, **Fetch**, **Bounce**, **Filter**, **Triome-style**, **Rainbow**, **Basic**, **Typed dual**, and **Utility / other** lands from local type and Oracle-text evidence.
- Each row shows an easy-to-scan land-category label alongside its entry requirement; the selected land's explanation includes the classification.
- Search now finds land archetype names and entry phrases in addition to card names and mana access.
- Regression tests exercise common cycles, both old and modern Shock wording, and read-only classification.

## Changed
- Land discovery is more transparent about how lands enter play and what conditions apply, without changing the collection-aware mana coverage ranking.
- Printed Oracle text and actual format/identity legalities remain the source of truth for applying a suggestion; category labels are conservative hints.

## Fixed
- Older and current Oracle phrasings of Shock lands are both recognized, including the "pay 2 life; otherwise tapped" wording.
- Fetchlands are classified by sacrificed search abilities rather than assumed direct mana colors.

## Removed
- Generic unlabeled land suggestions when a recognizable, evidence-backed mana-land archetype can be shown.

## Safety
- Entirely read-only until an explicit user Add / double-click. No automatic deck replacement, ownership, proxy, price, or collection data changes.
- Special or updated Oracle wordings may still appear as **Utility / other land**; inspect card rules before adding.
