# Arcane Vault — CORSAIR XENEON EDGE Dashboard (Preview 0.1)

Source for a native **iCUE widget**, not a Stream Deck plugin or replacement for the working Companion v0.9. Built for XENEON EDGE `dashboard_lcd` in 2560×720, responsive down to 840×344. No full-screen window or localhost server is required.

## Features in 0.1
- Read-only live monitoring of `%LOCALAPPDATA%\\ArcaneVault\\streamdeck.json`, the same native snapshot used by the Stream Deck Companion. iCUE's **File System Provider 1.0** delivers changes; the native app already publishes periodically.
- Touch previous/next deck selection with persistent widget-local deck ID; changes here do not alter Stream Deck's separate selected key.
- Commander art prefers Scryfall `art_crop`, then falls back to the full card, with explicit offline status.
- Confirmed Arena deck wins, losses and win percentage; latest match shown only when associated with selected deck; physical collection valuation.
- Empty, unavailable, stale and denied-access paths show non-blocking status text; previous valid snapshot stays visible.
- A safe browser-only **SAMPLE PREVIEW** fixture for visual development.

## Install (once packaged by the official CLI)
1. Have iCUE **5.52+**, XENEON EDGE, and Arcane Vault Native Windows **0.9.131+** running.
2. Use the CI widget packaging artifact or install the official CLI: `npm install -g icuewidget-cli`.
3. From the repo root: `icuewidget validate integrations/xeneon-edge` and `icuewidget package integrations/xeneon-edge`.
4. Open iCUE → XENEON EDGE → Screen Setup → Widget View → add/import the generated `.icuewidget` file.
5. Approve only the widget's declared **read access** to `<Runtime>/AppData/Local/ArcaneVault/streamdeck.json` and HTTPS image access to `cards.scryfall.io`.
6. For first load, leave Arcane Vault Native running; the File System Provider updates when the native snapshot next changes. If access is denied, permission can be adjusted within iCUE widget settings.

**Packaging caveat:** iCUE's CLI is required; arbitrary ZIP files renamed to `.icuewidget` are not supported and must not be distributed as verified widgets. GitHub Actions workflow `edge-widget.yml` packages with the official CLI.

## Limitations
- Prototype requires iCUE device validation; a packaged widget passing CLI checks is not equivalent to hardware testing.
- Current widget displays latest matched result but not historical trends (the native snapshot does not include a safe per-deck time series).
- Touch controls switch only this widget's selected deck. Explicit refresh writes are intentionally excluded: iCUE's File System Provider is **read-only**, and the native exporter already refreshes. No undocumented iCUE write APIs are used.
- Web-based artwork requires network image permission; widget remains useful without it.
- The widget never modifies a collection, invents a recorded win, or binds a physical card printing to an Arena art style.

## SDK references
- [Official Widget Specification](https://docs.elgato.com/icue/widgets/specification/)
- [File System Provider](https://docs.elgato.com/icue/widgets/references/plugins/file-system-provider/)
- [Widget Permissions](https://docs.elgato.com/icue/widgets/references/permissions/)
- [Official Widget CLI](https://docs.elgato.com/icue/widgets/)

## Next
Real XENEON EDGE tests for file watcher permissions, browser image loading, touch areas and DPI; then add explicit per-deck match history and set completion to a versioned native presentation snapshot.
