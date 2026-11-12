# Arcane Vault Native Windows v0.9.50 — Deck Health & Strategy Analysis (Preview)

Builds on the established v0.9.49 EDH Lab by introducing explainable, collection-aware deck health checkpoints. No ungrounded numerical power levels or win-rate predictions.

## Added

- New **Deck health — strategy and coverage review** panel in **EDH Lab → Improve**, using the existing bounded vertical-scrolling layout.
- Eight locally derived coverage areas: **Lands, Ramp, Card Draw, Removal, Board Wipes, Protection, Early Plays (MV 0–2), Commander Mechanic Signals**.
- Each selected area explains what the detected role *does and does not* measure, the count compared with a *suggested starting target* where applicable, and an eligible owned candidate if one exists.
- **Suggested next review** follows the transient strategy priority selected above the panel, with additional fast-start and commander-synergy guidance.
- **Inspect owned candidate in Cards** opens the existing manual picker, only after rechecking the current unsaved draft, copy allowance, selected format, and recorded inventory.
- Reporting distinguishes **partial drafts**, **missing or ineligible catalog cards**, **absent Oracle metadata**, **missing mana-value metadata**, and **substantial lists**.
- Regression scenarios for partial decks, paper/Arena isolation, malformed quantities, commander-specific focus, unavailable mana values, mature high-cost lists, WPF panel bindings and revalidation.

## Changed

- The existing deck blueprint/role estimates and MV optimization are composed into one more readable review rather than pretending to produce a unified competitive grade.
- The new health report refreshes when mainboard entries, custom land target or strategy focus change.
- Role counts can overlap, and statuses are framed as advisory starting points rather than Commander legality requirements or hard deck-building prescriptions.

## Fixed

- Invalid/ineligible draft names now explicitly pause actionable deck-health browsing.
- The owned-candidate navigation does not reuse a stale suggestion after an intervening draft change.
- Missing MV metadata never produces a made-up early-game curve diagnosis.

## Removed

- Nothing. The existing optimizer, mana tuning, Swap Lab, 2-card and 3-card synergy inspectors, collections, saved decks, updater and review/save flow are retained.

## Accuracy and safety

- This is **not a power score, deck legality certificate, card strength rating, win-rate forecast, or complete resilience assessment**.
- Role hints are cached text heuristics and can miss counterspells, unusual interaction, recurring draw engines, prevention, or other real effects.
- Color identity, format, Arena availability, and recorded ownership come only from the already filtered local snapshot. Recorded by card name does not verify the physical printing or cosmetic version.
- Suggestions never add, remove, craft, purchase, or save cards. Deck editing remains explicit and protected by existing preflight validation.

## Local acceptance

1. Update to v0.9.50 using **Native Updates → Check for updates**.
2. Open **EDH Lab → Improve → Deck health — strategy and coverage review**. Observe the eight areas and confidence caveats.
3. Change **Strategy Priority** between Faster starts, Card advantage, Interaction & protection, and Commander synergy. The recommended next review should change when relevant cards are available.
4. Select Draw, Removal, Protection, Early plays, and Commander signals. Confirm counts/limitations and any owned example appear without altering the mainboard.
5. Click **Inspect owned candidate in Cards** when enabled. Confirm the usual Cards tab opens filtered to the correct locally legal recorded card, without automatically adding it.
6. Try a sparse new draft. It should show *partial deck* instead of a final power assessment; missing MV metadata should not imply an exact curve.
7. Temporarily enter an ineligible/unrecognized card. The analysis should pause browse controls, then recover after correction.
8. Repeat with Arena Brawl and confirm no physical-only copy or unavailable Arena printing is described as an owned Arena option.
9. Resize and expand multiple Improve sections; verify scrolling and the current collection and saved deck remain intact.

Preview, not 1.0. Publish only after WPF/native regression suite, Windows build, checksum ZIP, and actual release creation succeed.
