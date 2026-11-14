# Arcane Vault Windows v0.9.104 — safer updates, card details, workspace UI

## Fixed
- Prevent WPF cross-thread access when double-clicking into Card Details: capture the selected card name on the UI thread before asynchronous preferred artwork, reverse face, or bookmark queries.
- Replace the misleading Set Progress attribution for recovered UI failures with a more accurate diagnostic message. The existing crash log remains available.
- Card Catalog and commander previews omit missing-art panels instead of drawing blank image widgets.

## Added
- Safe post-startup updater cleanup: retain the running verified managed application version and only the newest previous verified version for rollback. Remove older updater-managed versions if they can be safely validated. Skip unknown directories, symlinks, locked binaries and any path outside the managed versions root.
- In-app update confirmation overlay for installing and restarting; completion/failure information uses Arcane Vault's Activity notice system.
- Full-size **My Shortlist** workspace replacing the floating window, reusing verified bookmark, ownership, search, removal and Arena-text export actions.
- Official card-art thumbnails alongside commanders in the left EDH Lab browsing list.

## Changed
- Replace "Windows Native Preview" and "Windows native edition" labels with normal Arcane Vault branding.
- Activity notifications now appear as a compact bubble in the Card Catalog header, where the old preview badge was.

## Removed
- The obsolete "Native Windows Preview" and "Windows native edition" UI labels have been retired.
- No collection, deck, proxy, sync baseline or recovery features have been removed.

## Safety
- Updater cleanup never touches the native collection, catalog data, decks, imported files, saved snapshots, artwork preferences, proxies or recovery backups.
- All update installers are checksum-verified as before, and the old executable remains available if the new one does not successfully start.
- Synthetic retention tests and native regression, WPF shell and Windows build checks passed on the feature branch.
- Companion Android v2.1.16 removes stale *downloaded APK staging files* after installation rather than trying to delete Android-managed installed packages.
