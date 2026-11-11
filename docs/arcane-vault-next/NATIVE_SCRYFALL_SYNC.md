# AVN-027 — Native Scryfall bulk catalog

## Scope and user flow

Launch standalone `ArcaneVault.Next.Desktop.exe` and click **Download / update Scryfall catalog** on the main screen. Approve the bulk data download; it can consume hundreds of megabytes and temporary disk space. **Cancel Scryfall sync** may be used during download or indexing. The native catalog survives app restarts; a fresh install needs no legacy app or exported catalog.

After completion, browse card details and physical printings in the same window. Open a new/fresh native collection and choose **Add physical printing** to add an exact Scryfall UUID, set and collector number. Arena collection editing does not depend on the catalog.

## Data contract

- Official Scryfall `https://api.scryfall.com/bulk-data` manifest; pick `default_cards` (English-first unique printings, unlike the one-name-only Oracle export). Accept a validated HTTPS `jsonl_download_uri` when present or older `download_uri`, restricted to `data.scryfall.io` and `data.scryfall.com`.
- One bulk file streamed to private scratch and parsed incrementally. Recognize gzip magic and parse newline-delimited JSONL or a JSON array; do not deserialize the entire bulk dataset in memory. User-agent and Accept headers identify this app. A full catalog must have at least 10,000 printings and 2,500 distinct names before becoming active.
- Import to new private SQLite tables compatible with the installed application's Scryfall catalog: `cards(name,type,oracle,rarity,mv,colors,image,uri,legalities,layout,setcode,arena)`, `variants(print_id,name,setcode,setname,collector,rarity,image,image_back,art_id,games,arena,digital,promo,full_art,border,release,url)`, and `state(bulk_updated_utc)`.
- Print IDs, paper-only/digital flags and set/collector numbers are **not** estimated from card names. Card Details, Set Progress and physical collection search reuse existing native readers; missing catalog entries remain unresolved.
- Build catalog indexes and complete SQLite quick_check/readback *before* installation. If existing catalog present, atomically replace with full previous catalog saved to `native-catalog.catalog-rollback-*.sqlite` and verify new installed totals. No overwrite/delete of old recovery files. If no catalog, move verified new file without replacing anything.
- The native ownership workspace `native-workspace.sqlite`, all decks/artwork and the installed Windows app profile are **never read or written** by catalog download/index/rebuild. The separate app-owned catalog is the only published output.

## Reliability checks

Synthetic CI uses an offline array and gzipped JSONL fixture, official HTTP manifest+download responses through an in-process fake handler, no-change timestamp skip, invalid and untrusted source URLs, malformed and too-small data, canceled operation, simulated HTTP 503, old-catalog rollback hash preservation, physical printing selection, Arena-only exclusion, Card Details and Set Progress parity. **CI does not prove connectivity to Scryfall from the user's own Windows device**; perform an actual network sync, check retained user-owned printing IDs and metadata, restart, and verify rollback once before replacing the shipping app.

## Remaining Windows work

Native deck authoring, scanner, price refresh, packaging/signed installer and in-app update, real-device startup/search benchmarks and migration acceptance. Android and EDH Lab remain lower priority until Windows parity is established.
