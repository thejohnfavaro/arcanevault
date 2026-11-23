# Arcane Vault Windows v0.9.216 (Preview)

## Added
- Android-inspired Collapse All / Expand All controls in Windows' visual mainboard editor, with clickable type headers and visual hidden-card counts.
- Direct, exact Scryfall printing artwork lookup for Arena decklist labels such as `Island (FIN) 297`, avoiding the 300-item gallery limit on heavily reprinted cards.
- Regression checks for both desktop editor variants and official exact-printing artwork.

## Changed
- Mainboard group collapse state affects visual presentation only; source Text tab, card quantities, owned cards, saved revisions and match histories stay authoritative and unchanged.
- Editor card preview resolves explicit selected printing first, then embedded Arena set/collector hint, then official representative artwork.

## Fixed
- Saved-deck preview could show placeholder artwork for valid Arena-formatted printings.
- v0.9.215 Windows Actions run 45098924613 passed desktop compilation but failed headless regression project compilation due duplicate `draftEditorCode` local in `NativeVisualShellChecks.cs` (CS0128); renamed the added local to avoid collision.

## Removed
- Nothing from existing editor workflows, card details, EDH planning or Xeneon Edge behavior.

## Verification
- Run Windows CI and `native-printing-artwork` and `native-visual-shell` test groups. v0.9.215's error was isolated to test compilation; the v0.9.216 run must complete before installation.
- Verify `Island (FIN) 297` card art in We Are the Borg saved-deck preview, and expand/collapse groups in both saved editor and EDH Lab. Confirm unsaved deck text is never modified by collapsing.
