# Arcane Vault Native Windows v0.9.32 — EDH Lab Deck Blueprint (Preview)

Builds on native v0.9.31 collection-aware Commander / Arena Brawl drafting. This is an **unsaved, advisory deck-composition tool** using your verified native collection and locally synced Scryfall catalog.

## Added

- **Deck Blueprint** panel in an EDH Lab Deck Studio draft. See live progress toward a 99-card mainboard (commander counted separately) and basic role coverage as you edit.
- Advisory role totals for lands (36), ramp (10), card draw (10), removal (8), board wipes (2), and protection (2). Cards may count toward multiple roles.
- **Browse next role** action that filters eligible, recorded cards for the largest proportional gap. You can still opt to browse unowned suggestions via the existing inventory-only toggle.
- Counts of draft copies that lack a matching locally eligible card in the current Scryfall catalog.
- Automated checks for role coverage, card quantities, unknown/ineligible cards, safe catalog reading and UI wiring.

## Changed

- Improved the ramp role heuristic: a normal mana-producing land is classified as a land, not also as ramp merely because it taps for mana.
- The role browser clears an old search query when jumping to the next underrepresented role, so it shows matching suggestions immediately.
- The mainboard editor, deck analytics and saved Deck Library remain the existing protected native workflows.

## Fixed

- Ordinary mana-producing lands no longer artificially inflate the EDH Lab ramp estimate.
- Invalid mainboard text disables the role-gap shortcut until the draft is corrected.

## Removed

- No inventory, saved deck, catalog, printing, theme, backup, migration or updater features were removed.
- No automatic deck changes, purchases, crafting or collection modifications were added.

## Manual verification

1. From a working v0.9.31, export an independent full native collection backup.
2. In **Check for updates**, install the native v0.9.32 preview. The updater should stage a versioned executable rather than replace current application data or overwrite your prior executable.
3. Open EDH Lab, choose a Commander / Arena Brawl leader, and click **Build with selected commander**.
4. Edit the unsaved mainboard and check the Deck Blueprint refreshes with card quantities. Verify a normal basic land counts toward lands but not ramp, while Sol Ring counts toward ramp.
5. Click **Browse next role**. Confirm the role filter and recorded-only results, then uncheck inventory-only to see missing eligible suggestions. No card is added merely by browsing.
6. Test invalid / ineligible card names and malformed decklist text. Correct it, then verify estimates recover without lost card data.
7. Save a test deck via the existing review/preflight flow, reopen it in Deck Library, and confirm existing deck revision/recovery functionality is preserved.
8. Verify original physical and Arena inventory totals, printings, and the native workspace backup remain unchanged by drafting.

**Limits:** This is still a preview, not a validated optimizer or sanctioned legality oracle. Role hints use simple Oracle-text heuristics; target counts are optional guidelines, not format rules. Name-level holdings are not exact printing verification. Paired commanders and specialty Commander rules require dedicated follow-up work.

The Windows/.NET 10 native build and regression checks must pass for this versioned release. Real-device acceptance on your local collection remains outstanding.
