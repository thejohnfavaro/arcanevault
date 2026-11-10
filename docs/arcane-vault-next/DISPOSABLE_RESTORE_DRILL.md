# AVN-005 — Disposable full-backup restore drill

This is **the first restore-safety exercise**, not a production restore feature. It never touches a running Arcane Vault installation, real WebView2 profile, Android data, or user collection.

The native C# test suite now runs `BackupRestoreDrill.Run(temp)` against a temporary SQLite file. It uses generated synthetic data modeled on the app's existing schema-2 full backup JSON:
- Arena collection entries with owned quantities.
- Multiple physical editions/finishes of the same name, split-card names, and an unresolved printing.
- Deck entries, revisions, preferred artwork, and wildcard metadata.

## Tested guarantees

1. Validate input structure before staging.
2. Store full source JSON and each inventory/deck/artwork record in disposable SQLite under a transaction.
3. Verify full-document byte hash and section-by-section exact JSON round-trip, quantity totals, printing IDs, row counts, deck revisions and artwork.
4. Attempt a deliberately malformed second restore after valid data are staged; require the original committed snapshot and counts to survive unchanged.
5. Delete the disposable test directory at completion; failures fail the Windows build's C# regression step.

Run from the Windows repo:

```powershell
dotnet run --project ArcaneVault.Tests/ArcaneVault.Tests.csproj -c Release
```

## Limitations and next gates

- This does **not** restore a full backup to a WebView2 profile. The existing WebView2 localStorage restore behavior is not exercised.
- This is a staging/rollback safety proof, not a finalized shared SQLite schema.
- Synthetic inputs do not prove a user's actual backup has all user preferences.
- Before a real migration: perform read-only extraction of the full WebView2 and Android stores, verify approved private backups against installed data, and test full restoration on a disposable Windows profile.
- No user backup or file containing user card names belongs in a public CI artifact.
