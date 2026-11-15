# Arcane Vault Native Windows v0.9.114 — Clear Deck Health Validation

## Added
- Explicit blocker details in Deck Health, including over-limit mainboard count and the names/quantities of unknown or format-ineligible cards.
- Combined blocker text when multiple issues exist, with specific next actions.

## Changed
- Verified-card role coverage stays visible as provisional analysis instead of labelling every area "Review unavailable".
- Deck Health buttons explain when the underlying deck must be corrected before owned-card recommendations can be browsed.

## Fixed
- Overfilled or partially unverified Commander drafts previously marked every Deck Health row "Review unavailable", masking useful read-only role counts and suggested target comparisons.

## Removed
- No catalog, deck, ownership, printing, proxy or saved configuration data removed.

## Safety
- Recommendations remain disabled until the commander mainboard is at most 99 cards and all draft card names are eligible in the selected format and identity.
- Analysis is local and read-only. No collection or deck changes are made.
