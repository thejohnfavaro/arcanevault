# Arcane Vault for Windows v5.21.3

## Added
- Resumable physical collection valuation with completed-price checkpoints after every Scryfall batch.
- Four attempts with increasing delays for transient price lookup failures.
- Partial-refresh status reporting that identifies failed batch ranges.

## Changed
- Refresh Values continues through other printings when a batch fails instead of abandoning all progress.
- Recently successful quotes can be reused for up to 24 hours; unresolved and failed printings are retried on a later refresh.
- Ownership quantities remain unchanged, and price estimates continue to use exact printing and finish identifiers.

## Fixed
- Physical Collection valuation previously appeared to stop partway through on a failed Scryfall batch and discarded all prices accumulated during that run.

## Removed
- All-or-nothing pricing behavior.
