# Arcane Vault Next — WPF/.NET 10 compatibility spike

This is an **isolated proof of concept**, NOT a replacement Arcane Vault app. It intentionally has no connection to the user's WebView2 profile, card database, Scryfall account, or collection.

It validates the native WPF window, recyclable ListView virtualization, background filtering with cancellation, and `net10.0-windows` compilation. It displays 25,000 **synthetic** card rows and reports local sample filter timing. It does not benchmark real SQLite queries, image caching, startup, disk usage, or real collection workloads.

## Build locally (Windows with .NET 10 SDK)

```powershell
dotnet restore ArcaneVault.Next.Desktop/ArcaneVault.Next.Desktop.csproj
dotnet build ArcaneVault.Next.Desktop/ArcaneVault.Next.Desktop.csproj -c Release --no-restore
dotnet run --project ArcaneVault.Next.Desktop/ArcaneVault.Next.Desktop.csproj -c Release
```

## Proposed promotion gate

Before native UI code replaces any production view, require a golden-data sample imported into an isolated database, p95 search/filter/list-scroll timings with 1k/5k/25k data, realistic card images and memory profiles, accessibility tests, and no lost owned printings. Existing Windows shipping app remains `net8.0-windows` while compatibility testing proceeds.

The independent GitHub Actions job also tries to compile the *existing* Windows application sources with `TargetFramework=net10.0-windows` as an explicit command-line override; this does not modify or retarget the shipping project.
