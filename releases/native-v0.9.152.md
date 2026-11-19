# Arcane Vault Native Windows v0.9.152 (Preview)

## Added
- Regression tests for legacy match attribution on saved-deck rename, including conflicting Arena identifiers and repeat reconciliation.

## Changed
- A successful rename of a uniquely named saved deck now pins eligible older name-attributed Arena match records to the deck's immutable native ID.
- Both desktop deck editors apply the same match-history safeguard. Existing snapshot consumers (Stream Deck and XENEON EDGE) continue to use stable native deck identifiers.

## Fixed
- Historical name-only confirmed match records no longer drop out of deck W/L totals solely because their uniquely identifiable saved deck was renamed.
- Matches with conflicting Arena mappings or unverified inventory names are not reassigned automatically.

## Removed
- Nothing. Physical collection, saved deck contents, previous match outcomes, and existing Arena IDs are preserved.

This is a native Windows preview build, not a claim of real-device hardware verification. Renaming historical ambiguous decks may still require manual match attribution. Keep a private backup before updating.
