# Arcane Vault for Windows — v5.20.0

## Release overview

This release adds a per-card preferred printing artwork choice, a portable Android/Windows collection sync format, a one-time QR pairing workflow for transferring the snapshot directly from Windows to Android over private Wi-Fi, and numerical collector-number sorting inside Set Progress.

## Added

- **Save preferred printing artwork:** In Physical Collection → Choose printing, select an edition and choose **Use this printing's artwork**. Card Browser, Card Details and Set Progress use the saved image. The preference survives restarts independently of physical quantity and can be reset from Card Details.
- **Portable collection snapshots:** Export a JSON file containing Arena quantities, all physical printing records (including Scryfall IDs, quantities, finishes, condition and storage fields), and preferred artwork. Import the file on another device without reimporting your original CSV.
- **Selective import and undo:** Choose Physical + artwork, Arena only, or Both. The import replaces only the selected inventory rather than adding quantities, asks for explicit confirmation, and saves a rollback snapshot.
- **QR-based transfer to Android:** Physical Collection → **Show QR for Android** creates a temporary LAN URL rendered into a QR code offline. On Android v2.1.0, use **Scan Windows QR** on the same private Wi-Fi. A one-use cryptographic token expires after five minutes; the code does not itself contain card data.
- **Set Progress collector sort:** In an open set, sort by Collector Number or Card Name. Collector numbers are ordered numerically in the catalog before pagination; print numbers appear on the cards and in the set CSV page export.

## Changed

- Full Arcane Vault backup includes saved preferred artwork selections.
- Set card sort and CSV exports include each displayed collector number.
- Sharing is initiated only by clicking Show QR, not during background sync. QR creation uses a locally bundled MIT-licensed QRCode.js renderer, not a remote image service.

## Fixed

- Selected printing artwork no longer disappears when Card Browser is reopened.
- Physical printing artwork preferences remain separate from Arena ownership and metadata refreshes.
- Numerical collector order is maintained across page boundaries rather than sorting only the current page.

## Removed

- No existing features removed.

## QR and synchronization limits

- **QR pairing requires Windows v5.20.0 and Android v2.1.0 or later**, both on a trusted private Wi-Fi / LAN network. Windows Firewall may need to allow the application's temporary inbound local connection.
- The QR code is a short one-time local address; actual JSON data travels directly over the LAN (unencrypted HTTP) and is not uploaded to the cloud. Do not use this mechanism on untrusted networks.
- The transfer lasts five minutes or one successful download; scanning again after use requires a fresh QR.
- This release synchronizes **collection ownership and artwork preferences**, not automatically decks, matches, Scryfall caches, or continuously changing Windows Arena game logs.
- Android → Windows transfer uses the exported JSON import route. Automatic bidirectional cloud/background sync is not included.
- The Android scanner uses Google Play services barcode scanning. If unavailable, export/import the portable JSON file instead.
- No card data is erased or added without a confirmed import; the selected section is replaced by the incoming snapshot, and Undo last sync import is offered.

## Installing

Update through **Import & Data → Application updates → Check for updates** or download the Windows ZIP and SHA-256 checksum from this release. For Android, build the separately provided Android Studio v2.1.0 source package.
