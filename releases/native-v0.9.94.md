# Arcane Vault Native Windows v0.9.94 — Wi-Fi peer snapshot review

## Added
- **Share Sync to Phone (Wi-Fi)**: hosts a one-use Windows peer snapshot for Android over a trusted private LAN.
- **Receive Phone Sync (Wi-Fi)**: accepts one Android peer snapshot for record-level, read-only review on Windows.
- **Cancel Wi-Fi Sync Offer**: immediately invalidates the active one-time link.
- Each offer uses a new random 192-bit bearer token, lasts at most five minutes, and accepts a single transfer.

## Changed
- Peer review does not require manually transferring a JSON file when the phone and PC can reach each other over trusted Wi-Fi.
- The original file-based peer import/export and three-way comparison remain available.

## Fixed
- The user can review phone collection/deck differences on Windows without a cable or cloud account.

## Removed
- No functionality removed.

## Safety
- **This is manual wireless snapshot transfer and read-only comparison, not two-way write-back.**
- Local HTTP transport is not encrypted; use only a trusted home network. A future authenticated TLS pairing layer is required for automatic sync. The URL is a secret bearer credential; do not share it or use public Wi-Fi.
- Strict size limits, short expiry, randomized one-use URLs and no remote write endpoints. Collection backups and existing rollback remain unchanged.
