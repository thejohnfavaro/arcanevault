# Native Windows — Official mana icon audit (v0.9.78)

Related: [Issue #167](https://github.com/thejohnfavaro/arcanevault/issues/167)

## Rendering contract

Official artwork is served from Scryfall's symbology endpoint and SVG CDN, using the existing trusted-URL boundary and local cache. The renderer is **not** a home-drawn approximation. Missing/offline assets show the **literal {token}**, not a fake icon. A common `NativeManaStrip` now wraps the existing `NativeManaSymbol` for two **distinct** semantics:

- **Casting cost**: preserve every Scryfall token and its order/meaning, including `{2}{U}` as *one generic-two* plus *one blue* symbol, **not** two blue pips; also `{W/U}`, `{2/U}`, `{U/P}`, `{C}`, `{X}`, `{S}`, `{T}`, `{Q}`, and double-faced ` // ` separators.
- **Color identity**: the canonical, unique W/U/B/R/G subset in Magic order, or colorless when metadata is explicitly empty/colorless. Mana costs, lands' produced mana, and identity remain conceptually separate. Invalid identity metadata renders an **Unknown identity** text fallback; do not infer a legal color.

## Visual surface inventory

| Native Windows location | State after v0.9.78 | Safeguard |
|---|---|---|
| Main Catalog, W/U/B/R/G/colorless filters | **Replaced raw WUBRG letter checkbox labels** with official native pips plus accessible color names | Existing checkbox filters and query semantics preserved |
| Commander Browser, identity filter | **Replaced raw letters** with official pips plus names | Exact/contains-any/contains-all unchanged |
| Commander Browser, result list | **Replaced raw concatenated WUBRG list label** with official pips | List stays virtualized; no artwork scan |
| Commander Browser, selected inspector | Existing official identity/Oracle pips retained; **exact casting cost added** using local catalog and stale-result guard | Selected card still previews only, never adds |
| Main Catalog, persistent inspector | Existing official identity and Oracle retained; **exact casting cost added** | No printing/ownership writes |
| Card Details dialog | Existing official identity and Oracle retained; **exact casting cost added** | Missing `mana_cost` in an older catalog gives blank cost, not an invented glyph |
| Deck Library, persistent saved-card inspector | Existing official identity and Oracle retained; **exact casting cost added** | No deck, pricing, or ownership edits |
| EDH Lab Cards/mainboard preview | Existing official Oracle retained; **exact casting cost and color-identity icon strip added** instead of the raw `Identity: WU` string | Pending metadata does not falsely show a colorless pip |
| Deck Analytics / mana planner explanatory text | Labels such as `W: 5` / color **source counts** remain numeric **data**, not mana-cost pips | Do not rewrite source counts as mana costs |
| Existing Card Details / Deck Library / Commander Oracle text | Retain `NativeOracleSymbolTokenizer` + `NativeManaSymbol` official symbology | Literal card text, split symbols and unknown tokens preserved |

## Regression acceptance

- `NativeManaStrip.Pieces` tests assert that `{2}{U}` and `{U}{U}` remain different, plus hybrid, two-brid, Phyrexian, snow, tap, untap, colorless and X symbols, WUBRG order and distinct identity.
- Source/XAML tests check all twelve Catalog/Commander color filters contain the official WPF icon, not `Content="W"`; confirm exact-cost bindings and cache paths in all inspectors.
- Catalog metadata tests verify true `mana_cost` when present and old-schema fallback when absent.
- Native .NET10 compatibility/WPF compilation and Windows packaging must pass; local device regression still needed on offline startup, color themes, long/split cost, and 100/125/150% DPI.

## Next visual checks

The upcoming **commander color identity → land suggestion UI** must render mana-production ability labels separately from legal identity; this will extend the mana-source color icon treatment beyond numeric plan summaries. The **deck-specific printing picker** will reuse canonical symbol controls without changing a card's rules from its chosen art. This audit does not assert that source-count textual tables are themselves mana costs or that handwritten/custom physical cards have an official Scryfall token set.
