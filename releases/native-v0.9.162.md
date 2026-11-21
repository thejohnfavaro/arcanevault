# Arcane Vault Native Windows v0.9.162 (Preview)

## Added
- Every existing Windows C# regression group now has a discoverable plain-English name and stable suite ID.
- Command-line `--list` shows available C# regression groups; `--suite suite-id` and `--suite=suite-id` run a selected group for focused diagnosis.
- Each group reports RUN, PASS, or FAIL with its name and elapsed time, making CI and local diagnostic logs easier to understand.

## Changed
- Unfiltered regression invocation continues to run all existing C# groups, including synthetic SQLite snapshot safety checks. Default CI coverage is preserved.
- Source runner validates requested suite IDs and fails clearly for unknown arguments or suite names.

## Fixed
- Failure messages now identify the specific C# regression group before propagating the original assertion/exception.

## Removed
- Nothing. No existing C# test assertions, collection storage, match history, proxies, sync logic, or user data were removed or intentionally modified.

### Standalone Windows test harness
The separately distributed Local Test Center v0.4.1 adds a plain-English C# group picker to its GUI; it remains outside the app and does not depend on GitHub Actions. Full regression testing is still recommended before installing a new build.