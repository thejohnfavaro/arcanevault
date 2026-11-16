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

## Stream Deck Companion

The legacy-only Stream Deck exporter was removed with the retired WinForms application. It was **not** a native WPF integration. A native exporter needs to be implemented against the verified native deck library, native collection valuation and actual Arena match data before Stream Deck buttons can receive live records. Do not assume the legacy snapshot exists or copy data between user profiles.

## Rights

Arcane Vault is unofficial fan content, not endorsed by Wizards of the Coast. Magic: The Gathering and its card artwork and trademarks belong to their respective owners. Arcane Vault is not affiliated with Scryfall.
