# Arcane Vault Native Windows v0.9.63 — Docked Card Inspector & In-App Activity

## Added

- **Persistent Card Inspector in Card Catalog.** Select a card once in Card art, Detailed or Compact view to show its image, exact printing, owned copies, type, official color-identity mana symbols and Oracle text alongside the list. Selecting another card updates the same inspector, **without opening and closing a Windows dialog**.
- **Double-click to widen the inspector**, rather than automatically spawning another window. Use **Open full details & printings** only when you want the extended view and printing/artwork actions; **×** hides the inspector.
- **Stale-selection guards.** Metadata loads on demand from the read-only catalog snapshot. Rapid changes, new queries or catalog replacement cannot apply an old card's result to the latest selected card.
- **In-app notification toasts** with auto-dismiss (approximately 6.5 seconds), manual dismiss, and a persistent-for-this-session **Activity** history opened from the sidebar. The history retains up to 60 entries, including routine success/error/warning details, with no operating-system notification-center dependency.
- Shared nonmodal notice routing used across the main shell, Card Details, Deck Studio, printing gallery, shortlist, and physical/Arena collection editors.

## Changed

- Single-clicking a card in the catalog now updates the docked inspector and leaves browsing in place; card-art double-click and list double-click widen it.
- The inspector is a separate column beside the existing virtualized ListView, not a new window or a second card catalog. The card tiles remain fixed-size; the grid recalculates columns when the inspector is visible.
- Oracle text in the inspector uses the same official Scryfall symbols introduced in v0.9.62, with readable literal-token fallback when an SVG has not yet been cached.
- Routine **OK-only information and nonblocking warnings** now go into the native toast/activity UI. Action-dependent **Yes/No and other confirmations stay modal** to preserve safety for deletes, updates, imports, clears, restores and other protected operations.
- In-app activity belongs to the current process session and is deliberately not persisted into the collection SQLite database.

## Fixed

- Repeated opening/closing of independent Card Details windows interrupted catalog browsing and could accumulate windows.
- Informational message dialogs could steal keyboard focus and block ordinary browsing, even though they required no decision.
- A fast sequence of clicked cards could otherwise show metadata from an earlier request.

## Removed

- Automatically launching a standalone Card Details window on card double-click.
- The dependency on Windows MessageBox for migrated OK-only informational or warning notifications.
- No collection ownership, printing variants, backups, preferred art, decks, themes, shortlist or catalog records are removed.

## Windows acceptance

1. In **Card Catalog → Card art**, single-click a card. A right-hand inspector should open in the same main workspace; select another card to update it in place, with no new taskbar/window entry.
2. Double-click a card to widen the inspector, then select another card and continue browsing. Use **×** to reclaim the full catalog width; tiles should reflow without changing size.
3. Change search terms, rarity filters, and catalog pages while the inspector is open, then select a different card quickly. The final details must match the selected card; no stale asynchronous data.
4. Switch to **Detailed list** and **Compact list**, single-click several rows. The same inspector should update correctly, including official mana symbols and Oracle text.
5. Press **Open full details & printings** to verify the existing full Card Details window remains available intentionally for advanced actions.
6. Bookmark a card or check for updates with no newer version. The result should appear as a temporary in-app toast; open **Activity** in the sidebar to read past notices. Dismiss individual toasts, then **Clear history** to reset session notices.
7. Verify actual Yes/No confirmations for a proposed update and protected collection operations have **not** been downgraded to nonblocking toasts.
8. In Deck Studio and import/editor flows, check that nonblocking information/warnings surface in Activity without changing inventory or recovery behavior.
9. Confirm large collections still scroll smoothly and the virtualized 200-result catalog paging works. Inspectors fetch details only for the chosen card, not every off-screen result.

**Scope:** Native Windows preview v0.9.63. The toast/history system is a first-phase in-app notification center; the existing confirmation dialogs remain as explicit blockers for sensitive decisions. Automated tests cover structure, state cap, selection safeguards, and confirmation routing; Windows on-device UI and DPI acceptance remain necessary.
