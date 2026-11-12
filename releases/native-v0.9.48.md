# Arcane Vault Native Windows v0.9.48 — Synergy Chain Explorer (Preview)

Builds on the on-device accepted v0.9.47 release, making its read-only interaction clues easier to inspect and more precise.

## Added

- Side-by-side, compact **Enabler** and **Payoff** card artwork in **EDH Lab → Improve → Synergy chains**.
- Separate **Inspect enabler** and **Inspect payoff** controls, wired to the existing full card-art/Oracle-text preview on the right.
- **Locate owned missing link in Cards** button, visible only for locally eligible *owned* missing links, using the existing manual deck picker. It never adds a card.
- Explicit cached Oracle-text excerpts on both sides of the selected possible interaction, alongside the mechanism explanation and owned/missing status.
- Native regression coverage for the new WPF controls, trusted artwork, Arena-only artwork filtering, and false-positive artifact mention.

## Changed

- Artifact token and creature token production detection now requires the creation and token type to appear together in a bounded clause, instead of matching unrelated words from different rules effects.
- Chain artwork uses validated catalog imagery, smaller decode size, canceled stale requests, and offline text fallback when artwork is missing. Arena artwork remains gated to Arena printings.
- Existing expandable Improve section remains collapsed by default and scrollable when several panels are open.

## Fixed

- Selecting chains quickly should not allow an earlier in-flight artwork request to overwrite the latest selection.
- A card saying it creates a token and mentioning artifacts elsewhere no longer counts as an artifact-token engine.

## Removed

- Nothing. Existing card picker, deck revisions, collection ownership, backups, import reconciliation, mana and swap features are unchanged.

## Accuracy and safety

- Interaction hints are **not guaranteed combos**, complete rules simulations, infinite loops or win-condition certifications. Cached rules text may be incomplete or contain caveats.
- Artwork is illustrative and is not evidence of a particular owned printing. The inventory gap is calculated only from the format-eligible local collection.
- Inspecting and jumping are **read-only**; adding a card remains a separate manual action using existing deck validation and save flow.

## Local acceptance

1. Update to v0.9.48 using Native Updates.
2. Open a deck with an artifact-token enabler and artifact-entry payoff in **EDH Lab → Improve → Synergy chains**.
3. Select a chain. Confirm two corresponding illustrations or named text fallbacks appear side by side.
4. Click **Inspect enabler** then **Inspect payoff** and confirm each updates the right-side preview and complete Oracle text.
5. Select several chains quickly. Verify no previous card art reappears for the newer chain.
6. On an **Owned missing link**, click **Locate owned missing link in Cards**; verify Cards opens filtered to the owned eligible name, but the deck remains unchanged.
7. Try a **Not recorded** link; the Cards jump must be disabled, with no unowned card presented as owned.
8. Repeat in Arena Brawl, verify no unavailable or banned card suggestions and no paper-only artwork is implied to be owned.
9. Expand multiple Improve sections and resize; confirm scrolling, data preservation, and separate Review & Save flow.

Release gates: successful native/WPF regression suite, Windows build, checksum package, release publication, and user acceptance. Preview, not 1.0.
