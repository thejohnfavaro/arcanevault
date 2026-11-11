# Arcane Vault Native Windows v0.9.15 (Preview)

Windows native collection parity and reliability update. Existing native collections, preferred artwork, deck data, and recovery points are preserved across the versioned executable update.

## Added

- A dedicated **Set Progress** entry in the main window sidebar.
- Responsive 1–3-column Set Progress tiles, with virtualized rows and click-to-inspect set checklists.
- Independent **Physical (verified printing slots)** and **Arena (card-name coverage)** Set Progress scopes, with separate name-based checklists for Arena-available sets.

## Changed

- Set Progress filters wrap to fit narrower windows; counts and explanations change with the selected collection scope.
- Arena Card Details prefer artwork from an Arena-available digital style, falling back only to another Arena-available style.
- Arena **Printings & Artwork** limits its gallery to Scryfall printings marked as available in Arena and labels name-based ownership accurately.
- Arena card-name coverage is clearly identified as a different metric from verified paper-printing completion. Names printed in multiple Arena sets may appear in each set's coverage; this never establishes owning a specific set printing.

## Fixed

- Removed lingering **Arena N** quantity badges from Card Catalog results without changing owned Arena quantities.
- **Owned on Arena** and **Not owned on Arena** searches now exclude paper-only printings, even where the card name is owned in Arena.
- Arena Collection card details no longer silently substitute paper-only artwork when no Arena-specific art is available.
- Arena Set Progress card details preserve name-only semantics and restrict artwork to Arena-available styles.
- Regression coverage for owned-name filtering, Arena gallery scope, distinct Arena card-name counting, physical ownership isolation, and the OneWay percentage binding.

## Removed

- Removed paper-only styles from Arena-context artwork gallery results and removed implicit display of paper-only variants under Arena-owned search scopes.

## Preview verification

Use the in-app **Check for Updates** flow to stage this version separately from previous native executables. Confirm the Set Progress sidebar navigation, switching scopes, responsive set cards, Arena search results, and Arena artwork choices on your Windows device. Automatic tests are not a substitute for the final hands-on 1.0 feature-parity and artwork acceptance pass.
