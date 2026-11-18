# Arcane Vault Native Windows v0.9.147 (Preview)

## Added
- Explicit, independently rendered 16/20/24/32/40/48/64/128/256px Windows ICO frames, including simplified high-contrast small-size vault glyphs.
- Larger, independent 128px sidebar emblem rendered prominently in a 56px tile.
- Windows branding repair instructions for the permanent installed launcher and pinned shortcuts.

## Changed
- WPF main window uses the packaged multi-resolution ArcaneVault.ico for its window/taskbar/Alt+Tab icon rather than allowing Windows to downscale the generic PNG.
- The brand builder saves each icon frame individually instead of downsampling the same detailed 256px image for all shell sizes.
- The installer continues to refresh the permanent launcher ArcaneVault.exe and installed ArcaneVault.ico for the Start Menu and Explorer.

## Fixed
- In-app identity image appeared tiny due to nested 36px scaling and padding.
- Prior ICO builder created simplified 16px artwork but silently ignored it during ICO export, resulting in unreadable small Windows icons.
- Documentation now distinguishes the version-only in-app updater from the installer required to refresh the permanent Windows launcher executable's embedded icon.

## Removed
- No functionality removed.

**Important:** To refresh the **Explorer/Start Menu/taskbar launcher icon**, install v0.9.147 using its **Windows installer over the existing installation** (same folder). An in-app update alone does not replace the permanent pinned launcher EXE. If the taskbar still shows a cached icon after installer upgrade, unpin then repin the new Start Menu shortcut. Your collection and saved decks are untouched.
