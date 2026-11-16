# Arcane Vault × CORSAIR XENEON EDGE integration plan

Status: planning, November 2026. This does **not** imply that the widget or dashboard is implemented.

## Verified target

- CORSAIR XENEON EDGE 14.5-inch touchscreen, 2560×720.
- Official iCUE XENEON EDGE widget platform on Elgato Marketplace (May 2026). An official Stream Deck widget also lets the EDGE host Elgato's Virtual Stream Deck actions.
- Official references:
  - https://www.corsair.com/us/en/explorer/gamer/monitors/how-to-use-elgato-virtual-stream-deck-on-xeneon-edge/
  - https://www.corsair.com/newsroom/press-release/corsair-launches-open-widget-platform-for-the-xeneon-edge-14-5-lcd-touchscreen-on-elgato-marketplace
  - https://www.corsair.com/us/en/explorer/gamer/monitors/corsair-xeneon-edge/

## Two complementary implementations

### Phase A: immediate compatibility through Virtual Stream Deck

Reuse **Companion v0.9+**: Deck W/L, button-press deck cycling, latest match, collection value, refresh stats and open application. Configure a dedicated Virtual Stream Deck layout on the EDGE. Reuse its existing local `%LOCALAPPDATA%\\ArcaneVault\\streamdeck.json` snapshot without changing native inventory or match-tracking contracts. This is possible today, subject to actual device verification. The official iCUE Stream Deck widget may be preferable for users already running iCUE.

### Phase B: purpose-built wide XENEON EDGE dashboard/widget

Investigate and implement an iCUE widget using the official XENEON EDGE widget SDK. Keep all card/collection processing in Arcane Vault Native; make the EDGE surface presentation-only with bounded, trusted image paths and explicit interactions. If SDK constraints impede data access, use a native Windows 2560×720 touchscreen companion window instead of introducing undocumented iCUE APIs.

**Default layout sketch, landscape 2560×720:**

| Region | Purpose |
| --- | --- |
| Left (~33%) | Selected commander art crop, deck name, format, deck picker |
| Center (~33%) | Large W-L, percent, most recent match and trend over time |
| Right (~34%) | Last confirmed match, total collection value, per-set progress shortcut, refresh action |

Touch targets ≥56 device-independent px, keyboard/mouse navigation, high-contrast text, no tiny printings. When an artwork URL is absent or a network request fails, show named fallback and a diagnostic; never spin indefinitely.

## Data and interaction contract

- Version the snapshot (v2) rather than mutate fields required by existing Companion v0.9.
- Deck identity: stable native deck ID (not display name); active Arena deck ID only when confirmed.
- Read-only deck fields: id, name, commander, art-crop URL/local cached image, wins, losses, win percent, record coverage status, last-match timestamp.
- Collection read-only summary: physical collection value, currency, valuation age, set completion summary.
- Commands: select deck, next deck, refresh now, open Arcane Vault. Commands should use explicit acknowledgement, per-command nonce, bounded file/IPC channel, no direct edit to inventory databases.
- Share the same card image download/cache with Stream Deck tiles; prefer official Scryfall art crop; no embedded remote HTTP server required for a local device.
- Verify DPI scaling, landscape aspect ratio, iCUE widget boundaries and sleep/reconnect lifecycle.

## Milestones and acceptance tests

1. **Compatibility proof:** show existing Deck W/L action on the EDGE using Virtual Stream Deck and verify cycling, retained selection and refreshed artwork.
2. **SDK spike:** confirm current iCUE widget APIs, packaging, security requirements and render dimensions from official SDK documentation. Do not assume Stream Deck plugins are automatically iCUE widgets.
3. **Native data layer:** add explicit snapshot v2 / status codes and fixture-based regression tests for confirmed vs unmatched matches, empty inventory and missing art.
4. **Wide layout prototype:** test art-crop/record/dashboard at 2560×720 and small-size responsive layout; ensure all tap regions and fallback states work.
5. **Release:** package independently of Native Windows app; test with actual EDGE+iCUE and preserve Companion v0.9 key cycling.

## Non-goals

- No background collection changes, deck rewriting or inferred wins.
- No assumption the EDGE is connected/owned or iCUE installed.
- No claims of a working bespoke iCUE widget until its SDK and real-device behavior are verified.
