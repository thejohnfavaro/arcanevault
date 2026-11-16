# Arcane Vault Native Windows v0.9.124

## Added
- Native ownership of the two Arcane Vault icon assets used by the WPF app, pinned launcher, and Windows installer.

## Changed
- Retired the old WinForms/WebView2 Windows source tree and legacy test/solution workflow from the active repository.
- Native CI no longer compiles or monitors the obsolete legacy desktop application.
- Documentation and build entry points refer exclusively to the native .NET 10 application and `native-v*` updates.

## Fixed
- Eliminated build-time references to files in `ArcaneVault.Desktop`, preventing broken native builds after legacy cleanup.
- Removed the legacy Windows release pipeline that could publish confusing v5.x releases.

## Removed
- Legacy `ArcaneVault.Desktop`, `ArcaneVault.Tests`, `ArcaneVault.sln`, legacy release scripts/notes and legacy Windows GitHub Actions workflow.

No native workspace, physical or Arena inventory, decks, saved artwork, prices, backup, or user profile data is modified. Historical source remains in Git history. Native migration support remains intact.
