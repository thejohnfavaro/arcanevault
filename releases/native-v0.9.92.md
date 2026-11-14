# Arcane Vault Native Windows v0.9.92 — Collection integrity regression gates

## Added
- Regression tests that compare full native backups after rebalancing quantities between two distinct physical printings while keeping total card count unchanged.
- Regression tests for metadata-only differences between otherwise matching inventories, plus verification that comparison leaves the native workspace and recovery history unchanged.

## Changed
- Release validation now explicitly covers per-printing parity and opaque backup content differences as separate conditions.

## Fixed
- No runtime behavior changed. These tests guard against future regressions where equal aggregate quantities conceal changes to individual printings.

## Removed
- No functionality removed.

## Safety
- Tests use synthetic local fixtures and read-only comparisons; no user collection, decks, proxies, pricing, or backup files are changed.
- Do not publish or label v1.0 until native .NET 10 tests and Windows x64 packaging pass and real Windows acceptance is completed.
