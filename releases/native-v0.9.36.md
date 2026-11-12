# Arcane Vault Native Windows v0.9.36 — Arena-Style Basic Land Balancing (Preview)

This release adds an Arena-inspired basic-land assistant to EDH Lab. It starts
a **new** paper Commander or Arena Brawl draft with a balanced basic-land
mix, and automatically recalculates those basics when lands are added or
removed. Only the unsaved deck editor text changes; collection, inventory,
saved deck, and Scryfall catalog remain untouched until the existing
explicit preflight and review/save flow.

## Added

- **Arena-style land balancing** in the EDH Lab deck editor, including an
  adjustable land target (20–60, initially **36**) and a manual
  **Balance now** action.
- **Auto-balance basics** defaults **on for new unsaved EDH drafts**, but
  **off for existing saved decks** to prevent surprising edits.
- Automatic even land distribution by commander color identity:
  - Mono-color: recommended basics in that color.
  - Two to five colors: initially even split across the commander's colors.
  - Colorless: Wastes.
- When a legal dual, tri-color or any-color land is added, it consumes **one
  land slot**, contributes approximate color access, and reduces/rebalances
  managed basic counts. Lands that only make colorless mana still consume one
  land slot but do not inflate colored access.
- Preexisting snow-covered lands, fetch/utility lands and nonbasic lands
  remain untouched by balancing. The assistant counts them in the land total
  where locally identified as lands.
- A compact land-base summary with planned basics, estimated colored
  sources and notes about limited production knowledge.
- Live **Deck Blueprint** land guidance now follows your adjustable land
  target instead of always displaying 36.

## Changed

- Auto-balancing is skipped while decklist input is incomplete or contains
  names with no eligible local catalog match. The assistant never silently
  drops an unknown line or guesses it was a nonland.
- Suggested basic-land counts are capped by remaining room in the
  **99-card mainboard**. Adding nonland cards can reduce the number of
  auto-managed basics when the mainboard is already full.
- A dedicated toggle lets you disable future automatic adjustments while
  retaining current land counts; you can rebalance manually later.

## Fixed

- An added two-color land no longer leaves the automatic basic-land count
  unchanged: a dual counts as **one** land in the land target.
- Generic any-color lands are approximated as one shared source across the
  commander's eligible colors, not one full source for each color.

## Removed

- Nothing. No migrations, collection ownership edits, saved deck rewrites,
  printing changes, or changes to the native updater.

## Accuracy limits

- This is a *source-balancing baseline*, not an exact copy of Magic Arena's
  proprietary land suggestion algorithm.
- The source split is initially even and dynamically accounts for recognized
  lands, **not** the colored mana pips in the deck's casting costs.
  The native Scryfall cards table currently does not store the full mana-cost
  field needed for reliable colored-pip demand modeling.
- Fetchlands, conditional mana producers, restrictive lands and spells that
  make mana are not fully modeled. The assistant reports approximate access
  rather than claiming optimal mana or card-power ratings.
- Scryfall legality and color identity come from the local cached catalog;
  this does not guarantee live tournament legality.
- Auto-inserted basic land names are *not* treated as owned cards; inventory
  shortages remain visible, especially for physical Commander.
- No automatic purchase/crafting, alternate printing ownership assignment,
  deck save, or nonbasic deletion is performed.

## Local acceptance checklist

1. Update v0.9.35 to v0.9.36 through **Native Updates**. Confirm version and
   open a **new** two-color commander draft. It should start with 18/18 basics
   for a 36-land target (or the equivalent for the chosen color identity).
2. Add a locally legal two-color nonbasic land from the card picker. Confirm
   the planned total stays at **36**, and one basic is removed/rebalanced.
3. Add a third-color or any-color land where legal; verify the land counts
   and the approximate color-availability summary update.
4. Add a utility land or snow-covered land; verify it counts toward the
   land total but the chosen card is never deleted or rewritten.
5. Expand **Arena-style land balancing**. Change the target (for example,
   36 → 38), disable automatic updates, add a land and use **Balance now**.
6. Reopen an existing saved Commander/Brawl deck with **Edit in EDH Lab**.
   Confirm **Auto-balance basics** is initially off and the saved list is
   unchanged until you deliberately enable or apply it.
7. Enter an unknown/malformed card line; auto-balancing must pause rather
   than removing or normalizing the unknown line.
8. Save a test deck only after explicit review; verify revisions, native
   backup integrity, original inventory counts and local catalog bytes.
9. Repeat in Arena Brawl and with a colorless commander (Wastes baseline).

**Release gates:** Windows build, native WPF build, regression tests,
checksummed portable ZIP and updater compatibility. User-device acceptance
is still required before 1.0.
