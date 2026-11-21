# Arcane Vault Windows Regression Test Standard

> Adopted November 21, 2026. Applies to new tests and incremental edits to existing Windows test groups.

## Goal

A contributor with moderate C# knowledge should be able to answer three questions from any test: *what behavior is being checked; what data is used; what should happen?*

## Default test convention

1. **Name the behavior**, not the implementation detail (for example, "Saving a deck preserves the commander").
2. **Arrange** a small synthetic fixture in an isolated temporary workspace. Never use a real collection database as a disposable fixture.
3. **Act** by calling the production implementation.
4. **Assert** the exact expected outcome, with an actionable failure message. Test negative and boundary cases when important.
5. **Cleanup** temporary resources and files, even after an assertion fails.

Keep each case focused. Use stable identifiers or clear method names. Document unusual fixture assumptions in comments rather than hiding them inside helper logic.

## New feature checklist

- Add behavioral regression coverage in `ArcaneVault.Next.Tests` near related tests; keep the test runner `Program.cs` updated.
- Run `dotnet run --project ArcaneVault.Next.Tests/ArcaneVault.Next.Tests.csproj -c Release` locally and on CI.
- Add a readable suite description to the standalone local Test Center's `Tests-Catalog.json`; run its `test-catalog` component to check the inventory.
- Include a manual Windows smoke check for operations that cannot be validated by headless tests (visual layout, installer, user-facing synchronization).
- Preserve data integrity: do not mutate real user data, overwrite backups, or assume a changed SQLite hash means corruption.
- Do not represent source string/presence checks as behavioral regression coverage.

## Existing tests

The existing C# regression suites are **not replaced** by the standalone Test Center. Their assertions remain authoritative. When touching an existing suite, improve names, Arrange/Act/Assert structure, and failure messages incrementally, with the same assertions still passing. Avoid wholesale rewrites that could accidentally remove coverage.

The standalone local Test Center v0.4.0 provides human-readable catalog entries for 50 Windows C# test groups and adapters for build, environment, executable, and safety checks. Test inventory presence is not equivalent to an individual test's pass.

## Release gates

For Windows releases, CI restores the desktop and tests projects, compiles the WPF application, runs the C# regression suite, exercises the synthetic read-only SQLite acceptance check, packages and hashes the portable bundle, builds the installer, validates release notes and version ownership, then publishes a uniquely tagged release. A successful local test run does not by itself prove installer, GUI or in-place upgrade behavior on the user's PC.

## Result vocabulary

- **PASS:** executed and its assertions succeeded.
- **FAIL:** mismatch, exception, or failed required operation.
- **WARN:** a diagnostic could not complete or warrants review.
- **SKIP:** not executed; do not count as success.
- **MANUAL:** verification must still be performed by a person.

Report the test name, version/commit, expected outcome, observed result, and failure reason. Use synthetic data in examples.

## Independent local harness

The standalone GUI remains separate from GitHub Actions. It supports readable modular PowerShell components and a plain-English catalog; download and unpack it in a dedicated local tools directory, not inside the live collection data directory.