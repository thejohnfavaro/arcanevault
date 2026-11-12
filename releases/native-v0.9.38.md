# Arcane Vault Native Windows v0.9.38 — EDH Lab Mana Curve & Casting Consistency (Preview)

This release extends EDH Lab's collection-aware commander builder with
**live mana-curve and early land-drop consistency estimates** using the
locally cached card mana values and the land planner added in v0.9.36–37.

## Added

- **Mana curve & casting consistency** panel in new EDH Lab drafts and
  saved Commander/Arena Brawl decks reopened with **Edit in EDH Lab**.
- A 0, 1, 2, 3, 4, 5, 6+ **nonland mana-value histogram**, known nonland
  copy count, average mana value, and indication of missing/unverified
  local card information.
- Exact **hypergeometric land-drop estimates** (under explicitly
  stated assumptions) for:
  - At least 2 lands by turn 2.
  - At least 3 lands by turn 3.
  - At least 4 lands by turn 4.
  - At least 5 lands by turn 5.
- Separate, **conservative colored-source-pressure warnings** for strict
  repeated mana symbols (such as {U}{U} on a turn-two spell), showing
  marginal approximate source-draw likelihood rather than wrongly
  describing it as full spell-casting probability.
- A compact curve headline and a collapsible detailed panel. It updates
  from the unsaved mainboard on changes, without any catalog network call.
- Full 99-card mainboards use **actual current lands** and colored sources
  for estimates. Incomplete drafts use **a projected hypothetical 99-card
  deck** with the planner's suggested land total and clearly disclose that
  their displayed nonland curve is partial.
- Backward-compatible read-only mana-value extraction from existing
  locally synced catalogs, including older minimal catalog fixtures.
- Tests for a 36-land/99-card opening sample, source-risk warnings,
  curve quantity weighting, complete versus unfinished decks, empty
  or legacy mana-cost metadata, and malformed/missing card names.

## Changed

- The mana plan now reports estimated colored sources both as proposed
  after balancing and **actually present** in the current mainboard.
  That distinction prevents complete saved decks from being judged using
  an unsaved proposed basic-land redistribution.
- The existing manual/automatic basic-land balancing and pip weighting,
  Scryfall sync, inventory accounting, color identity and format
  legality, and protected native save/revision paths are unchanged.

## Fixed

- Live curve feedback no longer requires a complete saved deck before
  offering useful projected land-drop probabilities.
- Full-size decks no longer estimate land odds from a hypothetical
  land target when their actual land count differs.
- Missing catalog metadata suppresses probability claims rather than
  silently pretending unknown cards are spells or lands.

## Removed

- Nothing. No collection schema changes, migrations, deck rewrites,
  printing edits or automatic card additions.

## Probability assumptions / limits

- Land odds are exact **only for the simplified stated sampling model**:
  uniformly shuffled 99-card mainboard, seven starting cards, no
  mulligans, one draw per turn starting on turn two, natural land drops.
- A probability of drawing three lands is **not** the probability of
  *casting* a three-mana spell; it omits tapped-land timing, multiple
  colored requirements, acceleration, rituals, tutors, draw effects
  and alternate costs.
- Color source quantities are intentionally approximate. Multicolor
  production is distributed fractionally in the planning model, and
  the per-color warning computes only marginal access to a single
  color, **not joint odds of drawing all necessary colors**.
- Spells with hybrid/alternate costs are not treated as requiring every
  possible colored pip simultaneously; the strict-color warning only
  counts literal `{W}`, `{U}`, `{B}`, `{R}`, `{G}` symbols.
- Front-face mana values and Scryfall's unified mana value for
  split/MDFC layouts can be imperfect proxies for the spell face
  actually cast. Curve data is advisory.
- The curve is of cards currently in the draft, not a projection of
  spells that have not yet been added.

## Local acceptance checklist

1. Update v0.9.37 to **v0.9.38** through **Native Updates**; no new
   collection or Scryfall sync is required if you already ran Full Sync
   after installing v0.9.37.
2. Open EDH Lab and a new two-color Commander deck. Inspect **Mana curve
   & casting consistency** under the land-balancing section.
3. With no spells and 36 suggested lands, verify it reports **projected**
   odds from a hypothetical 99-card deck; it does not call the deck
   complete or invent spell mana values.
4. Add some 1-, 2-, 3-, 4- and 5-mana spells. Verify the histogram
   buckets and weighted average respond without saving the deck.
5. Change the land target from 36 to 40; projected land-drop odds
   should increase and reflect the new land plan.
6. Add repeated colored-pip spells in a poorly supported color;
   verify any warning explicitly says the odds are **marginal**, not
   a guaranteed casting percentage.
7. Reopen a complete 99-card saved deck with **Edit in EDH Lab**.
   Verify it uses its existing lands for estimates, without silently
   balancing or changing the deck.
8. Enter an unknown/ineligible card and malformed mainboard text;
   estimates should pause rather than assuming card types.
9. Check a mono-color and colorless commander plus Arena Brawl,
   ensuring independent format legality and ownership counts.
10. Cancel without saving; compare collection quantities, saved deck,
    revision history, and native backups before and after.

**Release gate:** native Windows CI, WPF compile/tests, SHA-256 verified
release ZIP and real-device acceptance before 1.0.
