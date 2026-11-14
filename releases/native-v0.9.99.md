# Arcane Vault Native Windows v0.9.99 — deck printing and mainboard order

## Added
- Implicit Commander reference as the first group in the visual EDH Lab mainboard.

## Changed
- Default visual group order: Commander, Creatures, Instants, Sorceries, Artifacts, Enchantments, remaining types and unverified cards, then Lands.
- Choosing a printing immediately updates the unsaved deck-specific artwork preference; pressing Review and save commits it as a saved deck revision.

## Fixed
- Printing selection previously appeared unchanged in card preview and could result in a no-changes save when the user did not click a separate apply button.
- Both embedded EDH Lab and standalone deck editor now use the same selection behavior.

## Removed
- No existing features removed.

## Safety
- Commander is a read-only reference to the Commander field and is not added to draft mainboard entries or counted twice.
- Changing a display printing does not change physical ownership or proxies; deck changes still require review and confirmation.
