# Arcane Vault Native Windows v0.9.87 — EDH workspace navigation polish

## Added
- Commander browser button can initialize commander discovery from the current catalog even when a saved deck was opened directly in the embedded EDH Lab editor.

## Changed
- The EDH Lab sidebar returns to an unfinished draft first. The explicit Commander browser control always shows commander discovery, initializing it if needed.

## Fixed
- Opening Deck Library → Edit in EDH Lab before ever browsing commanders no longer leaves the in-app Commander browser button inert.
- The current draft remains available through Return to draft and other sidebar navigation.

## Removed
- The assumption that a commander-browser view already exists whenever the user entered EDH Lab from a saved deck.

## Safety
- Browsing is read-only, and switching between commander discovery and a draft neither saves nor discards the deck.
- Existing explicit discard confirmation still protects a draft when starting a different commander or saved deck.
