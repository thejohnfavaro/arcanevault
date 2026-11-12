# Arcane Vault Native Windows v0.9.40 — EDH Lab Role-Aware Swap Proposals (Preview)

This release advances the v0.9.39 owned-card deck optimization into
**specific, explainable one-for-one proposals** in the existing native
EDH Lab Deck Studio. A swap is never automatic: selection previews it,
explicit confirmation changes only the unsaved editor text, and the
separate protected Review and Save is still required to persist the deck.

## Added

- **Swap Lab — review one-for-one edits** section below the existing
  collection-aware deck optimization panel in EDH Lab.
- A bounded list of up to eight proposed **cut → replacement** pairs.
  Candidates must already be recorded as owned in the selected
  Paper Commander or Arena Brawl inventory, locally eligible by color
  and format, and have at least one unused recorded copy and remaining
  local copy allowance.
- Proposals consider expensive nonland cards at mana value 4+ against
  locally recorded, eligible nonland replacements at mana value 0–3.
  Each pair must reduce mana value by at least 2.
- **Critical role preservation:** before suggesting a cut, check the
  blueprint's Ramp, Card Draw, Removal, Board Wipe and Protection
  targets. If a role is currently at or below its suggested minimum,
  a proposed replacement must retain that role. Roles already above
  their target can give up one contributor.
- **Commander synergy preservation:** if the card being cut has
  recognized commander rules-text mechanic matches, do not suggest
  replacing it with a card scoring lower on those same coarse signals.
- Each proposal displays its exact two card names and mana values.
  Selecting it previews its role retention, new role-gap coverage,
  synergy-score change and remaining recorded replacement quantity.
- **Apply selected swap to unsaved draft…** requires a clear Yes/No
  confirmation. The proposal is rechecked against the **current**
  draft, current role targets, local format eligibility, used copies
  and same locally filtered catalog before making one atomic
  text-box assignment. Stale proposals are refused, not guessed.
- Existing native save, revision backups, original deck identity and
  rollback remain the only way to persist changes.

## Changed

- This feature waits until at least 25 known nonland copies are present,
  preventing speculative cuts while a new draft is barely started.
- Suggestions are capped and calculated from the in-memory local
  catalog for responsive editor use; there is no network request.
- Deck edits immediately refresh the proposal list. A replacement
  whose only recorded singleton copy has been used is not suggested
  again until it becomes available.

## Fixed

- Expensive cards with scarce board-wipe, removal, protection, ramp or
  draw coverage can no longer be suggested as generic curve cuts
  without maintaining their necessary tracked roles.
- A locally banned, off-identity, Arena-unavailable, unrecorded or
  fully exhausted singleton will not appear as a replacement.
- The preview remains read-only; merely selecting a swap cannot
  modify the unsaved deck or collection.

## Removed

- Nothing. No schema/ownership changes, automatic land edits beyond
  the existing opt-in feature, card crafting, deck revisions rewrites,
  automatic saves, or updater changes.

## Important scope and limitations

- These are **heuristic proposals, not guaranteed improvements**. The
  role classifiers and commander-text matching do not understand every
  combo, win condition, strategic payoff, matchup or deck bracket.
  A five-mana payoff may deserve its slot even if a two-mana card
  superficially looks more efficient.
- Roles may overlap; counts are advisory targets rather than hard
  Magic rules. No mass deletion or replacement is performed.
- Candidates are recorded by card name, not by exact physical
  printing verification; actual saved legality and required quantities
  remain subject to native preflight.
- A sufficiently developed draft is required; incomplete, unknown,
  older catalog lacking mana values, or oversized deck data pause
  swaps instead of fabricating an upgrade.
- Alternative replacements may exist outside the bounded top local
  pool. A lack of proposed swaps does **not** imply a deck is optimal.
- If Auto-balance basics is enabled, it may rebalance basic lands
  after the user explicitly confirms a spell swap. The confirmation
  clearly discloses this possibility.

## Local acceptance checklist

1. Update from native v0.9.39 to **v0.9.40** via Native Updates,
   preserving collection and current deck backups.
2. Open a Commander deck in **Deck Library → Edit in EDH Lab**
   and expand **Swap Lab — review one-for-one edits**.
3. Verify the candidates appear only when a sufficient number of
   known nonland spells is present. Sparse drafts should show a
   clear reason rather than invented upgrades.
4. Select a proposal. The card names, mana values, preserved roles,
   new role-gap coverage, synergy scores and recorded copy count
   should appear in a preview **without changing any deck cards**.
5. Click **Apply selected swap to unsaved draft…**, then choose
   **No**: confirm the deck remains exactly as before.
6. Apply a nonessential suggestion choosing **Yes** in a test
   deck. Exactly one card should be removed and one added, preserving
   mainboard copy count. If basic auto-balance is on, review its
   resulting basics too.
7. Cancel without saving. The original saved deck, favorites, folder,
   ID, revision history, all collections and backup files remain intact.
8. Reopen, apply a swap and use ordinary **Review and save deck**:
   confirm protected preflight, existing deck identity and history
   append continue to work.
9. Add the proposed replacement separately before attempting the
   swap again. The stale proposal must be refused or disappear.
10. Try a scarce Board Wipe / Card Draw / Ramp card: a proposed
    replacement must preserve its critical role unless you
    already have more than the blueprint's suggested target.
11. Repeat in Arena Brawl. Paper-only, unavailable, off-color,
    banned and unowned options must not leak into proposals.

Release checks: Windows build, native .NET 10 WPF/regression,
checksummed portable release ZIP, then local device acceptance.
