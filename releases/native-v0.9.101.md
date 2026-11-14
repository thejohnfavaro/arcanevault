# Arcane Vault Native Windows v0.9.101 — USB peer sync

## Added
- USB cable peer sync via ADB reverse in **Data & Sync → Device Sync**.
- **Share Sync to Phone (USB)** launches Android with a one-time verified loopback peer link.
- **Receive & Review Phone Sync (USB)** accepts the Android snapshot and invokes the existing three-way merge, conflict review and approved Windows write-back.
- **Cancel USB Sync Offer** closes the loopback listener and removes the ADB reverse mapping.

## Changed
- Wi-Fi and USB transfers now share the same versioned peer snapshot format and merge/rollback engine.
- Cable transport binds Windows to 127.0.0.1 only and requires one authorized Android USB-debugging device.
- No QR code, Wi-Fi or cloud service is needed for USB transport.

## Fixed
- USB peer data no longer needs the legacy collection-only QR/USB importer; decks, printings, Arena and artwork flow through the same peer reconciliation as Wi-Fi.

## Removed
- No existing Wi-Fi, QR, collection or deck features were removed.

## Safety
- Android's USB handoff requires explicit user approval; Windows applies no incoming changes until it receives a valid shared baseline and the user approves the merge.
- ADB platform-tools must be installed or on PATH and USB debugging authorized; users with multiple connected ADB devices must disconnect extras.
- This build does not bypass conflict guards, restore snapshots, deletion safeguards, or the one-use five-minute transfer token.
- Physical cable/device round-trip testing remains required.
