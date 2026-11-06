# Windows release guide

1. Push source changes to `main`; verify the Windows build workflow passes.
2. Create and push a version tag (example: `windows-v4.9.4`).
3. GitHub Actions builds, tests, publishes a `win-x64` self-contained app ZIP, and creates a release if all preceding steps pass.
4. The application update checker should target repository `thejohnfavaro/arcanevault`.
5. Updates currently notify the user and link to downloads. They do not install automatically.

The compiled app uses a native WebView2 runtime, which is not bundled. Do not distribute an unsigned installer as if it were a signed release. Keep private deck data and collection CSVs out of the repository.

Android is intentionally excluded from this repository and its release workflow.
