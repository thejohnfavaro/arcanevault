# Arcane Vault Native — WebView feature parity reality check

**Date:** 2026-11-11  
**Status:** **Major parity gaps confirmed. Native v0.9.5 is a development preview, not a near-finished Windows 1.0.**  
**Release blocker:** [#62](https://github.com/thejohnfavaro/arcanevault/issues/62)

## What was compared

- **Shipping Windows WebView application:** `ArcaneVault.Desktop/Assets/Arcane_Vault_MTGA_Collection_v4_2.html` (embedded in the v5.21.x Windows app) and associated native services `ArenaLogConnector.cs`, `ArenaMatchTracker.cs`, `MatchHistory.cs`, `LanCollectionTransfer.cs`, `UsbAdbTransfer.cs`, `UpdateManager.cs`, `ScryfallCatalog.cs`.
- **Native Windows app:** `ArcaneVault.Next.Desktop/MainWindow.xaml`, `NativeCollectionWindow.xaml`, `NativeDeckEditorWindow.xaml`, `NativeCardDetailsWindow.xaml`, `CommanderBrowserWindow.xaml` and implementation under `ArcaneVault.Next.Collection`.
- **Prior requirements:** `FEATURE_PARITY_AND_QA.md` and `MIGRATION_ROADMAP.md`. Their **target F** is a goal, **not a claim of implementation**.

This is a **source-level screen-and-service audit**, not a behavioral test on a Windows device. A hidden helper, partial proof-of-concept, or advisory-only tool is **not** counted as full parity without end-to-end usable UI and persistence tests.

| Existing WebView section / workflow | Native 0.9.5 evidence | Parity | Required next proof |
| --- | --- | --- | --- |
| Card Browser | Searchable offline catalog, list/compact/text tile view, basic Card Details | **Partial** | Owned/unowned, role, mana-identity/type/rarity filters; artwork grid, exact printing and variant tools |
| Set Progress | Native verified set totals and collector-slot checklist | **Partial** | Arena/paper source choice, art and symbols, chronological sorts, owned/missing filters, parity of counts |
| Physical Collection | Printing-aware quantity editing, CSV/XLSX additive imports, catalog verification, full recovery | **Partial** | Valuation, condition/location, preferred print art, complete sort/filter behavior |
| My Shortlist | No corresponding visible native screen | **Missing** | Saved shortlist, collection-aware candidates and persistent interaction |
| Deck Studio | Native saved-deck editor and advisory format validation | **Partial** | Full deck-building navigation, filters, recommendations, import/export, owned/wildcard UX |
| Deck Library | Native create/edit/duplicate and revision preservation | **Partial** | Favorites, folders/tags, cover artwork management, revision restore parity |
| Deck Lab | No full analysis/goldfishing screen | **Missing** | Mana-base analyzer, sample hands, deterministic goldfishing, readiness/compare panels |
| Wildcard Planner | No corresponding visible native screen | **Missing** | Multi-deck crafting priorities, inventory/wildcard impact |
| Strategy Workbench | No corresponding visible native screen | **Missing** | Probabilities, functional search, deck comparisons and goldfish helpers |
| Archetypes | No corresponding visible native screen | **Missing** | Archetype detection/coverage, recommendations |
| Synergy Lab | No corresponding visible native screen | **Missing** | Combo checks, synergy suggestions, collection-aware upgrades |
| Arena Connection | Native manual Arena inventory editing, **not** log ingestion | **Missing** log connection | Read official local logs, snapshot card evidence, import decks and associate matches |
| Match Performance | No corresponding visible native screen | **Missing** | Match history, per-deck outcomes, deck-version assignment, trends |
| Data & Sync | Native Scryfall bulk sync, full backup/recovery, safe side-by-side updates, legacy-data inspection | **Partial** | Previous QR/USB/Wi-Fi transfers, settings/themes, cache controls and full legacy state migration |

### Other scope decisions

**Already useful in the native preview:** offline Scryfall catalog sync; editing separate physical and Arena inventories; verified printings and unresolved rows; saved decks/basic warnings; CSV/XLSX import previews; native recovery and reset; side-by-side versioned preview releases. Preserve these as we restore functionality.

**Important legacy state not yet proven fully migrated:** match and gameplay history, favorite deck/card flags, deck folder/tag/cover presentation preferences, preferred artwork and alternate-print mappings, theme/settings and cross-device sync lineage. A successful physical/Arena quantity transfer is **not enough** to declare full migration.

**Deliberate backlog (not a pretext for dropping shipping functionality):** new Android app features, new camera scanner, and new EDH Lab capabilities remain deferred. Existing cross-device transfers must still be preserved or explicitly signed off as a supported compatibility fallback.

## Prioritization and release policy

1. **Data safety remains P0.** Keep full recovery, no-loss edits, verified print IDs, catalog update safety and migration proof. Existing old WebView app remains installed and usable throughout.
2. **Everyday native collection + deck workflow parity.** Browser filters and variants; owned printing visuals; full Deck Studio/Library import/export and saved organization; Set Progress modes; price/value/printing preferences.
3. **Feature modules lost in the rewrite.** My Shortlist, Arena logs and Match Performance; Deck Lab/goldfishing; Wildcard Planner; Strategy Workbench; Archetypes; Synergy Lab. Each needs a native workflow, golden fixture tests and user validation, not just a menu button.
4. **Data & Sync parity, settings and performance.** Existing transfer modes or a safe supported compatibility path; low-friction UI; realistic large-collection comparisons.
5. **Go/no-go on Windows 1.0.** Each row must be **Full** or carry **explicit user approval of an intentional scope removal**. Completion requires a user-machine smoke test, observed performance and exact backed-up record comparison. CI green alone does not close a parity gap.

A semver-style number like `0.9.5` is **not 95% complete**. Treat 0.9.x builds as iterative previews; do not represent them as final 1.0 candidates while this matrix remains substantially incomplete.

## Tracking

- Feature-parity gate: **[#62](https://github.com/thejohnfavaro/arcanevault/issues/62)**.
- Legacy feature target matrix and golden tests: `FEATURE_PARITY_AND_QA.md`.
- User-device backup/upgrade acceptance: `WINDOWS_1_0_ACCEPTANCE.md`.
