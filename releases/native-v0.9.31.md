# Arcane Vault Native Windows v0.9.31 — EDH Lab Deck Construction (Preview)

This release advances the native EDH Lab from commander discovery to **collection-aware deck drafting**, building on v0.9.30. It reuses the protected native Deck Studio and Deck Library workflows rather than introducing another deck database.

## Added

- **Build with selected commander** action in the native EDH Lab browser.
- A prefilled Deck Studio draft with the selected commander, Paper Commander or Arena Brawl format, and an EDH Lab folder.
- A locally indexed **eligible card picker** using the selected commander's Scryfall color identity, local format legalities and Arena-printing availability for Brawl.
- Browse recorded cards first, or uncheck **Recorded in my inventory only** to add potential cards you do not yet own.
- Format-separated Physical and Arena copy counts and explicit missing-copy status.
- Searchable role hints for ramp, card draw, removal, board wipes, tutors, lands, protection and token production.
- Live draft status highlighting card names not confirmed eligible by the local catalog; native saved-deck preflight provides deeper legality/ownership/copy-count warnings.
- Enforced in-picker singleton, basic-land and catalog-supported printed copy exceptions (such as seven allowed copies). The existing protected saved-deck preflight remains authoritative.
- Automated regression fixtures for cross-format ownership separation, missing cards, bans, color identity, role hints, allowed copies and source catalog hash preservation.

## Changed

- New EDH Lab drafts save through the **existing** Deck Library, including verified workspace SHA checks, user confirmation, revision history and recovery backups.
- The builder reads a verified native collection on opening, and its suggestions use the **exact read-only Scryfall snapshot** from the commander browser, not a possibly different catalog.
- Draft launch refuses stale commander-browser sessions if the catalog changed.
- Eligible name index is cached for the open editor session, with fast lookups and bounded result lists for responsive search.
- Existing analytics and mainboard add/remove/sort remain available in the draft editor.

## Fixed

- Prevented invalid color-identity and cached banned cards from appearing as eligible suggestions for the current commander.
- Kept unowned suggestions separate from actual inventory; adding a missing card to a draft never changes owned quantities.
- Paper Commander and Arena Brawl suggestions no longer mix their collection pools or use the other's legality/printing requirements.
- Avoided a stale catalog handoff between commander selection and deck construction.

## Removed

- No native card collections, printing identities, Deck Library entries, saved revisions, backups, Set Progress, theme settings or updater functionality were removed.
- No new database copy or migration was introduced.

## Manual verification

1. From a working v0.9.30, make an independent native full backup before testing; then install the checksummed native v0.9.31 preview.
2. Open EDH Lab, select a Commander-legal leader, then click **Build with selected commander**. Check name, commander, format and EDH Lab folder are prefilled.
3. Search recorded eligible cards; switch **Recorded in my inventory only** off and confirm missing/unowned choices become available without affecting ownership totals.
4. Test a card outside the commander's color identity; it should not appear in eligible suggestions. Verify text import/manual edits still generate review warnings instead of losing deck text.
5. Add ordinary singleton cards, multiple basic lands and a catalog-supported copy exception; check copy-limit handling.
6. Check role filters, missing-copy totals, 100-card including-commander status and mana/type/color analytics.
7. Repeat with Arena Brawl; verify independent Arena quantities and cards without Arena printings are excluded.
8. Save a test deck only after reviewing the native preflight; confirm it appears in Deck Library and reopens with the expected commander, format and cards.
9. Recheck Physical/Arena inventory quantities and existing saved decks. The workflow must not mutate collection ownership.

**Important limitations:** Local Scryfall legalities may be stale relative to current bans; name-level ownership is not proof of an exact physical printing. Partner/background combinations, every exceptional Commander rule, card-effect synergies and automated deck optimization still need their own validation. This is a preview and not a sanctioned-deck certification or complete v1.0 signoff.

The Windows build and .NET 10 compatibility/native regression workflows passed on PR #112 before merge. Final versioned release build and real-device acceptance must also pass.
