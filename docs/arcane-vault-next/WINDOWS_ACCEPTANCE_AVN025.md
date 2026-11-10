# AVN-025 Windows acceptance: native physical quantity edits

**Status:** Automated regression CI can validate synthetic backups, but real installed-PC acceptance is pending owner testing. This is a prototype bundle, **not a shipping signed installer or an in-app update**.

## Safe preparation

1. Keep the shipping Arcane Vault Windows app and its private data as-is. Close it before any optional legacy migration.
2. In GitHub Actions, open the **Arcane Vault Next - .NET 10 Native Compatibility Spike** run for the AVN-025 pull request or merged commit. Download `ArcaneVaultNext-NativeStandalone` from the **Artifacts** section, unzip it and launch the native executable from that folder on Windows. Do **not** replace installed legacy app binaries.
3. Start with a disposable **Start fresh collection** in Arcane Vault Next, *only if it is safe to discard/replace the current native workspace*. If there is already native data you care about, avoid resetting it; use `Restore native recovery` if a rollback is needed. A reset of the native workspace is a separate, destructive operation even though it saves a recovery copy.
4. Use **Keep catalog natively** or a read-only catalog snapshot, then **Open native collection**.

## Acceptance walkthrough

- **A: Exact printing add.** Use **Add physical printing** to search a distinctive card and select a precise set + collector number. Add two nonfoil copies; verify the physical list displays 2 and Set Progress credits precisely the chosen verified slot (where metadata permits).
- **B: Same card, different printing/finish.** Add one foil copy of the same printing and/or one copy from another set. Verify they appear as distinct lines; do not combine them by card name.
- **C: Reduce one exact row.** In the Physical Collection tab select the 2-copy nonfoil row and choose **Reduce / remove selected**. Set quantity to 1, review the confirmation. Verify only that row changes; foil/other editions remain intact. Reopen the native collection to verify persistence after app restart.
- **D: Remove one row.** Select the row you changed to 1, choose **Reduce / remove selected**, set quantity to 0, type **REMOVE** and confirm. Verify exactly that line disappears and Set Progress changes only if that was the last owned verified slot.
- **E: Cancel or reject.** Verify Cancel or No makes no change. A stale open collection view must reject edits after another operation changed the native workspace (reopen before retrying).
- **F: Full recovery.** From the main window choose **Restore native recovery**, select the latest pre-delete checkpoint, inspect the indicated totals, and confirm. Ensure the removed row and copy count return. Restart and re-open the collection. Existing earlier recovery files must remain available.
- **G: Preservation.** Before/after, check Arena quantities, paper unresolved count, decks/deck revisions, preferred artwork, and the shipping legacy app's collection have **not** changed. If you imported from the legacy app, do not assume two-way sync: native edits never write back to it.
- **H: Failure recovery.** If opening or editing fails, stop, keep the `%LOCALAPPDATA%\\ArcaneVaultNext` SQLite file and `.rollback-` files together, and report the error; do not delete data to clear the error.

## Report useful acceptance evidence

Provide the native app's version/build/Actions run ID, initial/final paper and Arena copy totals, chosen card/set/collector and finishes, observed recovered totals, whether the app successfully restarted, and any error message. **No private collection database or profile dump needs to be publicly posted.**

## Non-goals

This does not exercise Android USB/QR sync, scanner OCR, native Arena edits, automatic live legacy migration correctness, deck editing, price ingestion, or EDH Lab. Native and legacy collections are not synchronized.
