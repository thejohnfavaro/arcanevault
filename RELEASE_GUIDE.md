# Windows release guide

This repository contains only the Windows desktop edition.

1. Add the Windows source project files to the repository root (ArcaneVault.sln, ArcaneVault.Desktop/, ArcaneVault.Tests/, tests/).
2. Push to main and confirm the GitHub Actions Windows build completes successfully.
3. Create and push a release tag such as `windows-v4.9.4`.
4. The workflow builds the Windows x64 application, uploads the ZIP build artifact, and creates a GitHub Release if all preceding steps succeed.
5. Set the in-app update checker repository to `thejohnfavaro/arcanevault`.

This pipeline does not ship an MSI or automatically install updates; it publishes an executable ZIP and the application can notify users about newer versions.

You must keep private card collections, credentials, logs, and databases outside version control. The single-file Windows app still requires the Microsoft Edge WebView2 Runtime.

Android is intentionally excluded from this repository.
