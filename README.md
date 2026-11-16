# Arcane Vault — Native Windows

Arcane Vault is a local-first Magic: The Gathering collection manager, deck builder, and EDH Lab for Windows. The supported desktop application is **ArcaneVault.Next.Desktop** (.NET 10 / WPF), released using the `native-vX.Y.Z` version series. The Android application is maintained separately in `thejohnfavaro/arcanevaultmobile`.

## Native development

Use a Windows machine with the .NET 10 SDK and the Windows desktop workload.

```powershell
dotnet restore ArcaneVault.Next.Desktop/ArcaneVault.Next.Desktop.csproj
dotnet build ArcaneVault.Next.Desktop/ArcaneVault.Next.Desktop.csproj -c Release
dotnet run --project ArcaneVault.Next.Tests/ArcaneVault.Next.Tests.csproj -c Release
```

The native CI workflow is `.github/workflows/arcane-next-native.yml`. It builds and tests the native Windows application, launcher, installer, migration tool, and versioned release assets. Curated notes are in `releases/native-v*.md`. Only `native-v*` releases are valid updates for this app.

## Data safety and historical compatibility

User data resides under `%LOCALAPPDATA%\ArcaneVaultNext` and is not part of the source repository. Existing native inventory, decks, artwork, prices, and backups must remain untouched by application updates.

The old WinForms/WebView2 Windows app (`ArcaneVault.Desktop`, v5.x) has been retired from `main`. Its dedicated tests, solution, release workflow, scripts, and old release notes have been removed to prevent accidental legacy releases. Native migration and backup-compatibility components remain intentionally available for reading old backups, and the historical code is recoverable from Git history.

The native application uses the two original Arcane Vault icon assets now stored in `ArcaneVault.Next.Desktop/Assets`. The native project, installer, and permanent launcher all refer to these paths.

## Stream Deck Companion (native Windows)

Native Windows v0.9.126 includes a read-only Stream Deck Companion adapter. While Arcane Vault is running, it writes the existing Alpha 0.1 plugin's compatible presentation snapshot to `%LOCALAPPDATA%\\ArcaneVault\\streamdeck.json`. This path is retained **only for plugin compatibility**; authoritative native data remains under `%LOCALAPPDATA%\\ArcaneVaultNext`.

The snapshot includes saved native deck names/IDs, cached official Scryfall commander artwork, latest recorded physical collection valuation, confirmed Arena match W/L, and an Arena-selected current deck when uniquely identifiable. Artwork downloads are incremental, so the deck list can appear before deck images.

Confirmed Arena matches are read from MTG Arena's local `Player.log` when Detailed Logs is enabled. Deduplicated results are stored in the independent native `ArcaneVaultNext\\match-history.sqlite3`; an existing historical `ArcaneVault\\match-history.sqlite3` can be read without modification, and duplicate match IDs are counted once. Unverified outcomes and unidentifiable decks never count as confirmed wins or losses.

For diagnosis, inspect `%LOCALAPPDATA%\\ArcaneVault\\Diagnostics\\streamdeck-export.log`. The Stream Deck plugin's deck selector can be refreshed in its property inspector. This integration never modifies collection or saved-deck data, and does not run the retired legacy executable.

## Rights

Arcane Vault is unofficial fan content, not endorsed by Wizards of the Coast. Magic: The Gathering and its card artwork and trademarks belong to their respective owners. Arcane Vault is not affiliated with Scryfall.
