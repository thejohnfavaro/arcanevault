# AVN-046 — Native theme contrast, Card Art and utilization tags

## What changed

The native Windows app's dark tokens were already in use, but WPF's stock ComboBox popup remained themed by Windows and rendered pale backgrounds against inherited light text. `App.xaml` now defines centralized ComboBoxItem and non-editable ComboBox dropdown templates with explicit dark surfaces, light readable content and visible selected/hover/focus states. Editable format boxes retain their text editing behavior; their choices use the same dark items.

**Card Art view:** switch the main Catalog's view dropdown from Detailed or Compact to **Card art**. Each row shows an actual printing image from Scryfall `variants.image`, name, code, collector number, verified paper/Arena/unresolved ownership labels and role tags. On-demand images use the existing restricted `cards.scryfall.io` loader/cache. A virtualized vertical panel only realizes visible rows; at most four concurrent image requests, canceled when a row is unloaded or recycled. The previously used non-virtualized WrapPanel is no longer used for this view.

**Utilization roles:** the new **Utilization Tag** dropdown restores the 18 named role classes from the WebView app. Computed from locally cached `cards.oracle` and `cards.type` without a network call, roles can overlap: e.g., Treasure production can be Ramp and Tokens. Filter one role in combination with existing name, set, rarity, type, color, Arena availability or owned/unowned filters. Tags appear in all three views.

These role labels are **heuristics, not official Scryfall tags or automatic deck recommendations**; this is why role filtering uses the recorded local Oracle text and never modifies card ownership. The rule set approximates the older WebView `deriveRoles` logic, but tricky card text and newer mechanics can produce ambiguous or missing tags. This is visible data for browsing, not a validated card taxonomy.

## Backward compatibility and safety

- No schema migration: use the already cached `cards.oracle`, `cards.type` and printing-level `variants.image`. If legacy SQLite lacks Oracle text, role filters fail safely with an explicit Full Scryfall Sync instruction; browsing and other applicable filters still work.
- The original indexed prefix-name search remains available. With a role filter, results are limited to the **first 200 matching entries**, not a random 200 before role matching, so results remain consistent. That query may be slower on a very broad catalog and needs realistic Windows profiling.
- Card image downloads are read-only user-cache activity; they do not alter collections, deck state, native SQLite catalog or recovery records. Image failures show fallback text, not broken inventory.
- Automated tests exercise role tagging/overlap, SQL filter combinations, card art metadata, old schema, result limits and preserved SQLite source SHA.

## Required real-device smoke test

Open Windows preview v0.9.12; expand rarity, format, view, ownership, sorting and utilization ComboBoxes; verify item contrast, mouse and keyboard navigation, selection and scrolling at 100%, 125% and 150% scaling. Switch Catalog to **Card art**; scroll quickly, switch back to Compact, open card details and try offline mode. Filter by Ramp, Board Wipe and Tutor on a real Scryfall bulk catalog, and check role accuracy on complex cards. Run an ownership backup comparison; app data must be unchanged.

## Still open

More complete native Deck Studio/Library picker, Arena logs/Match Performance, Deck Lab, Strategy, Wildcard Planner, Archetypes/Synergy and transfer/Settings/Data & Sync parity remain tracked in [#62](https://github.com/thejohnfavaro/arcanevault/issues/62). Do not retire the current WebView Windows app until the user approves real-device parity.
