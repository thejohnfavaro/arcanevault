# Arcane Vault for Windows v5.21.16

## Added
- Build-time regression check that the bundled SQLite engine is at least version 3.50.2.

## Changed
- Updated Microsoft.Data.Sqlite from 8.0.11 to 8.0.33, which references the newer SQLitePCLRaw 2.1.13 native bundle and addresses the previous vulnerable transitive SQLite package warning.

## Fixed
- Replaced the outdated bundled SQLite native dependency flagged by GitHub security advisory GHSA-2m69-gcr7-jv3q.

## Removed
- No user-facing features removed.

This is a dependency/security maintenance release, not a collection migration. The existing catalog and user collections retain their current file locations and schemas. Do not remove prior backup files during the update.
