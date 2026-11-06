# Arcane Vault for Windows — v5.17.0

## Release overview

Set Progress can now measure either your Arena collection or your physical Magic card collection. The ownership source can be switched instantly while retaining the separate catalog-printing scope and the existing set drilldowns.

## Added

- **Arena / Physical ownership toggle in Set Progress.** Switch between separate ownership sources with a two-button selector. The app remembers the selection across launches.
- **Physical set completion.** Physical cards imported from collections such as ManaBox, Mythic Tools, or Moxfield count toward completion by unique card name. Different physical printings of the same card are combined for the ownership quantity, not double-counted toward set completion.
- **Physical Owned / Missing drilldowns.** Each set's Owned and Missing filters, card quantities, progress percentages, summary counts, sorting, and page exports now follow the selected source.
- **Independent read-only physical queries.** The Windows catalog service uses temporary, request-local physical ownership tables, never overwriting imported Arena ownership.

## Changed

- **Set Progress explanation.** The text now clearly states that completion uses the selected collection rather than always using Arena inventory.
- **Exports.** Set card CSV filenames identify the selected source, Arena or Physical, to avoid confusion.
- **Scoped navigation.** Set Progress remains in the Collection navigation group. The catalog scope selector remains independent: Arena-listed printings or All Magic printings can be selected regardless of which inventory is being measured.

## Fixed

- **Collection-source confusion.** Previously Set Progress always used Arena quantities even if the user had imported a physical inventory elsewhere in Arcane Vault.
- **Asynchronous switching.** Outdated set and card requests are ignored when switching the collection source so delayed responses cannot overwrite the newly selected view.

## Removed

- **No features removed.** The existing Arena set-progress workflow, catalog scope options, card detail links, and saved data remain intact.

## Important details

- **Completion is by unique card name, not set-specific physical printing ownership.** A card owned from another set counts toward any displayed set containing that name. The same semantics apply to Arena and Physical to support meaningful comparisons.
- **Inventories remain isolated.** Physical quantities never replace or update Arena quantities in the catalog database.
- **No data migration needed.** The toggle reads the existing local physical inventory; if none is imported, Physical completion displays zero-owned results.
- **The local Scryfall printing catalog is required.** Refresh or download the catalog in Card Browser if Set Progress has no results.

## Installing

Open **Import & Data → Application updates → Check for updates**, review the release notes, and choose **Download and install update**. As before, the Windows release ZIP is accompanied by a SHA-256 checksum.
