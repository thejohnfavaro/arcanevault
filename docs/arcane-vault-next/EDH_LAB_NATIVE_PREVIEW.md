# EDH Lab — native commander browser and collection-aware draft

**State:** Native Windows preview. Commander discovery and collection-aware
Draft in Deck Studio shipped in the v0.9.31 preview; the **role blueprint
follow-up is proposed here**, pending CI and local acceptance testing.

## Commander discovery

- Browse eligible commanders from a locally synced Scryfall catalog by format:
  paper Commander or Arena Brawl.
- Search by name and Oracle text; filter by color identity, owned status and
  supported commander rules; choose **Surprise Me!** from filtered results.
- Choose **Build with selected commander** to open a fresh, unsaved native Deck
  Studio draft with that commander and format selected.

## Collection-aware drafting

- Candidates are read from an isolated **read-only** SQLite catalog snapshot.
  Format legality, Arena availability and commander color identity narrow the
  candidate list. Ownership is read separately from the verified physical or
  Arena collection — never inferred from catalog presence.
- Default candidate browsing shows recorded cards. Unchecking **Recorded in my
  inventory only** adds locally eligible missing cards without claiming ownership.
- Search by card name or select role hints: lands, ramp, draw, removal, wipes,
  tutors, protection and tokens. Ordinary mana-producing lands are not labeled
  ramp just because they tap for mana.
- The **Deck Blueprint** shows live mainboard progress out of 99 (commander is
  separate), rough roles, and a count of draft copies not matched to an eligible
  local catalog card.
- **Browse next role** chooses the largest proportional shortfall against a
  simple starting guideline (36 lands, 10 ramp, 10 draw, 8 removal, 2 wipes,
  2 protection), clears prior search text and shows recorded candidate cards.
  Uncheck recorded-only to see missing candidates. It never inserts cards.
- Role hints may overlap and are text heuristics, not a statement of optimal
  construction, card strength, or tournament legality. The guidelines are
  optional and do not restrict deck saves.

## Storage / safety contract

- Everything remains unsaved until the user chooses the existing **Review and
  save deck** flow. NativeDeckPreflight runs local legality and copy checks.
- Saving remains in NativeWorkspaceSafety.SaveDeck with expected-backup SHA,
  recovery backup and deck revision handling. EDH Lab does not change owned
  cards, printings, catalog rows, or existing saved decks merely by browsing.
- Ownership is currently at the card-name level. Exact physical printing
  verification and real-time banlist certification are **not** implemented.
- Paired commanders, specialized commander eligibility and other unusual deck
  rules remain future work; manually imported cards still receive preflight
  warnings instead of silent removal.

## Test pass before release

1. Open a verified native collection and a synced catalog in Windows.
2. Browse commanders for both formats; select a commander and start a draft.
3. Type or add a card; confirm the blueprint counts refresh with no lag.
4. Verify an ordinary basic land counts as land but **not** ramp, and a mana
   rock counts as ramp. Test missing and wrong-identity card names.
5. Select **Browse next role** and verify search/role/recorded filters,
   including when no recorded matching cards exist.
6. Save and reopen a deck, inspect backup + revisions, and verify physical
   and Arena ownership remain unchanged.
7. Confirm the native test fixture, WPF build and on-device startup pass.

Next milestones: manual role target tuning; synergy/archetype tags; better mana-base
advice; fully legal specialty commander partnerships; exact owned-printing support.
