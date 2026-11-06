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
