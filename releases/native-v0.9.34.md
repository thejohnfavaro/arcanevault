# Arcane Vault Native Windows v0.9.34 — EDH Lab Saved-Deck Integration (Preview)

This release makes collection-aware EDH Lab tools available when editing an
existing saved Commander or Arena Brawl deck, without moving or duplicating it
out of the protected native Deck Library.

## Added

- **Edit in EDH Lab** action alongside **Edit selected deck** in the native Deck Library.
- Existing Commander and Arena Brawl decks now reopen with the EDH Lab role
  hints, recorded/missing card filters, Blueprint targets, and Browse next role.
- Format-specific saved commander verification against the read-only local
  Scryfall catalog before launching the specialized editing mode.
- Regression tests for saved-deck format detection and exact commander identity,
  including banned leaders, unavailable Arena cards, partial names and
  unsupported paper Planeswalkers.

## Changed

- The specialized editor retains an existing deck's exact saved name, mainboard,
  Commander, format, folder and Favorite flag on opening. New drafts still use
  their EDH Lab defaults.
- EDH Lab's saved-deck action is enabled only when an editable Commander or
  Arena Brawl deck has a commander and a local Scryfall catalog is present.
- Saved decks still use the SAME native deck preflight, explicit save
  confirmation, original deck identity, revision history and recovery backup.
- Normal **Edit selected deck** remains available independently of the EDH Lab
  catalog-verification requirements.

## Fixed

- Previously, reopening an EDH Lab-created deck in Deck Library lost the
  specialized candidate filters and role blueprint. The dedicated action now
  restores those tools for eligible saved decks.
- Ineligible, banned or unavailable saved commanders do not silently receive
  a guessed color identity for card suggestions.

## Removed

- Nothing. No new collection database, copying of decks, changes in ownership,
  schema migration, printing modifications, or changes to the native updater.

## Local acceptance checklist

1. Update from v0.9.33 through **Native Updates**, and confirm v0.9.34 starts.
2. Select an existing editable Commander/Brawl deck in **Deck Library**.
3. Choose **Edit in EDH Lab**. Confirm the saved name, favorite, folder and
   mainboard are preserved, and the Blueprint / recorded-only / role tools
   appear with the correct physical or Arena pool.
4. Change a card and cancel without saving; confirm the original saved deck and
   revision history did not change.
5. Reopen with **Edit in EDH Lab**, edit and save through the ordinary
   preflight. Confirm the existing deck ID, folder, favorite, and previous
   revision remain intact.
6. Try a 60-card saved deck, banned or unmatched Commander, missing catalog,
   and read-only malformed deck. The special mode should be unavailable or
   refuse safely; the ordinary editor remains usable where permitted.
7. Verify physical and Arena owned quantities, printings, and native recovery
   backups remain intact.

**Limits:** Card-role hints, targets and local legality are advisory. The
snapshot may not reflect live banlist changes, and card-name holdings do not
verify exact physical printings. Partner/background and specialized commander
cases still need dedicated support. Run local acceptance before 1.0 signoff.
