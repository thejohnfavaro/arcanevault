# AVN-031 — Arcane Vault Next native visual system

## Design source

Use the **existing shipping WebView UI** as a design reference, particularly its CSS variables in `ArcaneVault.Desktop/Assets/Arcane_Vault_MTGA_Collection_v4_2.html`. Do not redesign the product as orange-themed just because the early WPF prototype used orange primary buttons. Preserve the recognizable Arcane Vault logo and dark-purple panels.

| Role | WebView source color | Native resource |
| --- | --- | --- |
| Background | `#0d1017` | VaultBackground |
| Sidebar | `#121722` | VaultSidebar |
| Panel | `#171c28` | VaultPanel |
| Raised panel | `#202737` | VaultPanelRaised |
| Borders | `#343d51` | VaultLine |
| Text | `#f0f1f6` | VaultText |
| Muted text | `#9da9bd` | VaultMuted |
| Accent | `#b59bfc` | VaultAccent |
| Secondary | `#7fcae1` | VaultTeal |
| Owned/good | `#8bd3ac` | VaultGood |

## Native UI changes

1. **App.xaml** holds shared colors and WPF control styles for rounded buttons, text input, tabs, lists, focus/selection and native meters. Edits and modal confirmations inherit the same theme.
2. **MainWindow.xaml** reorganizes previous crowded WrapPanel commands into the original grouped left sidebar. Catalog search sits above a visually enriched, virtualized printing list, with a branded banner and a clear data/status footer. Every named original command remains wired.
3. **NativeCollectionWindow.xaml** presents accurate physical/Arena/deck/art summaries and verified-vs-unresolved totals in cards; physical editing is contextual to its tab, Arena and deck actions remain separate. Deck library is styled as efficient virtualized deck-box rows. Set Progress renders the exact verified collector-number slot fraction as native progress meters.
4. **NativeCardDetailsWindow** displays a prominent card preview alongside inventory/printing ownership, metadata and Oracle text. Scryfall URL is optional and read from the already local catalog; only trusted HTTPS `cards.scryfall.io` can be fetched, with bounded local disk cache. The card text is fully usable offline; artwork is decorative and may not match the selected physical printing.
5. Existing add/edit/delete, quantity, fresh start, migration, backup, and recovery code paths remain unchanged. No browser is embedded for these UI screens.

## QA and open fidelity items

- Compile all WPF screens, preserve event handlers and x:Names, retained virtualization for large lists and source-verified Set Progress percentages.
- Confirm keyboard focus, selected row states, legibility, scroll behavior, data binding and cancellation at common Windows DPI/scales and short-window sizes. Synthetic CI cannot verify rendered pixels.
- Compare deck boxes, catalog art grids, optional outfit-like cover customization, iconography and promotional set image backgrounds in later UI polish. Do not add unbounded network images to virtualized lists.
- Performance: scrolling must not request remote images; Card Details only makes one optional official CDN request per uncached card, constrained to 8 MiB and 15 seconds. The cache is presentation-only and bounded to roughly 160 MiB.
- Release remains a preview pending user-device acceptance and signed/versioned installer/updater; do not replace the old installed Arcane Vault automatically.
