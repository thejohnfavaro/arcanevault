# Arcane Vault Native Windows v0.9.172 (Preview)

## Added
- Unicode punctuation tolerance for catalog-verified AI Review card names, including curly and straight apostrophes in recommendations such as Sphinx’s Tutelage.
- A synthetic SQLite regression test verifies AI name variants, duplicate aliases and rejection of unknown card names.

## Changed
- AI Review preserves the original text and typographic styling, while linking to the verified canonical catalog identity.
- Link detection recognizes common smart-apostrophe forms in full sentences, emphasized card names and Cut/Add recommendation tables.

## Fixed
- AI reviews previously missed otherwise valid card names when the model used a typographic apostrophe different from the Scryfall catalog's stored spelling.
- Fixes missing preview for Sphinx’s Tutelage in the Borg Queen swap table when the local catalog contains Sphinx's Tutelage.

## Removed
- Nothing. No collection, ownership, deck, printing, proxy, backup, sync, or ChatGPT account data changes.

## Verification
- Windows WPF compilation and the native regression suite passed on PR #242 before merge.
- Final Windows installer pipeline validates the package. On-device visual confirmation of the card link is still required.
