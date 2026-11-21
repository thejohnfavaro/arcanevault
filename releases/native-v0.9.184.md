# Arcane Vault Native Windows v0.9.184 (Preview)

## Added
- Removal-only incremental physical printing verification and Set Progress ownership recomputation after an already-verified physical-to-proxy transfer.
- Regression checks comparing incremental transfer results with a full catalog verification and rejecting ownership increases.
- Shared DPI-friendly search textbox style with vertically centered text across Catalog, Deck Library, Set Progress and EDH Lab.
- Responsive Card Details reading layout: full-height metadata, format legalities and Oracle panels side by side on wide windows, stacked on compact windows.

## Changed
- A proxy-ledger-only quantity change or batch edit refreshes the Proxy list in place after validating its already-verified atomic replacement; owned cards, pricing and set progress remain cached.
- Physical-to-proxy transfers reuse verified printing identities and unchanged catalog denominators, update ownership counts, and trigger pricing recalculation asynchronously. The Catalog ownership overlay is refreshed for the changed inventory.
- More Card Details width is allocated to artwork and long legalities/Oracle text where window size allows.

## Fixed
- Avoids the prolonged full collection reload for eligible proxy-only and physical-to-proxy edits.
- Improves clipped descenders in search boxes caused by excessive vertical padding and uneven text alignment.
- Prevents the Card Details metadata block from staying fixed at a small height in a large window.

## Removed
- Nothing from persistent data. No ownership, deck history, sync record, preferences or recovery file is deleted.

## Verification
- All writes retain optimistic SHA checks, staging, atomic SQLite replacement and rollback/reverification. Incremental refresh rejects unsupported changes and retains the full reload as a safety fallback.
- The full native regression suite and official Windows packaging workflow must pass. Verify proxy transfer, proxy quantity editing, Card Details at normal/maximized sizes, and Catalog text input at the target display's scaling.
- Physical printing additions and other inventory changes not covered by the removal-only path still use the full safe re-open.
