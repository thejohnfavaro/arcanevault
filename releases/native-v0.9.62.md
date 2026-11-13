# Arcane Vault Native Windows v0.9.62 — Card Details fit + official Magic symbols

## Added

- **Official mana and rules symbols:** Card Details resolves Scryfall's `/symbology` mapping and draws the EXACT official Magic symbol SVGs through SharpVectors' native WPF renderer. Color identity (W/U/B/R/G and C) and all recognized Oracle-text tokens use the same source, including generic costs, tap, hybrid and Phyrexian symbols. This REPLACES the previously hand-drawn approximations.
- **Oracle text with inline symbol artwork:** e.g., `{2}{W}{U}`, `{T}`, `{W/U}` and `{B/P}` render as small real pictograms where Scryfall has an official symbol. New **Copy exact text** button preserves the exact original Oracle string with all curly-brace symbols and line breaks. Unknown tokens stay visible as text.
- **Cache for offline reuse:** Downloaded official SVGs and the trusted Scryfall symbol map are saved under local application data. Previously loaded glyphs remain available offline. If an icon has never loaded and the connection is unavailable, the literal token appears rather than a simulated or misleading icon.
- **Screen-aware opening:** Card Details measures its owner's monitor working area in device-independent pixels before opening. The dialog opens centered at an appropriately bounded size (up to 1160 × 920 DIPs) rather than assuming every display fits one hardcoded window.
- Pure regression coverage for monitor sizes, artwork scaling, Oracle tokenization, exact copied text, official SVG URI trust and blocked lookalike hosts.

## Changed

- Artwork height adjusts between 185 and 445 DIPs with window height. On laptop-sized monitors the art shrinks before ownership or essential controls are cut off.
- **Browse printings & artwork** and **My Shortlist** actions remain in a fixed footer and are visible when Card Details opens. Artwork/ownership, color identity/legality metadata, and Oracle text scroll independently as needed. The Oracle panel keeps its own minimum vertical space.
- Card Details has a larger comfortable desktop default, but clamps to the current monitor. Long card names wrap and remain accessible via tooltip.
- Official SVG rendering happens only in the details dialog, with bounded network requests and no per-row Card Catalog SVG downloads; Card Catalog virtualized image performance is unchanged.
- Native symbol renderer uses SharpVectors.Wpf 1.8.6 (BSD-3-Clause). No new font assets or source-image approximations are bundled.

## Fixed

- On opening Card Details, the fixed 445-DIP art preview and stacked action buttons could push ownership and controls below the visible area, requiring manual resizing.
- Color identity previously showed hand-constructed facsimiles instead of official WUBRG symbols.
- Oracle text previously left `{W}`, `{U}`, `{T}`, etc. as raw text even when the real symbols existed.
- Small/secondary-monitor windows are clamped to the monitor working area instead of going offscreen or requiring adjustments.

## Removed

- The old custom-made white sun, blue drop, black skull, red flame, green tree and colorless diamond silhouettes.
- No cards, collection quantities, deck data, preferred artwork, shortlist entries, Scryfall catalogs, recovery points or theme settings were removed.

## Verify on Windows

1. Open Card Details at normal and maximized main-window sizes, first on your primary monitor, then on a second monitor if available. Confirm all action buttons remain accessible, ownership is visible, and the Oracle pane has space without resizing.
2. Try a 900×600-DIP or similarly constrained display: artwork should shrink; independent scroll areas expose longer printings, metadata, legalities and Oracle text.
3. Open a mono-color, multicolor and colorless card; verify color identity uses recognizable official WUBRG/C symbols (not the old handmade icons).
4. Open a card with `{T}`, `{2}`, `{W/U}`, and `{B/P}` in Oracle text. Verify official SVG shapes appear inline. **Copy exact text** must preserve every literal brace symbol and line break.
5. Test after cached glyphs are loaded, then while offline; loaded official symbols should remain visible, while missing ones remain readable as their original `{W}`-style tokens.
6. Ensure unknown or uncommon tokens are not guessed. Source URLs must resolve exclusively to Scryfall's official HTTPS card-symbol CDN, never user-provided links.
7. Exercise bookmarking, printing gallery, preferred artwork, and color-identity labels. Neither browsing nor copying should modify ownership or decks.

**Scope:** Native Windows v0.9.62 preview. CI verifies build, safe parsing, and shell/layout wiring; runtime network SVG retrieval and actual DPI-specific presentation still require on-device acceptance. Some symbols may not be available offline until downloaded once.
