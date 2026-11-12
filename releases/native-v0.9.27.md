# Arcane Vault Native Windows v0.9.27 (Startup Recovery Hotfix)

This hotfix repairs the native v0.9.26 startup failure reported on Windows. It supersedes native v0.9.26 while preserving the installed side-by-side native application versions and all collection data.

## Added

- A bounded, temporary child-process readiness handshake for verified native updates.
- Startup preflight that builds the WPF main window before the new version confirms readiness and the previous instance closes.
- Native relaunch regression coverage, including early-child-exit handling, safe signal cleanup, and no writes to the collection or installation.
- Visual-shell regression checks guarding against frozen WPF palette-brush mutation and ensuring themeable resource references update dynamically.

## Changed

- Native theme palettes now **replace** shared WPF `SolidColorBrush` application resource entries rather than changing the `Color` of possibly frozen brushes.
- Theme-aware XAML references use `DynamicResource` where appropriate, allowing live theme updates without touching the collection, Scryfall catalog, saved decks, or recovery files.
- The application constructs the main window explicitly, registers crash diagnostics before theme loading, and opens the native workspace only after safe relaunch handoff.
- The native updater keeps the previous working application alive until the replacement executable confirms that its main window can be constructed.

## Fixed

- **Confirmed v0.9.26 crash:** `System.InvalidOperationException: Theme brush is frozen: VaultBackground` in `VaultThemeManager.Apply` during `App.OnStartup`.
- The old fallback no longer repeats the exact failed theme operation.
- A child that exits early or does not acknowledge startup cannot make the old version shut itself down.
- Failed native startup now records diagnostics under `%LOCALAPPDATA%\ArcaneVault\Diagnostics\native-crash.log` before reporting failure.

## Removed

- No core feature has been removed.
- No changes to native collection schema, printing IDs, card ownership, deck revision history, Scryfall catalog, update checksum verification, or recovery file formats.

## Manual verification

1. With the working previous native version running, select Native Updates and install v0.9.27. Confirm the previous app remains open until the new one acknowledges startup, then v0.9.27 opens.
2. Start `ArcaneVault.Next.Desktop.exe` directly from the `v0.9.27` version folder and verify it opens without `Theme brush is frozen`.
3. In Options → Appearance & Themes, preview Arcane Noir, Daylight Archive, a mono-color theme, and a multicolor theme. Verify existing windows change color, Cancel reverts, and Save persists after restart.
4. Confirm collection quantities, saved decks, Card Catalog, Set Progress, and card printing information remain available.
5. Confirm the old native version still launches independently and the local workspace has not been reset or reimported.
6. If a crash recurs, retain and share the latest entries in the native crash log; do not delete previous app version folders.

Both Windows compilation and native .NET 10 compatibility/regression workflows must pass on the final versioned commit. Automated checks do not replace a Windows-device smoke test.
