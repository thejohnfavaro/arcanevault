# Arcane Vault Native Windows v0.9.165 (Preview)

## Added
- Full-width AI Review workspace within the existing EDH Lab, including a Return to deck editor action. No additional review or results windows.
- Native, read-only rendering of ChatGPT recommendations with formatted Markdown headings, bold and italic emphasis, lists, paragraphs and proposal tables.
- In-place ChatGPT account connection controls, analysis mode selector, optional deck-only ownership context, inspectable prompt, Copy prompt and Copy review.
- Regression coverage for Arena-style set/collector annotations in offline deck analytics.

## Changed
- **AI Review** in EDH Lab and the Options navigation action now open the integrated review workspace rather than modal dialogs. The current draft remains in memory and remains editable when returning.
- ChatGPT review response is formatted in native WPF instead of displayed as raw Markdown in a separate text window.
- Offline analytics normalizes imported MTGA printing annotations for catalog lookup without rewriting saved deck text.

## Fixed
- Deck Analytics could show only 1/100 matched copies and incorrect mana, color, and type charts when printing annotations were included in imported card names.
- Raw Markdown delimiters and table pipes previously appeared in AI review results; basic Markdown is now formatted.

## Removed
- Removed the old modal AI prompt/review windows from the active EDH Lab review flow. No data, collection, deck, proxy, sync, snapshot, or backup records are deleted.

## Verification
- PR #235 successfully compiled the native Windows WPF project and passed the native regression suite and portable bundle packaging before merge. The installer verification and final release pipeline must still complete.
- Live subscription authorization and AI responses had been tested through the prior modal version; this redesigned interface requires a hands-on Windows visual test after installation.
- AI advice remains read-only; no automatic deck edits or unsolicited collection uploads.
