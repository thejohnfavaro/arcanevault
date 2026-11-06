# Arcane Vault for Windows — v5.15.0

## Release overview

This update introduces structured, readable release notes throughout the Windows update experience. Release details are written and reviewed as product changes—not generated from Git commit titles.

## Added

- **Full release notes in the in-app updater.** Check for Updates displays the complete release description, including feature-level changes, fixes, and removals.
- **Versioned release-note documents.** Each future Windows release has its own reviewed Markdown file under `releases/`, shared by GitHub Releases and the app.
- **Separate Added, Changed, Fixed, and Removed sections.** Every category is visible, including when there are no changes in that category.

## Changed

- **Professional update layout.** Release summaries, section headings, and bullet points are presented as readable sections instead of a single truncated paragraph.
- **Release publishing workflow.** Successful Windows builds publish the curated, matching-version release notes rather than an automatically generated list of commits.
- **Up-to-date view.** You can read the latest published release notes even when your installed version is already current.

## Fixed

- **Truncated changelogs.** The previous 900-character limit no longer hides later updates or important release information.
- **Git commit noise.** Internal commit messages are no longer used as the primary customer-facing change history.

## Removed

- **Automatically generated commit-list release notes** for newly published Windows versions. No app features were removed.

## Installation and compatibility

- **Platform:** Windows x64.
- **Update:** In Arcane Vault, open **Import & Data → Application updates → Check for updates**. Review the release notes before choosing to install.
- **Data:** This update does not migrate or delete Arena or physical collections, saved deck libraries, match history, or local settings.
- **Download integrity:** Windows packages continue to use SHA-256 verification before installation.

## Looking back at recent updates

Previous versions introduced physical collection imports (v5.13.0), automatic CSV source recognition for Mythic Tools and other collection managers (v5.14.0), and separate Arena ownership workflows. These were delivered in their respective versions and are not new features in v5.15.0.
