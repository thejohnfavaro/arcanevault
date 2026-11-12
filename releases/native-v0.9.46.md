# Arcane Vault Native Windows v0.9.46 — Synergy Package Coverage and Mechanic Focus (Preview)

Builds on the user-accepted v0.9.45 commander synergy packages by making their next steps clearer and the panel easier to navigate.

## Added

- **Mechanic** selector in EDH Lab → Improve → Commander synergy packages. Choose **All mechanics** or one recognized commander-text mechanic, including discovered creature-typal signals.
- Package-level **in-deck role coverage** for potential enablers, payoffs, and other supporting matches, computed from all eligible current draft matches, not just the five displayed examples.
- A conservative **Next review** prompt for each mechanic that points to potentially missing enabler/payoff signals and prioritizes recorded owned options when present.
- Regression checks that multiple token creators alone do not imply a complete payoff engine, and that coverage is measured from the current deck rather than from missing recommendations.

## Changed

- Package summaries now show a readable per-mechanic section with in-deck counts, owned/missing example counts, role coverage and an explanatory next step.
- Changing the mechanic selection changes only the displayed package subset, keeping card preview and editor/collection state separate.
- A selected mechanic is retained through subsequent mainboard edits if still recognized; changing commander or catalog context safely falls back to All mechanics.

## Fixed

- Avoid implying that an artifact/token text match by itself means an actual working combo. Role labels are explicitly **potential** signals inferred from cached Oracle text.
- Longer lists of recognized mechanics no longer require reading every group just to inspect one specific mechanic.

## Removed

- Nothing. The existing deck editing, native updates, imports, safe reconciliation, mana planning, strategy priorities and swap verification remain intact.

## Accuracy and safety

- Coverage is heuristic and can miss actual combos or mark a mere text coincidence as an enabler/payoff. It does **not** certify competitive strength, trigger relationships or comprehensive deck synergy.
- Recommendations are filtered through the locally eligible physical Commander/Arena Brawl catalog. No new network calls, data schema changes, collection writes or automatic deck edits.
- The final Review and Save preflight still governs actual modifications.

## Local acceptance

1. Update to native v0.9.46 through Native Updates.
2. Open EDH Lab → Improve → Commander synergy packages, and use the new mechanic selector.
3. With an artifact/token commander, confirm choosing Tokens limits the list and status to Tokens; All mechanics restores both.
4. Confirm the displayed role counts change when you manually add or remove cards, and the next-review message does not claim a guaranteed combo.
5. Select a suggestion and verify it updates the right-side read-only preview without changing the deck or collection.
6. Resize the app, open additional Improve sections and verify that vertical scrolling still reaches the controls.
7. Check Physical vs Arena Brawl pools for correct legalities and owned counts, then verify existing saved deck data is intact.

Release requires passing Windows and native .NET 10 regressions, checksummed portable packaging and local acceptance. This is a preview, not 1.0.
