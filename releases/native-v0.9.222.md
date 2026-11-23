# Arcane Vault Windows v0.9.222 (Preview)

## Added
- Stable WPF UI Automation identifiers on both saved-deck and embedded EDH Lab editors for visual mainboard list, search, composition, full details, and artwork/preview status.
- Identifiable Deck Analytics headings for the format, explicitly recorded opponent-color, and partial observed opponent-color matchup panels.
- Standalone **read-only UI smoke test** in `tools/TestCenter/Run-UiSmoke.ps1`: attaches only to an existing Arcane Vault window, checks visible mainboard geometry, detects a selected card preview stuck on "Loading local card details", and verifies analytics headings remain ordered when panels wrap.
- CI geometry-fixture and source contract checks independent of an interactive desktop, with JSON/text smoke reports and explicit PASS/FAIL/SKIP outcomes.

## Changed
- Windows Test Center v0.6.1 provides the optional **UI smoke (open app)** action. Checks never start Arcane Vault, edit a deck, click Save, or write to the user collection.

## Fixed
- UI regression diagnostics previously relied on source-code presence checks and could not inspect the actual live WPF control bounds.
- PowerShell success now exits explicitly with code zero, preventing stale `LASTEXITCODE` values from misreporting a passing self-test.

## Removed
- Nothing. No deck contents, ownership, saved printings, Arena matches, match annotations, or Edge export records are modified by the new accessibility hooks.

## Verification
- Run all native Windows compilation, regression, packaging, and installer checks.
- Run Test Center CI's `Run-UiSmoke.ps1 -SelfTest` and automation-ID contract checks. **The self-test does not claim to exercise a real desktop.**
- On a Windows machine, open Deck Analytics and scroll to the three matchup headings; run `UI smoke (open app)`. Then open a saved deck's Manage tab, select a card, and repeat. Review UI-SMOKE reports for PASS/FAIL/SKIP.
- Real bitmap pixel correctness, end-to-end UI keyboard/mouse behavior, hardware widgets, and a real installed-app acceptance run remain manual.
