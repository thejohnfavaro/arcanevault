# Arcane Vault Native Windows v0.9.16 (Preview)

Collection and Arena presentation correctness update for the native Windows application.

## Added

- Selecting a Set Progress tile expands a full-height, artwork-rich **set details checklist** inside the main window. Use **← All sets** to return to the responsive set overview; no extra window opens.
- **Hide unowned physical printings** or **Hide unowned Arena card names** in a selected set. The toggle filters the list without modifying owned quantities or reported completion totals.
- Virtualized set thumbnails and collector-number checklist images using representative card art from the current official local Scryfall catalog. Display degrades gracefully when an older catalog has no image fields.

## Changed

- Arena catalog searches now show **one representative card per name**. When an explicit digital printing exists it is preferred; otherwise a base shared printing is shown.
- Arena card entries display name-based ownership without presenting paper set/collector identifiers as an owned Arena printing. The Arena artwork gallery shows one base-card preview, not an inferred list of purchasable/owned styles.
- Set Progress is navigated through its own sidebar destination and retains separate Physical (verified printing slots) and Arena (card-name coverage) accounting.

## Fixed

- Fix the "A Good Day to Die" pattern (TRK #111, #428, #438): Arena browsing shows one card by name and a single base-art preview. Paper set/collector IDs are not displayed as Arena holdings, and paper-only cosmetic availability is not inferred.
- Preserve name-only Arena quantities while preventing unverified alternate printing artwork from being treated as a purchased Arena card style.
- Set checklist remains virtualized at large catalog sizes and avoids expanding the source catalog or changing physical collection holdings.

## Removed

- Removed the old always-visible split Set Progress overview/checklist layout, replacing it with a dedicated in-place selected-set view.
- Removed duplicate Arena card-name tiles and paper collector-ID presentation from Arena-specific catalog searches. Individual paper printing rows remain available in physical/all-printing browsing.

## Testing notes

- In **Card Catalog → Owned on Arena**, search for **A Good Day to Die** and confirm a single card-name entry appears without physical collector metadata. The Arena-scoped artwork gallery presents a base-art preview only; styles are explicitly labeled *not synced*.
- In **Set Progress**, choose a physical set, verify the artwork thumbnails, use **Hide unowned physical printings**, and return via **All sets**. Repeat under Arena name coverage and confirm it makes no unsupported per-printing ownership claims.
- Displayed Set Progress artwork is **representative official card artwork**, not promotional set banner art or a claim about the exact owned printing. Official set symbols/promotional images and authoritative MTG Arena cosmetic ownership remain future parity tasks.
- This is a preview build, not Windows 1.0. Existing collections, saved decks, backup/recovery files, metadata and old application remain separate from executable version updates.
