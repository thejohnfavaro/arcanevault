# Arcane Vault Native Windows v0.9.141 (Preview)

## Added
- Stream Deck and XENEON EDGE snapshot reconciliation against user-confirmed persistent Arena deck ID mappings.

## Changed
- Exported deck W/L uses the same stable native deck identities and confirmed Arena ID mappings as the current Match History workflow.
- Existing `streamdeck.json` schema remains backward compatible with Stream Deck Companion and EDGE widgets.

## Fixed
- Arena matches linked through remembered native-deck mappings now contribute to the correct saved deck's W/L and recent-match history in the snapshot.
- Unknown or conflicting explicit Arena/native deck IDs cannot fall back to an unrelated same-name deck.
- When an active Arena ID cannot be resolved, the snapshot does not guess an active saved deck by name.

## Removed
- Speculative name matching when a match already declares a nonmatching stable deck ID.

> This release updates the native shared snapshot exporter used by both integrations. It does not modify the separately distributed Stream Deck Companion's own source or promise that a key-side manual W/L write is persisted. W/L remains derived from stored Arcane Vault match records.
