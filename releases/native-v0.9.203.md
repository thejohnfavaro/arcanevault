# Arcane Vault Windows v0.9.203 (Preview)

## Added
- A saved-deck selector in Match Analytics for independently verified play-versus-draw results. It requires an exact recorded native deck ID and leaves missing/ambiguous results unassigned.
- A layperson-friendly `tools/Run-GuidedChecks.ps1` menu for deck artwork, Arena tracking, deck editing, data safety, or the full existing native regression suite. Reports are stored in a temporary folder and can be shared for troubleshooting.
- Regression fixtures for exact deck-scoped play/draw evidence and Arena-format Standard card inspector artwork resolution.

## Changed
- Card inspector metadata queries strip Arena's `(SET) collector` display suffix only for catalog lookup, retaining the untouched saved deck card text.
- Standard deck card inspector artwork uses the same official Scryfall printing-aware resolver as saved deck covers and the Xeneon Edge exporter.

## Fixed
- Cards such as `Optimistic Scavenger (DSK) 21` can now display metadata and official artwork in the saved deck's card inspector instead of leaving `Artwork not cached` when a catalog image exists.
- Deck-specific on-play/on-draw views do not pool results from different saved decks.

## Removed
- Nothing. Existing collection, Arena match history, saved deck identity, printer selections and verified W/L results remain unchanged.

## Verification
- Run `pwsh -File tools/Run-GuidedChecks.ps1 -Area MatchTracking`, `-Area DeckArtwork` and `-Area DeckEditing`.
- Confirm existing match W/L attribution is unchanged; verify the per-deck play/draw view excludes unverified matches without native IDs.
- In Deck Library select a Standard card with `(DSK) 21` formatting and confirm the card inspector's artwork and rules appear. Offline Scryfall catalog must contain the printing.
- Build and run native Windows regression checks via GitHub Actions. This is a preview; no real-machine visual acceptance is implied.
