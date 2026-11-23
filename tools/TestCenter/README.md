# Offline Test Center integration

The Windows offline Test Center catalog is derived from
`ArcaneVault.Next.Tests/Program.cs`, which remains the authoritative list of
named C# regression suites. The checked-in generated inventory lives at
`tools/TestCenter/Tests-Catalog.json`.

## Normal developer workflow

1. Add or update a named regression suite and its readable description in
   `ArcaneVault.Next.Tests/Program.cs`.
2. Run `powershell -NoProfile -File tools/TestCenter/Sync-TestCatalog.ps1`
   from a Windows checkout. This regenerates the JSON **without contacting a
   server, running tests, or touching personal collection data**.
3. Review the generated category/label, then commit
   `tools/TestCenter/Tests-Catalog.json` with the test change.
4. Run `powershell -NoProfile -File tools/TestCenter/Sync-TestCatalog.ps1 -Check`.
   `-Check` is read-only and fails when the suite inventory is stale.
5. Run `powershell -NoProfile -File tools/Run-GuidedChecks.ps1 -Area Menu`
   for friendly targeted checks. Add `-Offline` to forbid package restore
   (requires a prior .NET 10 SDK restore and local package cache).

## Automatic safeguards

- `.github/workflows/test-center-catalog.yml` checks on test-suite and
  catalog changes. It requires exact ordered suite IDs, descriptions,
  categories, test kind, and automation flags to match the generated data.
- The workflow also generates a separate temporary JSON catalog and validates
  it to catch broken sync scripts. It does not require NuGet restore.
- `tools/Run-GuidedChecks.ps1` checks catalog consistency before running
  C# regression suites.
- The native Windows build and its behavioral regression suite remain a
  separate, required release gate. The inventory check **does not** substitute
  for passing tests, exercising the GUI, or installing the release.

## Using the standalone Test Center

Unpack the standalone Test Center ZIP in a **dedicated tools directory**.
It must remain separate from the real Arcane Vault native workspace.
Select your local source checkout in the GUI, click **Refresh test list** to
read the checkout's current `Program.cs`, then run **Check setup**. Tests
operate on disposable synthetic fixtures, not on the live collection.

The standalone ZIP ships a copy of the generator for use without a Git
checkout at its own location. The committed JSON catalog is the source for
package creation; the GUI's Refresh test list action can update its local
copy after a newer source checkout is selected. If an old standalone ZIP is
still in use, download the updated package to obtain the refresh action.

## Coverage dashboard (v0.6.0)

The standalone offline Test Center v0.6.0 adds a **Coverage dashboard**.
It reads the reviewed `Coverage-Map.json` mapping and shows:

- Evidence levels (`behavioral`, `source-plus-behavior`, or `manual-only`).
- The exact named native regression suites associated with each system.
- PASS, FAIL, SKIP, and NOT RUN counts **only from the latest local test report**.
- Written manual acceptance steps and known test gaps per system.
- An exportable Markdown coverage review saved under `Reports/`.

Neither the dashboard nor the mapping claims that test-suite presence proves
full feature coverage, rendered WPF behavior, Android tests, or Xeneon hardware
verification. Most recent test runs may select just one suite; unexecuted
suites must remain NOT RUN.

When creating a new named C# suite, update the reviewed
`tools/TestCenter/Coverage-Map.json` if it covers a new system or closes a
coverage gap. CI executes `tools/TestCenter/Test-CoverageMap.ps1`, rejecting
unknown suite IDs, incomplete manual gaps, and suites not assigned to any
system. Unlike the generated test inventory, the mapping and its rationale
are **curated** and must be reviewed, not blindly auto-generated.

## Source of truth and quality claims

- Test suite IDs and descriptions: `ArcaneVault.Next.Tests/Program.cs`
- Official catalog, committed and CI-validated: `tools/TestCenter/Tests-Catalog.json`
- Category assignment policy: `tools/TestCenter/Sync-TestCatalog.ps1`
- Actual automated assertions: `ArcaneVault.Next.Tests` via `dotnet run`
- Manual UI and device acceptance: `docs/arcane-vault-next/TESTING_STANDARDS.md`

An inventory item is **not** a passing test. The GUI must report non-executed
tests as SKIP and report manual tests separately.
