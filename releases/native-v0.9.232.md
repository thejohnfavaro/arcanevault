# Arcane Vault Windows v0.9.232 (Preview)

## Added
- Regression checks requiring clear, concise recommendations and export descriptions in both Windows deck editor layouts.

## Changed
- Shortened approximately forty tooltips, labels and helper messages in the saved-deck editor and embedded EDH Lab draft.
- Recommendations, land suggestions, shortages, swaps, Complete My Deck exports, and routine failures now use short, direct language rather than repeated implementation disclaimers.
- Keep confirmation dialogs for actual edits and saves, and preserve copy explaining meaningful limitations when needed.

## Fixed
- Verbose, repetitive in-product descriptions made ordinary actions harder to scan and gave the UI an overly technical tone.

## Removed
- Repetitive assurances about not editing the collection from routine tooltips and status messages. No safety behavior, warnings in critical save paths, card data, deck contents, or test coverage removed.

## Verification
- Run the native .NET Windows compiler and full regression suite.
- Open both saved-deck editor and embedded EDH Lab, checking tooltips, shortages, swaps, land guidance, and Complete My Deck exports for clarity at normal window sizes.
- Confirm that actual deck-editing and save confirmation flows remain separate and protected.
