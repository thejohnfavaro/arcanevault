# Arcane Vault — Windows

Arcane Vault is a local-first MTG Arena collection browser, deckbuilder and deck analysis app.

**By RNGesus · Copyright © 2026**

This repository is **Windows only**. The Android project is distributed separately.

## Development

Open `ArcaneVault.sln` in Visual Studio 2022 with the .NET 8 desktop development workload, then restore and build.

```powershell
dotnet restore ArcaneVault.sln
dotnet build ArcaneVault.sln -c Release
dotnet run --project ArcaneVault.Tests/ArcaneVault.Tests.csproj -c Release
```

To publish a single-file Windows executable, use `PUBLISH_ONE_EXE.bat` or the GitHub Actions pipeline. The destination computer still needs the Microsoft Edge WebView2 Runtime.

## Continuous integration and releases

GitHub Actions tests/builds on Windows and produces a downloadable ZIP. Pushing a tag such as `windows-v4.9.4` publishes a GitHub Release, after the workflow succeeds. A tag alone is not evidence the app passed testing; check Actions before sharing releases.

The in-app update checker can be pointed to `thejohnfavaro/arcanevault` to discover releases. This is **an update notification**, not an unattended installer.

## Rights

Arcane Vault is unofficial fan content, not approved or endorsed by Wizards of the Coast. Magic: The Gathering and its trademarks and card artwork belong to Wizards of the Coast and/or their respective owners. Arcane Vault is not affiliated with Scryfall; see the in-app About section for details.

## Elgato Stream Deck Companion (Windows)

The desktop app now exports a **read-only** Stream Deck snapshot to
`%LOCALAPPDATA%\ArcaneVault\streamdeck.json` automatically while Arcane Vault is running.
The Arcane Vault Companion Stream Deck plugin reads this file approximately every five seconds.
You do not need to copy test JSON into this location.

- **Deck W/L:** Saved decks from the `libraryV3` deck library. The export joins confirmed win/loss
  rows in `match-history.sqlite3` by unique Arena deck ID first, or unique saved deck name.
  Unknown match results and ambiguous assignments are deliberately excluded.
- **Artwork:** The deck's selected cover card (falling back to commander/first card).
  Scryfall card imagery is cached locally under `StreamDeckArtwork`; artwork may take
  additional refreshes to appear.
- **Current deck:** Uses Arena's recent explicit deck selection when uniquely mapped to a saved
  deck, otherwise the currently selected Arcane Vault studio deck.
- **Collection value:** The existing physical collection valuation snapshot, not Arena ownership.
  Displays unavailable until physical prices have been calculated in Arcane Vault.
- **Recent match:** The latest confirmed win/loss associated with a saved deck.
- **Launch:** Uses the current Windows executable path, without manual configuration.

This does **not** automatically turn on Arena log tracking: enable it in Arcane Vault and enable
Detailed Logs in MTG Arena. Deck match history is still governed by Arcane Vault's existing
match deduplication and manual corrections. Deleting or renaming a saved deck can make previously
assigned matches unmatchable unless the Arena deck ID is maintained. The snapshot contains
only collection and match summaries, never the entire personal collection.

If buttons show **NO DATA**, confirm Arcane Vault has been updated to a build containing
`StreamDeckBridge.cs`, the Windows app is running, and the snapshot file exists.
For deck selectors, reopen the Stream Deck action settings and click **Refresh deck list**.

### Release note

These changes are source commits on `main`; they are not evidence a Windows release or
Stream Deck hardware validation has succeeded. Run the .NET solution tests and publish/release
a Windows build before relying on automatic deployment via the in-app updater.
