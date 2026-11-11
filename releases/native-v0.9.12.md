# Arcane Vault Native Windows v0.9.12 — Dark dropdowns, real card art and utilization roles

Windows x64 native preview; not a full 1.0 replacement for the shipping WebView app.

## Fixed
- **Unreadable white-on-white dropdown menus** across the native Windows application. Noneditable ComboBoxes now use the app's dark panel for closed selection and popup; menu item text, hover/selection, focus and disabled states have explicit contrast.
- Shared ComboBox/ComboBoxItem styles apply to native Collection, Card Catalog, deck editor filters and supporting windows. Editable deck-format ComboBoxes preserve the text editor and receive dark, readable popup menu items.

## Added
- **Card art view** in the main native Card Catalog. The previous text-only 'Cards / tiles' layout now displays the actual cached Scryfall **printing image**, alongside full card name, set, collector number, ownership counts and utilization roles. Double-click opens the existing full Card Details screen.
- Artwork loads **only for visible, virtualized rows**; image downloads use the existing HTTPS-only Scryfall CDN validator, bounded disk cache, 4-request concurrency limit, unload cancellation and small thumbnail decode. Text browsing does not prefetch images.
- **Utilization Tag** filtering with 18 role categories restored from the previous WebView heuristic: Ramp, Removal, Board Wipe, Discard, Counterspell, Card Draw, Card Selection, Lifegain, Tutor, Stax, Haste, Protection, Recursion, Sacrifice, Tokens, Graveyard Hate, Finisher, Equipment.
- Derived role tags appear in Detailed, Compact and Card Art modes. Filter by one role while combining existing set, rarity, type, color identity, Arena availability and paper/Arena ownership filters.
- Native CI tests for legacy-equivalent categories, multiple role matches, combined role filters, role-aware result limits, printing-specific art URLs, backward compatibility with catalogs missing Oracle/image fields, and immutable source catalog bytes.

## Changed
- The existing Scryfall `cards.oracle` and `cards.type` fields power utilization heuristics, and `variants.image` is the chosen exact printing art. No new ownership database fields, catalog migration, or extra metadata sync is needed for a recently synced native catalog.
- Older legacy catalogs without official Oracle text still browse normally; role filters explain that Full Scryfall Sync is necessary rather than inventing unsupported tags. Image-less printings use a visible fallback.

## Removed
- The former **text-only** Cards / tiles display is superseded by a proper **Card art** image view. Detailed and Compact text modes remain available.
- No catalog, native collection, recovery, saved-deck, legacy WebView or Android user data is deleted.

## Limitations
- Role tags are **rule-based suggestions inferred from Scryfall Oracle text**, not official Scryfall tags, guaranteed strategic roles, format legality or complete game-design classifications. Cards can have multiple roles; uncommon templating may need future heuristic improvements.
- The Card Art layout prioritizes responsiveness with one image-bearing result per virtualized row rather than loading a large unvirtualized grid of cards.
- An internet connection is required to fetch images not already cached; artwork and card metadata degrade independently of each other.
- Real-Windows dropdown interaction, image scrolling, large-catalog role query performance and data acceptance still require manual testing for the Windows 1.0 gate #62.
