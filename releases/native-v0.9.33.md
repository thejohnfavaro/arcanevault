# Arcane Vault Native Windows v0.9.33 — Restore EDH Lab Navigation (Preview)

This is a focused follow-up to v0.9.32. All EDH Lab construction and
Deck Blueprint features remain present; this release exposes the launcher
that the previous native sidebar layout accidentally hid.

## Added

- A visible **EDH Lab** workspace entry in the native Windows left sidebar,
  immediately below Deck Library.
- Automated visual-shell coverage that prevents the EDH Lab launcher from
  being silently hidden by a future navigation redesign.

## Changed

- EDH Lab is now enabled after an existing native Scryfall catalog is opened
  at startup, after a successful Scryfall sync, after keeping a native catalog,
  and after opening a catalog manually.
- The EDH Lab sidebar entry stays disabled when no valid Scryfall catalog
  has been loaded, rather than opening a misleading empty workspace.

## Fixed

- Restored access to the existing Commander / Arena Brawl browser,
  **Build with selected commander**, and the v0.9.32 Deck Blueprint / role
  gap guidance.
- Removed an obsolete "EDH Lab remains backlog" state that disabled the
  launcher after a successful catalog sync.

## Removed

- Nothing. No changes to owned cards, catalog or printing identifiers,
  Deck Library contents, backups, themes, or updater installation.

## Manual verification

1. Update the running native app from v0.9.32 to v0.9.33 using Native Updates,
   or install the official checksummed ZIP side by side.
2. Confirm **EDH Lab** appears directly below **Deck Library** in the
   WORKSPACES sidebar.
3. With an already-synced native Scryfall catalog, confirm the entry is
   enabled after launching the app without manually reopening the catalog.
4. Open EDH Lab; choose a Commander, click **Build with selected commander**,
   and verify the Deck Studio blueprint and role shortcut appear.
5. Sync Scryfall catalog again and confirm EDH Lab remains enabled.
6. Verify the Deck Library and collection counts did not change from browsing.
7. Confirm a fresh install without a catalog shows a disabled entry until
   a valid Scryfall catalog is available.

Native CI regression checks are required; local display acceptance remains
outstanding.
