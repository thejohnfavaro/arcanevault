# Arcane Vault Windows v0.9.224 (Preview)

## Added
- A bounded retry sequence for temporary GitHub HTTP 408, 429, 500, 502, 503, and 504 responses while checking or downloading native updates.

## Changed
- Each retry begins again at the original trusted GitHub URL and validates every redirect. Failed HTTP responses are disposed before retrying.
- Existing verified checksums, archive validation, side-by-side installation, and collection protections are unchanged.

## Fixed
- An intermittent GitHub gateway timeout (HTTP 504) previously aborted an otherwise safe native app update immediately.

## Removed
- Nothing. The existing native Windows v0.9.223 deck-editor Card Catalog shortcut remains available.

## Verification
- Native Windows GitHub Actions must build and run the regression tests, package a checksummed portable ZIP, compile the installer, and publish the release.
- On an installed v0.9.223 client, choose **Check for Updates** again when v0.9.224 is published.
- Confirm that a temporary GitHub gateway timeout no longer immediately aborts; persistent errors still fail without modifying the active executable or collection.
