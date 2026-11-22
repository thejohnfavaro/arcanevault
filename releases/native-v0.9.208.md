# Arcane Vault Windows v0.9.208 (Preview)

## Added
- A headless-testable shared opponent-color annotation precedence helper in `MatchPerformanceAnalytics`.

## Changed
- Native Match History calls the shared helper rather than declaring the reconciliation helper inside a WPF-only view.
- The regression fixture exercises the same helper from the cross-platform native test project, without referencing WPF controls.

## Fixed
- The v0.9.206/v0.9.207 Windows workflow failed in the native test compilation step, not the desktop compilation step. GitHub Actions run 45057161786 reported three CS0103 errors in `MatchPerformanceChecks.cs` at lines 108–110 because `NativeMatchHistoryView` is not included in the headless test project. All three references now resolve through `MatchPerformanceAnalytics`, which is included by the existing test project.
- The saved deck analytics scope and readable two-column layout are preserved without changing recorded matches or collection data.

## Removed
- The duplicate UI-local opponent-color precedence method.

## Verification
- Confirmed from GitHub Actions run 45057161786 that the WPF desktop build succeeded with zero compilation errors before the native test compilation failed on three CS0103 errors.
- The patch specifically removes the missing WPF class dependency from the headless test fixture. The new v0.9.208 CI run and installer outcome have not yet been independently confirmed.
- Run the complete `ArcaneVault.Next.Tests` suite, then Windows packaging and release checks.
