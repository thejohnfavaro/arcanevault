# Arcane Vault Windows v0.9.219 (Preview)

## Added
- **Mainboard composition** in both the saved-deck editor and EDH Lab draft editor: full unsaved mainboard copy totals with counts and percentages for creatures, instants, sorceries, artifacts, enchantments, planeswalkers, other/unverified, and lands.
- **Full card details** action beside the existing +/- quantity controls in the visual Manage tab, plus double-click on a card row to open the familiar full card details and printings window.
- Regression tests for accurate unsaved copy composition, unknown card types, independent section collapse behavior, and read-only full-details navigation.

## Changed
- Composition totals always derive from the authoritative unsaved Text mainboard, not from filtered, searched or collapsed visual rows. They do not count the commander as a mainboard copy.
- Full details resolves card identity and optional (SET) collector suffix using the local catalog on a background task, then opens the existing card details view without a writable collection context.
- Details are accessible for the commander reference card as well as mainboard rows. Existing +1/-1/remove actions are unchanged.

## Fixed
- Visual mainboard previously required using a separate screen to open full rules, printings, and legality details.
- The visual mainboard now shows the composition of the complete draft even when card-type groups are collapsed.

## Removed
- Nothing. No changes to the saved deck, text representation, revisions, physical/Arena inventory, match history, artwork cache, or Edge exporter.

## Verification
- Build and run native Windows CI including EDH Lab draft and visual-shell regression suites.
- In We Are the Borg, open Manage; check land and artifact copy shares (not distinct-name percentages), collapse/expand groups and search without changing the summary.
- Select Island (FIN) 297 and open Full card details (or double-click), inspect Oracle text, legality, and local printing metadata; verify the dialog closes back to the unsaved editor and has not changed ownership or deck text.
- Build/installer and actual UI behavior require validation before treating this preview as accepted.
