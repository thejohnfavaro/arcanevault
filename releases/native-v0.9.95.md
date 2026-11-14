# Arcane Vault Native Windows v0.9.95 — Scan-to-sync QR codes

## Added
- Native scannable QR codes for both **Share Sync to Phone (Wi-Fi)** and **Receive Phone Sync (Wi-Fi)**.
- Local QR generation; your one-time sync link and bearer token never go to a third-party QR image service.
- Copyable URL and manual fallback, with five-minute expiry clearly displayed.

## Changed
- The Windows Wi-Fi offer opens a dark, phone-readable QR dialog instead of a long text-only link dialog.
- QR scanning is available from Android Data & Sync through its existing Google Code Scanner, which requires no extra camera permission.

## Fixed
- Users no longer need to manually type/copy the long Wi-Fi peer link between PC and phone.

## Removed
- No features removed. Manual links and file export remain supported.

## Safety
- QR contains a short-lived secret URL. Do not share screenshots or use public Wi-Fi.
- The underlying local HTTP transfer is not encrypted; use a trusted home network.
- Wireless peer exchange remains read-only review with **no automatic cross-device collection edits**. Pairing and encrypted transport remain follow-up work.
