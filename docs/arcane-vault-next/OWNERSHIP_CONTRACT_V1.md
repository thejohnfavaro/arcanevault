# AVN-004 — Printing-aware ownership contract v1 (isolated read model)

This is the **first Phase 1 domain slice**, not a live collection migration. No persistent writes. Existing Windows and Android apps remain unchanged.

## Decisions and invariants

- Every physical line has a stable UUID, name, nonnegative quantity, finish and **optional** Scryfall printing UUID. Multiple printing/finish rows remain separate.
- Null printing IDs stay unresolved even with a plausible name, set and collector number. IDs absent from the trusted local catalog also stay unresolved.
- Where a source supplies set/collector metadata that **conflicts** with the matched ID, the row stays unresolved for review. A name mismatch alone doesn't invalidate an exact ID: split-card and face names may differ.
- Set Progress credits **only** exact ID matches in a trusted catalog. The catalog's set decides credit, not an import's set hint. Multiple owned rows for the same printing count as one distinct printing but multiple copies.
- Zero-quantity rows are retained without counting as owned. Duplicate stable line IDs, malformed catalog identities, duplicate catalog print IDs, or negative quantities are rejected instead of silently merged.
- A completion percentage is **not** calculated yet: set denominator policy for variants/promos needs an explicit contract.

## Synthetic golden fixture

`contracts/ownership-v1.golden.json` has 13 copies: six linked to three exact printings, seven unresolved due to missing ID, missing catalog ID, or source/catalog conflict. It includes multiple finishes, split card, and zero quantity. No actual user's data is committed.

Run with .NET 10:

```powershell
dotnet run --project ArcaneVault.Next.Tests/ArcaneVault.Next.Tests.csproj -c Release
```

Windows CI runs the golden checks beside the catalog snapshot checks. **The Android Java version of this test is still required** for cross-platform parity; this change does not claim Android v2 sync, live migrations, or Set Progress fixes.

## Next gates

1. Mirror this fixture and exact line-level outputs in Android Java tests.
2. Implement read-only legacy extraction + dry-run report and compare totals, names, ID provenance, decks, and artwork without changing live profiles.
3. Define transactional SQLite ownership schema + rollback tests before enabling native inventory edits or scanner writes.
