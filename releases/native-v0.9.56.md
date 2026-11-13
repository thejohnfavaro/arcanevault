# Arcane Vault Native Windows v0.9.56 — Visible inventory clear acknowledgement

## Added

- A pinned **Required backup acknowledgement** section in the selective inventory clear dialog, always visible directly above the action buttons.
- A native layout regression check that requires the acknowledgement and actions outside the scrollable information area.

## Changed

- Increased the default inventory-clear dialog height and made only its informational content and scope/typed phrase form scroll at smaller windows or display scaling.
- Replaced the former fixed-width acknowledgement text with a wrapping text block that fits the dialog at its minimum width.
- Kept the existing typed phrases (`CLEAR PHYSICAL`, `CLEAR ARENA`, `CLEAR BOTH`) and the explicit backup acknowledgement as separate required confirmations.

## Fixed

- The **Clear selected inventory** button could stay disabled even after entering the exact phrase because the mandatory acknowledgement checkbox was clipped out of view in a zero-height star grid row.
- The action can now be enabled by the existing confirmation logic after the correct phrase is entered **and** the visible backup acknowledgement is checked.

## Removed

- No data-safety checks, SHA guard, workspace rollback, deck preservation or inventory reset options were removed.

## Acceptance

1. Use the native Windows v0.9.56 preview with a full export of your collection saved separately.
2. Open Collection & Deck Library → Clear inventory….
3. Select Physical collection only and type `CLEAR PHYSICAL`. Confirm the destructive button is still disabled until you check the visible backup acknowledgement.
4. Check the acknowledgement; confirm the button enables.
5. Uncheck the acknowledgement or change the phrase; confirm the button disables again.
6. Repeat with Arena and Both phrases, and verify that scrolling or reducing the dialog height cannot hide the acknowledgement or action buttons.
7. If you decide to perform a clear, verify the retained rollback and the other collection and saved decks; reimport only after verifying your full backup.

**Scope:** Native Windows preview v0.9.56. Not Android and not a final 1.0 release. CI verifies compilation and regression checks; Windows display scaling should still be inspected on the actual device.
