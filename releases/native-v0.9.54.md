# Arcane Vault Native Windows v0.9.54 — Card Details readability

Follow-up to the v0.9.53 collection integrity and favorite-star release. This focuses on the Card Details layout at normal and smaller Windows window sizes.

## Added

- A **compact Your Ownership** summary below the **Card Art / Printings** controls in the left-hand card-details column, retaining the original owned count without any changes to inventory.
- Independent vertical scrolling for the left artwork/ownership column and the right metadata/legalities area.
- Native XAML regression assertions for ownership location, Oracle text placement and bounded metadata height.

## Changed

- Removed the large Your Ownership card from above metadata and Oracle text on the right.
- Constrained Card Metadata and the expandable Format Legalities area to a scrollable region (maximum 230 px) to preserve the Oracle Text area as the window is resized.
- Oracle Text now occupies the remaining right-column height with a minimum height of 190 px, retaining its own vertical scrollbar for long card text.
- Adjusted artwork height and button spacing slightly so the relocated ownership panel is easier to see at the default window height without sacrificing readable card art.

## Fixed

- Expanding or displaying a long format legality list no longer pushes Oracle Text almost entirely out of view in the Card Details window.
- Artwork and printing-related controls no longer compete with the Oracle Text column for vertical space.

## Removed

- No card fields, Oracle text, format legalities, ownership information, card-art gallery, bookmark behavior, or collection data have been removed.

## Safety

- XAML layout only; no collection, deck, cache, database, backup, metadata or updater changes other than version increment.
- Existing card selection, artwork loading, printing gallery, favorite and format validation controls keep their handlers.

## Local acceptance

1. Update to **v0.9.54** through Native Updates.
2. Open a card with a long Oracle Text and expand **Format Legalities**. The right-hand Oracle Text should remain readable and independently scrollable.
3. Verify **Your Ownership** now sits in a compact panel under the card artwork/printings/bookmark controls, within a scrollable left column.
4. Resize Card Details down toward its minimum size. Check you can scroll separately to artwork, ownership, legalities and the complete Oracle text.
5. Open cards with no metadata, double-faced/split names, and Arena-only artwork. Confirm no crash and no misplaced blank panels.
6. Verify ownership text and bookmark buttons remain accurate and usable; no card quantities or preferences change unless you explicitly act.
7. Confirm the catalog favorite stars from v0.9.53 remain centered.

Release gates: Windows WPF/native regression checks, portable checksummed ZIP and release verification, followed by user acceptance. Preview, not 1.0.
