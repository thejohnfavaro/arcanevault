# Arcane Vault Native Windows v0.9.56 — Clear Inventory confirmation & catalog readability

## Added

- The confirmation dialog now keeps **Clear selected inventory** clickable, shows the required backup acknowledgement in a scrollable layout, and explains exactly which prerequisite is missing on click.
- Long card names, ownership labels and utilization roles display their complete text in hover tooltips in both card-art tiles and compact catalog rows.
- Native visual-shell regression checks for the updated clear-confirmation requirements and readable catalog labels.

## Changed

- The selected-scope phrase is validated again at click time **case-insensitively**: `Clear Physical`, `CLEAR PHYSICAL`, and `clear physical` are equally valid after trimming surrounding whitespace. The user must still select Physical/Arena/Both and explicitly acknowledge their full backup.
- Card names and role/ownership labels in card-art tiles and compact catalog rows are slightly larger and have clearer text hierarchy.
- No change to the number of results shown, image caching, virtualized rows, ownership calculations, inventory reset scope or backup behavior.

## Fixed

- The formerly compressed backup acknowledgement is reachable even on smaller windows. Typing `Clear Physical` no longer fails on capitalization, and clicking the action explains any remaining unmet safeguard instead of doing nothing.
- Short or truncated catalog labels are easier to inspect without opening Card Details.

## Removed

- Nothing. All existing catalog filters, role tags, shortlist stars, card details, CSV/Excel imports, selective reset safeguards and rollback paths remain.

## Safety and manual acceptance

1. Upgrade with **Native Updates → Check for updates** to v0.9.56. Export and verify a complete backup before using any reset.
2. Open **Collection & Deck Library → Clear inventory…**, choose Physical only, type `Clear Physical` (mixed case); click the available Clear button first and verify it reports that the backup acknowledgement is required, with no data changes.
3. Check the acknowledgement. Uncheck it again and click Clear; verify it refuses to proceed and explains why, without modifying the collection.
4. For Arena/Both, try `Clear Arena` and `clear both`. Select a different scope; previous typed phrase must not inadvertently authorize a different one.
5. Cancel without clearing, then optionally test a verified selective reset; preserve existing rollback and collection isolation checks.
6. Open the Card Catalog in **Compact list** and **Card art** modes; hover a truncated card name, role or ownership to see its full value. Verify Shortlist and double-click Card Details still work.
7. No changes to import reconcile/replace behavior or persistent inventory formats.

Windows WPF/native regression and checksummed release required; user device acceptance remains the last gate.
