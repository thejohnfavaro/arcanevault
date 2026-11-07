# Arcane Vault for Windows v5.21.0

## Added
- **Show USB transfer QR** in Physical Collection for Galaxy devices connected by a USB cable with USB debugging enabled.
- Local USB tunnel through Android Debug Bridge (ADB) to bypass Wi-Fi routing and Windows LAN firewall restrictions.
- Clear ADB setup guidance and a USB file-transfer alternative using the exported collection sync JSON.

## Changed
- USB transfers use the same one-time, five-minute collection snapshot protocol as Wi-Fi QR transfers, but only bind to Windows loopback.
- ADB requires a user-authorized USB debugging connection and does not automatically use wireless devices.

## Fixed
- Collection sync can now work when the PC and phone are unable to reach one another over the local Wi-Fi network.

## Removed
- No features removed.
