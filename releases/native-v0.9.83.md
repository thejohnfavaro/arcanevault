# Arcane Vault Native Windows v0.9.83 — Bulk proxy additions

## Added
- Collection → Proxies now includes a multiline **Batch Add Proxies** area. Paste cards as `2 Sol Ring`, `3x Counterspell`, or a card name alone (one copy). Duplicate names within a batch are added together, case-insensitively.
- All names in the paste are validated up front; the UI commits a complete batch using one SHA-gated native workspace replacement, one full rollback point, and one collection refresh.
- Regression coverage for additive updates, mixed and repeated card names, invalid input, quantity overflow, stale workspace fingerprints, and preserved real collection quantities.

## Changed
- Batch entry increments existing Proxy counts instead of replacing or deleting existing quantities. The single-card Record / Update and Remove workflows remain available.
- The pasted list is cleared only after a successful verified save. Failed or stale batches retain the text for correction.

## Fixed
- Recording many third-party cards no longer requires repeating the expensive whole-workspace refresh after each card; the refresh happens once after the entire batch has been committed.
- An invalid line or count cannot leave a partially imported proxy list.

## Removed
- The need to create every new proxy card one at a time through the single-card form.

## Safety
- Proxies remain separate from verified physical/Arena holdings, card values, set completion, and price scraping.
- No proxies are silently converted to owned printings. Existing backup, recovery and expected-SHA safeguards remain in effect.
- The new bulk operation is additive, not a destructive import or replacement.
