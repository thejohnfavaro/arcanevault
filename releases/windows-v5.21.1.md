# Arcane Vault for Windows v5.21.1

## Added
- Regression checks for complete QR/USB transfer responses.

## Changed
- QR and USB sync server now consumes the full HTTP request headers and flushes the JSON snapshot before closing the connection.

## Fixed
- Android collection transfers could fail with "Unexpected end of stream" because the Windows socket closed with unread request headers, causing a connection reset and truncated data.

## Removed
- No features removed.
