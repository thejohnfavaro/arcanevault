# Arcane Vault - Original Razer Stream Controller Plugin (MVP)

**Status: source prototype committed; Windows plugin build and physical hardware testing pending.**

Uses the official Loupedeck / Logitech Actions SDK and the existing read-only
Arcane Vault snapshot at %LOCALAPPDATA%\ArcaneVault\streamdeck.json.
No collection records, match records, Arena logs, or saved decks are written.

## Controls

| Control | Function |
|---|---|
| Select Saved Deck (dial) | Rotate to cycle saved decks; press to reset |
| Selected Deck W/L (touch key) | Display selected deck name and confirmed W/L |
| Arena Constructed Rank (touch key) | Display latest recorded rank |
| Collection Value (touch key) | Display latest exported USD value |
| Previous / Next Deck (touch keys) | Move through saved decks |
| Refresh Arcane Vault (touch key) | Request exporter refresh |

Deck choice is local to the plugin and does not change Arcane Vault's
selected deck or the existing Xeneon Edge selection.
Touch-key labels refresh when the selected deck changes and periodically
when the exported snapshot file changes. Custom image-backed touch-key
rendering is not included in this first version.

## Building on Windows

1. Verify Arcane Vault runs and the streamdeck.json snapshot exists.
2. Install a version of Loupedeck / Logi Plugin Service that detects the
   original Razer Stream Controller, and install the .NET 8 SDK.
3. In this directory, run: .\Build-RazerPlugin.ps1
4. If PluginApi.dll is not found, supply the SDK directory with
   -PluginApiDir 'C:\path\to\PluginApi.dll folder'.
5. To create the installer package, install the official plugin tool:
   dotnet tool install -g Loupedeck.PluginTool
   Then run: .\Build-RazerPlugin.ps1 -Package
6. Install the resulting .lplug4 via the Loupedeck package installer.
7. Assign buttons and a dial from the Arcane Vault plugin action group.

Package validation and device compatibility are not yet verified.
The plugin uses a .NET 8 SDK reference; software versions that only support
older plugin runtimes may need a separate compatibility build.

## Suggested layout

Top row: Deck W/L | Constructed Rank | Collection Value | Refresh.
Second row: Previous Deck | Next Deck | two spare slots.
Dial 1: Select Saved Deck. Keep other dials for existing streaming controls.

## Planned follow-up

Live image tiles with saved deck art; automatic snapshot-change refresh;
additional dials for changing displayed metrics and paging through collections.
These enhancements should be tested on the physical controller before release.