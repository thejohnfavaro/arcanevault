# Arcane Vault Native Windows v0.9.183 (Preview)

## Changed
- An embedded EDH Lab deck save now reuses the **already independently verified** atomic-save result to refresh only the saved deck list, preserving the existing Collection UI, physical/Arena ownership, catalog review, set progress, pricing and filters.
- The same verified deck-only refresh previously used for Deck Library edits is now available to EDH Lab saves.
- After a deck-only save, remain on the Deck Library without replacing the entire workspace with a blocking `Reloading your verified local collection…` screen.

## Fixed
- Avoids unnecessary full collection reopening, catalog re-reconciliation and set-progress rebuilding following an ordinary EDH Lab deck edit.

## Added
- Regression guard covering the EDH Lab save handler's fast verified deck-only path and safe full-reload fallback.

## Safety and verification
- The underlying save still uses the existing optimistic-SHA checks, staging, atomic SQLite replacement, rollback file and integrity verification.
- If inventory changes, a mismatch is detected or the fast refresh fails, Arcane Vault retains the full verified reload fallback.
- Local hardware performance still needs testing; this does not promise a measured latency or eliminate the time spent performing the underlying verified save itself.
