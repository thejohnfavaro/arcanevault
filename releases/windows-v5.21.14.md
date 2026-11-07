# Arcane Vault Windows windows-v5.21.14 — Reliable Update Checks

## Added
- Native HTTPS fallback for GitHub release discovery via a restricted local-app endpoint.
- A direct GitHub Releases link when both native and embedded-browser update checks fail.
- Clearer network error diagnostics explaining whether the local service or browser request failed.

## Changed
- Query the operating system's native HTTPS stack before making a direct embedded-browser request to GitHub.
- Record the last successful update check only after the release listing is fetched.
- Restrict the native endpoint to GitHub's releases API for syntactically valid owner/repository names; never proxy arbitrary network URLs.

## Fixed
- Update checks could report "Could not check for updates: Failed to fetch" when the embedded WebView failed to reach GitHub, even though published releases were available.
- Preserve a clickable release fallback when update discovery is temporarily unavailable.

## Removed
- No features removed.

## Data safety
- No collection data, printing identities, backups, synchronization data, or schema changes.
- Updating an existing Android installation must use an APK signed with the same key, without uninstalling the app.
