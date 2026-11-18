# Arcane Vault — CORSAIR XENEON EDGE Dashboard (Preview 0.5)

Source for a native **iCUE widget**, not a Stream Deck plugin or replacement for the working Companion v0.9. Built for XENEON EDGE `dashboard_lcd` in 2560×720, responsive down to 840×344. No full-screen window or localhost server is required.

## Features in 0.5
- Read-only live monitoring of `%LOCALAPPDATA%\\ArcaneVault\\streamdeck.json`, the same native snapshot used by the Stream Deck Companion. iCUE's **File System Provider 1.0** delivers changes; the native app already publishes periodically.
- Touch previous/next deck selection with persistent widget-local deck ID; changes here do not alter Stream Deck's separate selected key.
- Commander art prefers Scryfall `art_crop`, then falls back to the full card, with explicit offline status.
- Confirmed Arena deck wins, losses and win percentage; account-level Constructed and Limited rank snapshots in place of the old Latest Match panel; physical collection valuation.
- Last 20 confirmed match outcomes for the selected deck, with touch-friendly W/L timeline and rolling up-to-ten-game win-rate sparkline (requires native v0.9.133+).
- Read-only physical Set Progress based on independently verified catalog collector-number slots, with up to four highest-owned sets (requires native v0.9.132+). Missing data displays unavailable, not invented numbers.
- Polished wide-screen panels, reduced unnecessary image redraw when only statistics change.
- Empty, unavailable, stale and denied-access paths show non-blocking status text; previous valid snapshot stays visible.
- No synthetic preview fixtures are present in the installed widget. Missing native data is shown as unavailable.

## New in 0.5
- Six **original Arcane Vault rank emblems** bundled offline for Bronze, Silver, Gold, Platinum, Diamond, and Mythic. These are not official MTG Arena badge files.
- Constructed and Limited each show a colored icon beside their verified tier and division; Mythic displays no division.
- Unknown rank values keep their existing text but display no emblem. Rank update time still indicates the last verified Arena snapshot.
- Requires Windows Native v0.9.148+ for rank data; the existing Stream Deck integration is unchanged.

## Earlier 0.3 improvements\n- Touch **Settings** opens panel visibility toggles for match history, current rank, collection valuation, and set progress. Visibility is retained on this Edge locally. **Restore defaults** re-enables all four.\n- The **Physical Set Progress** panel has a full set dropdown and previous/next set buttons. Selected set code is persisted locally; the default view still displays the top four sets.\n- Native v0.9.133 provides a bounded set roster of up to 2,000 physical sets; older snapshots gracefully retain only the existing top-four display.\n- The Edge remains strictly read-only: navigation and layout change only widget-local preferences, never the native ownership ledger.\n\n## Install (once packaged by the official CLI)
1. Have iCUE **5.52+**, XENEON EDGE, and Arcane Vault Native Windows **0.9.132+** running.
2. Use the CI widget packaging artifact or install the official CLI: `npm install -g icuewidget-cli`.
3. From the repo root: `icuewidget validate integrations/xeneon-edge` and `icuewidget package integrations/xeneon-edge`.
4. Open iCUE → XENEON EDGE → Screen Setup → Widget View → add/import the generated `.icuewidget` file.
5. Approve only the widget's declared **read access** to `<Runtime>/AppData/Local/ArcaneVault/streamdeck.json` and HTTPS image access to `cards.scryfall.io`.
6. For first load, leave Arcane Vault Native running; the File System Provider updates when the native snapshot next changes. If access is denied, permission can be adjusted within iCUE widget settings.

**Packaging caveat:** iCUE's CLI is required; arbitrary ZIP files renamed to `.icuewidget` are not supported and must not be distributed as verified widgets. GitHub Actions workflow `edge-widget.yml` packages with the official CLI.

## Limitations
- Prototype requires iCUE device validation; a packaged widget passing CLI checks is not equivalent to hardware testing.
- Per-deck history only includes confirmed Arena wins/losses already associated with unique saved decks. A zero-match record or absent timeline does not imply all games have been counted.
- Set Progress represents catalog paper collector-number slots, not booster completion or Arena-printing ownership. Summary generation can lag behind the first snapshot and remains optional.
- Touch controls switch only this widget's selected deck. Explicit refresh writes are intentionally excluded: iCUE's File System Provider is **read-only**, and the native exporter already refreshes. No undocumented iCUE write APIs are used.
- Web-based artwork requires network image permission; widget remains useful without it.
- The widget never modifies a collection, invents a recorded win, or binds a physical card printing to an Arena art style.

## SDK references
- [Official Widget Specification](https://docs.elgato.com/icue/widgets/specification/)
- [File System Provider](https://docs.elgato.com/icue/widgets/references/plugins/file-system-provider/)
- [Widget Permissions](https://docs.elgato.com/icue/widgets/references/permissions/)
- [Official Widget CLI](https://docs.elgato.com/icue/widgets/)

## Next
Real XENEON EDGE tests for file watcher permissions, browser image loading, touch areas and DPI; then expand into collection-value history and interactive time ranges after hardware validation.
