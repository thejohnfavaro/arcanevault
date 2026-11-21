# Arcane Vault Native Windows v0.9.166 (Preview)

## Added
- Added a new `native-crash-recovery` regression suite that runs the actual native workspace refresh in a disposable child process.
- The suite forcibly terminates the child immediately after verified SQLite staging and immediately after the Windows atomic replacement, then checks active and recovery collections and verifies retry behavior.
- Added test-only, internally accessible crash-point callbacks restricted to the Windows regression assembly, not controllable by application users.

## Changed
- Windows regression reporting now covers forced process failure at the most important save boundaries, alongside existing cancellation, stale-SHA, and corruption checks.
- Tests use only synthetic data in automatically generated temporary folders and do not touch installed user collections.

## Fixed
- Improved regression coverage for recovery-file retention and protection of the original workspace when a process terminates before replacement.

## Removed
- Nothing. No existing test assertions, collection records, or recovery functionality were removed.
