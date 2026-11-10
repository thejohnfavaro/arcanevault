# AVN-014 — EDH Lab native commander browser (initial preview)

**State: implemented in isolated native WPF prototype, not in the shipping Arcane Vault app.**

## What is implemented
- Load unique commander identities from the installed app's existing Scryfall `cards` SQLite table using a **read-only connection to the prototype's isolated catalog snapshot**. The existing app data is never modified by EDH Lab.
- Open the `EDH Lab — Commanders` window in the .NET 10 WPF preview after selecting an existing Scryfall catalog.
- Switch between paper Commander and Arena Brawl, each with distinct commander eligibility/format legality checks.
- Search all candidates by card name or Oracle text; browse **all** matches across 50-row pages (not capped at 200).
- Color identity W/U/B/R/G, exact/contains-all/contains-any selection, colorless-only, and clear filters.
- `Surprise Me!` securely chooses among **only** the current eligible filtered pool. Optional no-repeat until exhausted; reroll retains filters and shows the selection in the list.
- Local 260+ commander fixture exercises paging, legality, special commander cases, filters, random replay, and never writing the original SQLite file.

## Important coverage and safety limits

**This is a candidate browser prototype, not a complete live legality oracle.** The source SQLite card schema currently stores type, Oracle text, color identity, legalities, image and Arena availability, but not printed P/T or full `oracle_id`. Therefore special Vehicle/Spacecraft commanders whose eligibility depends on P/T are excluded for now. Paired commanders/Background combinations are not supported. Brawl's banlist is the last synced Scryfall data, not a live rules lookup. A paper card may have no Arena printing, so Arena candidates require `arena=1`.

Ownership is deliberately not inferred from the catalog. The preview does not load the user's actual paper/Arena collection into the commander browser, craft cards, create decks, or edit stored profiles. EDH Lab deck-building, collection-first recommendations, card art caching, camera scanning, signed app update and Android companion functionality remain follow-ups.

## Run and test

1. Build/run the **ArcaneVault.Next.Desktop** WPF native prototype using .NET 10 on Windows, or download the `ArcaneVaultNext-NativeUI-SPIKE` artifact from a passing native workflow run.
2. Choose **Open a read-only catalog snapshot** and select `%LOCALAPPDATA%\ArcaneVault\scryfall-catalog.sqlite`. A completed Scryfall card catalog is required.
3. Click **EDH Lab — Commanders**, choose the format, colors, filters, then **Surprise Me!**.

CI checks the native WPF build and `EdhLabChecks.Run(work)` synthetic fixture. **Local on-device behavior and p95 performance remain to be measured** before calling this a release-ready feature.

Next EDH Lab milestones: enrich catalog for special commander eligibility and stable oracle identity; test per-format bans, then add saved-deck support, owned/unowned filtering and Android read-only browsing.
