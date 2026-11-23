# Arcane Vault Windows v0.9.217 (Preview)

## Added
- Regression assertions for both the exact `TRK #428` and `SLD #123` Sol Ring printings, plus missing collector numbers and untrusted image URLs.

## Changed
- Corrected the exact printing-artwork regression fixture expectations to match the URLs actually stored in the synthetic offline catalog.

## Fixed
- v0.9.216 GitHub Actions run 45099118596 compiled the Windows application and passed Scryfall sync tests, but failed `native-printing-artwork`. The test incorrectly compared `TRK #428` against the `SLD #123` printing image. The test now expects the TRK printing's own `paper-alt.jpg` artwork, and separately checks the SLD image.
- Retains the card-preview exact set/collector lookup and Android-style collapsible card-type groups in both Windows deck editors.

## Removed
- The incorrect cross-printing artwork assertion. No actual deck, collection, or match data was modified.

## Verification
- Confirm from run 45099118596: WPF app compiled with zero errors; failure was the incorrect `native-printing-artwork` fixture assertion.
- Re-run the native printing artwork and visual-shell suites, then build the Windows installer.
- After a passing release, check `Island (FIN) 297` artwork in the saved Borg deck and Collapse All / Expand All in the Cards/Manage view.
