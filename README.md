# Arcane Vault — Windows

**Arcane Vault** is a local-first Magic: The Gathering Arena collection browser, deckbuilder and analysis tool.

**By RNGesus · Copyright © 2026**

This repository is dedicated to the **Windows desktop edition**. The Android edition is maintained separately.

## Windows build

Requires Windows, Visual Studio 2022 (or .NET SDK 8), and access to NuGet.

1. Open `ArcaneVault.sln` at the repository root.
2. Run `dotnet restore ArcaneVault.sln`.
3. Run `dotnet build ArcaneVault.sln --configuration Release`.
4. Run `dotnet publish ArcaneVault.Desktop/ArcaneVault.Desktop.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true`.

The publishing workflow at `.github/workflows/windows.yml` builds and tests the Windows edition and uploads a ZIP as a workflow artifact. When a tag beginning with `windows-v` is pushed, it also creates a GitHub Release.

**Repository setup status:** The workflows are being prepared before the Windows source project is uploaded. Builds will not pass until the solution files are added to the repository.

## Updating Arcane Vault

The app's update-check settings can point at `thejohnfavaro/arcanevault`, after Windows release tags and downloadable assets are published.

## Third-party rights

Arcane Vault is unofficial fan content, is not approved or endorsed by Wizards of the Coast, and is not affiliated with Scryfall. Magic: The Gathering and related artwork, text, logos and trademarks remain the property of Wizards of the Coast and/or their respective owners. Refer to the in-app About page for attributions and policy links.
