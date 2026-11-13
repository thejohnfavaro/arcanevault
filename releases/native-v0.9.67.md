# Arcane Vault Native Windows v0.9.67 — Shortages to EDH Lab replacements

## Added

- **Review shortages in EDH Lab** directly from a saved deck's persistent Deck Library inspector, enabled only for an editable Commander or Arena Brawl deck with recorded-copy shortages and a synced local catalog.
- The handoff uses the **existing verified saved commander / format / identity** gate and opens the existing unsaved Deck Studio draft on **EDH Lab → Improve**. The selected missing mainboard name is preferred; otherwise the first missing mainboard card is selected.
- A **Resolve missing cards** section in Improve lists all outstanding name-level gaps, including commander-only shortages, separately for the selected format's physical or Arena inventory.
- Select a missing **mainboard** card to see up to eight unused, locally format-and-identity-eligible recorded alternatives. Conservative suggestions preserve every recognized source role hint and land/nonland classification; missing catalog data does not invent equivalence.
- Select an alternative to read its shared role hints and inventory counts. **Replace one copy in unsaved draft** requires a Yes/No confirmation and revalidates both the live shortage and the eligible alternative before making exactly one -1/+1 edit to unsaved mainboard text. Saving remains a separate user action with existing preflight and recovery.
- Regression cases for pool separation, source with no recognized roles, exhausted candidates, missing metadata, per-draft replacement guards and handoff wiring.

## Changed

- The saved deck inspector now offers an actionable next step when name-level shortages exist instead of leaving the user to navigate manually to Edit in EDH Lab.
- The EDH Improve panel refreshes its missing-copy list as the **unsaved draft** changes, using the existing read-only physical/Arena coverage checker. The existing Swap Lab, optimizer, deck health and role browser remain available unchanged.

## Fixed

- Previously, going from a specific saved-deck shortage to EDH optimization required manually opening the EDH Lab editor and finding the missing card again.
- Generic Swap Lab's early-curve suggestions do not reliably target missing inventory copies; the new section handles that distinct job explicitly.

## Removed

- No saved-deck records, inventory, printing choices, commander, revision history, card catalog, backups or recoveries are removed.
- No purchase, automatic deck edit, inferred card equivalence, inventory reservation or automatic save. Role-based recommendations are heuristics, not guarantees of strategic equivalence or legality.

## Windows acceptance

1. Open a saved **Commander** deck with missing Physical copies. In Deck Library inspector, select a missing card and click **Review shortages in EDH Lab**. Verify the existing saved deck opens on **Improve**, with that shortage highlighted if still missing.
2. Inspect the resulting alternatives. The list should contain only *unused recorded* cards from the verified format/commander color-identity catalog, with shared role hints. Unknown effects need manual review; no substitutions are performed on launch or selection.
3. Choose one alternative and click **Replace one copy in unsaved draft**. Cancel the confirmation: nothing changes. Repeat and confirm: one mainboard copy is replaced, draft summary and shortages update, but collection counts and saved deck remain unchanged.
4. Select **Review and save** only when satisfied, and confirm native preflight, previous revisions and collection recovery protections are unchanged.
5. Open a **Brawl** saved deck with Arena-only shortages. Verify its EDH suggestions do not borrow Physical inventory or suggest Arena-banned/unavailable cards.
6. For a missing commander with no separate mainboard line, verify the view identifies a **commander-only shortage** and does not offer to replace it as a mainboard card.
7. Verify unsupported imported decks, non-EDH formats, decks with no shortages and missing local Scryfall catalog do not present an enabled shortcut that promises unavailable advice.
8. Test with no local metadata for the missing name, exhausted owned candidates and rapid edits. The interface must show honest fallbacks rather than asserting a match.
9. Confirm saved-deck inspector, standard **Edit in EDH Lab**, ordinary **Swap Lab**, native catalog filters and the updater still work.

**Scope:** Native Windows preview v0.9.67. No collection or saved-deck write occurs unless the user subsequently chooses the existing protected Review and save path. Native CI and on-device acceptance required before shipping.
