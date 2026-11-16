# Arcane Vault Native Windows v0.9.129

## Added
- Native UI regression coverage that EDH Lab's recorded ownership pool remains selectable for both Arena and Physical inventories.

## Changed
- Enable the ownership-pool selector in both the embedded EDH Lab and standalone editor, including saved Commander drafts.
- Deck legality, commander color identity and Arena Brawl eligibility remain governed by deck format, independently of which collection pool is used for ownership review.

## Fixed
- EDH Lab previously locked the ownership selector and could report a card as missing from a paper Commander draft even though that name was recorded in the user's Arena inventory.
- Switching pools now uses the distinct Arena/Physical snapshots introduced in v0.9.127 rather than remaining stuck on format-derived ownership.

## Removed
- No features removed.

The release never changes inventory, saved deck lists, proxies or card ownership. If a card is present in the selected pool but excluded by the chosen format or color identity, the catalog may still exclude it as ineligible; ownership does not imply legality. Stream Deck companion v0.3 adds immediate property-inspector deck selection and a clear unselected state independently from this native release.
