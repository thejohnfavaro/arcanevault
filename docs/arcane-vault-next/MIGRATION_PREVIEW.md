# AVN-005 — Read-only legacy backup migration preview

**State: diagnostic dry run only. No migrated data, no SQLite writes, no WebView access.**

The separate .NET 10 command reads an **explicitly exported** Arcane Vault full backup (schema 2) or legacy collection-sync JSON (schema 1), and can optionally check print IDs against the **existing** Scryfall SQLite catalog. The executable does not create a new collection database, switch any source of truth, or modify original files. It intentionally does not read installed profile data automatically.

## Usage (Windows, with .NET 10 SDK)

Export a full backup from the shipping Arcane Vault app to a **private location outside the Git repo**. Prefer full backup, since QR/USB snapshots omit decks and some preferences. From a repository checkout:

```powershell
dotnet run --project ArcaneVault.Next.Migration/ArcaneVault.Next.Migration.csproj -c Release -- --backup "C:\Private\arcane-full-backup.json" --catalog "$env:LOCALAPPDATA\ArcaneVault\scryfall-catalog.sqlite"
```

Without `--catalog`, the report is **unverified**: even valid Scryfall UUIDs are counted as unresolved, not confirmed. If using a running Windows app, let catalog indexing finish first, since a live SQLite catalog can be updating while this read-only statement executes.

The JSON output contains **only aggregate counts and warning codes**: Arena copies, physical rows/names/copies, present UUIDs, exact catalog-confirmed printing copies/sets, unresolved copies broken down by reason, duplicate identities, deck and revision counts, and artwork preferences. No individual names, print IDs, deck names, local paths, data hashes, or contents are printed. It is not telemetry and is not uploaded.

The most important distinction is **UUID present != UUID verified**. The obsolete Phase 0 audit treats a set+collector hint as identified, which is useful only for recovery triage. This tool never credits Set Progress from that hint or from a UUID absent from the selected catalog.

## Acceptance and safety

- CI tests a synthetic 16-copy full backup against a separate SQLite catalog: 8 verified copies, 8 unresolved, two sets, duplicate line warnings, and preserved Arena, decks/revisions, art totals.
- Missing/malformed/unknown UUIDs and source set/collector conflicts are reported separately; no identity is inferred from a card's name or set hint.
- It checks the full backup against a schema-1 sync snapshot and rejects negative, fractional, or unsupported data.
- Hash and modification time tests verify that the two **synthetic input files** stay byte-for-byte unchanged.
- No personal inventory files belong in GitHub issues, PRs, CI logs or artifacts.

## Not yet implemented

This is **not** a real profile migration, restore, fix for Android 0% Set Progress, or scanner/save feature. Before an actual migration, we still need read-only extraction of WebView2/Android persisted states, stable record-ID assignment, cross-device conflict review, transactional schema migrations, rollback and disposable-profile user acceptance. **Do not uninstall or clear existing app data.**
