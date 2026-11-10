# Arcane Vault Next — Read-only Native Catalog Performance Prototype

This is a **separate experimental WPF/.NET 10 application**, not a replacement for the shipping Arcane Vault executable. The shipping Windows app and its framework/version are unchanged.

## Current functionality

- **EDH Lab — Commanders** opens a **second native WPF window** after you load a full Scryfall catalog. Browse every eligible identity through 50-row virtualized pages (without the original card search's 200-result cap); choose paper Commander or Arena Brawl, filter exact/includes-all/includes-any mana identities, search names or Oracle text, and use **Surprise Me!** with optional no-repeats. This is an offline, read-only prototype: no deck creation, collection edits, ownership detection, or live legality downloads.


- The native virtualized list starts with 25,000 deterministic synthetic cards; no account, collection, or catalog is accessed automatically.
- **Open a read-only catalog snapshot** lets you explicitly choose the existing Scryfall catalog SQLite file. The app opens that file in SQLite read-only mode, uses SQLite's transactionally consistent online backup to make a private temporary snapshot, then queries only the copied printing catalog.
- The first **200** matching printing rows are displayed. Search is a **name-prefix search**, not the full production Card Browser filter engine; percent signs, underscores, and backslashes are treated as literal characters. Queries are parameterized and can be canceled. Query milliseconds are displayed.
- **Use 25,000 synthetic cards** closes and deletes the isolated catalog snapshot and restores the prior synthetic view.
- The snapshot is deleted when the prototype closes normally; after a crash, the OS temporary directory may retain an orphaned SQLite file. Do not rely on automatic cleanup after a forced termination.
- The snapshot copies the entire local catalog database (including its cached ownership mirror), but **never imports, changes, or displays user ownership**; the snapshot is kept under the user's temporary directory, not uploaded.

## Build and try on Windows

Use the .NET 10 SDK.

    dotnet restore ArcaneVault.Next.Desktop/ArcaneVault.Next.Desktop.csproj
    dotnet run --project ArcaneVault.Next.Desktop/ArcaneVault.Next.Desktop.csproj -c Release

Select the catalog from:

    %LOCALAPPDATA%\ArcaneVault\scryfall-catalog.sqlite

If the catalog has never been downloaded in the current Windows app, return to the shipping Arcane Vault app and complete a Scryfall sync first. This prototype does not download or index cards.

Or retrieve the **ArcaneVaultNext-NativeUI-SPIKE** artifact from the passing GitHub Actions workflow. This preview is not signed or packaged as a shipping Arcane Vault update, and no user should replace their existing Windows executable with it.

## Synthetic automated tests

    dotnet run --project ArcaneVault.Next.Tests/ArcaneVault.Next.Tests.csproj -c Release

CI creates a synthetic SQLite printing catalog (25,202 rows), verifies source hash remains unchanged after the read-only snapshot, checks isolation from a later synthetic source edit, tests literal prefix handling and bounded result limits, and asserts temporary snapshot cleanup. It also reports p50/p95 query timing from the CI machine. No user's cards or backups are uploaded.

## EDH Lab candidate rules and known coverage gaps

- Eligibility requires an explicit local Scryfall format legality of `legal`. Brawl uses current `brawl`, with `historicbrawl` used only when `brawl` is absent. Cards must also be available on Arena for Brawl. An unknown/malformed legality excludes the card; the local catalog may be stale.
- Paper Commander includes legendary creatures, plus explicit `can be your commander` / `may be your commander` exceptions. Arena Brawl additionally supports legendary planeswalkers. Standalone Background enchantments are not treated as commanders.
- A qualified Vehicle or Spacecraft requires validated printed power/toughness, but the current legacy `cards` table does **not** preserve it. Such special cases are conservatively excluded until catalog schema enrichment. **This preview is not yet an exhaustive commander legality checker**, especially for paired commanders.
- A commander name appearing on a list does **not** mean that you own it. Paper and Arena ownership remain separate and are not read by this window.
- The random picker uses uniform secure random index selection from the current legal and filtered pool; filters are retained when rerolling. A result set with zero candidates returns a clear message instead of a card.

## Current limitations and next engineering gates

- This prototype is **not** a native collection manager yet: no owned printing selections, metadata enrichment, artwork display, deck editing, progress, backup restore, or Scryfall network requests.
- The timing displayed is **SQL query timing**, not end-to-end input latency or first paint.
- A temporary snapshot of a large catalog consumes extra free disk space. Verify disk availability before testing on a low-space device.
- WPF scrolling, artwork memory, cold/warm startup, and 1k/5k/25k realistic browsing need explicit reference-device performance measurements before deciding to replace the current UI.
- The existing WebView2 source-of-truth storage requires its own migration dry-run and rollback verification; **this prototype does not read it**.
