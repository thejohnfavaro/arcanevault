# Arcane Vault — CORSAIR XENEON EDGE Dashboard (Preview 0.9)

Source for a native **iCUE widget**, not a Stream Deck plugin or replacement for the working Companion v0.9. Built for XENEON EDGE `dashboard_lcd` in 2560×720, responsive down to 840×344. No full-screen window or localhost server is required.

## Features in 0.9
- Read-only live monitoring of `%LOCALAPPDATA%\\ArcaneVault\\streamdeck.json`, the same native snapshot used by the Stream Deck Companion. iCUE's **File System Provider 1.0** delivers changes; the native app already publishes periodically.
- Touch previous/next deck selection with persistent widget-local deck ID; changes here do not alter Stream Deck's separate selected key.
- Commander art prefers Scryfall `art_crop`, then falls back to the full card, with explicit offline status.
- Confirmed Arena deck wins, losses and win percentage; account-level Constructed and Limited rank snapshots in place of the old Latest Match panel; physical collection valuation.
- Last 20 confirmed match outcomes for the selected deck, with touch-friendly W/L timeline and rolling up-to-ten-game win-rate sparkline (requires native v0.9.133+).
- Read-only physical Set Progress based on independently verified catalog collector-number slots, with up to four highest-owned sets (requires native v0.9.132+). Missing data displays unavailable, not invented numbers.
- Polished wide-screen panels, reduced unnecessary image redraw when only statistics change.
- Empty, unavailable, stale and denied-access paths show non-blocking status text; previous valid snapshot stays visible.
- No synthetic preview fixtures are present in the installed widget. Missing native data is shown as unavailable.

## New in 0.9
- **Physical Set Progress now owns the remaining height** of the right-side column and its set rows scroll independently. The set selector stays visible even when many sets are displayed.
- Current Rank badge cards have less wasted padding. The observed rank timestamp remains available as an expandable disclosure; reset predictions and Wizards verification details remain one tap away.
- The rank reset countdown remains always visible, with the official MTGA tier badges preserved.
- Collection value moves into a compact single-line summary below Set Progress, and Connection becomes a slim bottom status with readable errors.
- The new list can receive keyboard focus for scroll navigation, and hidden-panel layout preferences remain compatible.
- No changes to the Windows exporter, saved data, deck W/L or Arena rank sources. Compatible with Native v0.9.151+.

## Previous 0.8 improvements
- The Current Rank panel now displays the **verified next Arena ranked season end** and a countdown that advances locally between snapshots.
- When the official reset table explicitly contains the current Constructed or Limited tier, the panel shows the **conditional** post-reset rank (only if the current rank is also the final season rank).
- Windows Native v0.9.151 checks Wizards' [ranked-season page](https://magic.wizards.com/en/news/mtg-arena/ranked-season) on startup and every 12 hours while running (retrying network failures hourly), and saves its last verified future schedule locally for offline use.
- When the schedule expires, the widget says **Awaiting official date** rather than assuming a monthly date, showing a negative countdown, or inventing next season's reset rules.
- The UI displays when the official schedule was last verified, including its underlying source; no separate widget network permission is needed.

## Previous 0.7 improvements
- Fixed widgets remaining stuck at **Waiting for Arcane Vault** after installation, even while the Windows app was running. The iCUE File System Provider notifies consumers of actual content changes; it does not guarantee an initial read when subscribing. Windows v0.9.149 adds a changing `publishedUtc` heartbeat every ~15 seconds, making a real file-change notification possible without needing a new match.
- The widget retries a stalled file subscription and clearly reports a lack of file events or missing iCUE file permissions. These changes do not alter the collection or match history.
- The official CLI build includes an automated simulated watcher regression: first snapshot, account rank rendering, stale watcher recovery.
- To diagnose on Windows: check that `%LOCALAPPDATA%\\ArcaneVault\\streamdeck.json` exists and its modified timestamp advances after Arcane Vault opens. In iCUE check that the widget is granted read access to that file.
- This release requires **Windows Native v0.9.149+** for reliable first-paint heartbeat delivery. The v0.6 MTGA PNG badges are retained without change.

## Previous 0.6 badge improvements
- The Current Rank panel now uses **actual MTG Arena rank badge images** from the [Draftsim Arena ranking guide](https://draftsim.com/mtg-arena-ranking-system/) instead of the v0.5 custom Arcane Vault tier shields.
- Separately mapped Constructed and Limited PNGs for each division (Bronze–Diamond, Tier 4 through Tier 1), plus Mythic and Unranked: **44 distinct rank icons**.
- Badge art is included in the packaged widget for offline display. Text remains next to each icon and unknown or incomplete rank information never selects an arbitrary division.
- The build retrieves the source images from Draftsim with PNG-signature and size verification. **These are third-party MTGA images, not original Arcane Vault assets.** The public page does not itself establish a redistribution license; verify appropriate permissions before publishing more broadly.
- Windows Native v0.9.148+ continues to provide the account rank snapshot; no new Windows app version or Stream Deck plugin update is required.

## Previous 0.5 features
- The v0.5 custom icon set is superseded and excluded from the packaged v0.6 widget.
- The Current Rank panel preserves its captured timestamp and independent Constructed/Limited data.

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
